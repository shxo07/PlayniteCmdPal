using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

/// <summary>
/// The extension's single dynamic fallback. It displays one exact, unambiguous,
/// installed Playnite title at a time; it never registers every game at the root.
/// </summary>
internal sealed partial class PlayniteGameFallback : FallbackCommandItem, IDisposable
{
    private const string FallbackId = "playnitecmdpal.open-game";
    private const string ActionSubtitle = "Open with Playnite";
    private readonly object _gate = new();
    private readonly PlayniteDirectLaunch _directLaunch;
    private readonly Func<string, CommandResult> _launch;
    private readonly ArtworkIconLoader _artwork;
    private FallbackState? _state;
    private CancellationTokenSource? _queryCancellation;
    private Task _artworkUpdate = Task.CompletedTask;
    private int _version;
    private bool _disposed;

    internal PlayniteGameFallback(
        IReadOnlyPlayniteCatalog catalog,
        Func<string, CommandResult>? launch = null,
        ArtworkIconLoader? artwork = null)
        : base(new NoOpCommand(), "Open a game with Playnite", FallbackId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _directLaunch = new PlayniteDirectLaunch(catalog);
        _launch = launch ?? (id => new LaunchGameCommand(id).Invoke());
        _artwork = artwork ?? new ArtworkIconLoader(PlayniteCatalogAdapter.ResolveArtworkPath);
        Volatile.Write(ref _state, new FallbackState(
            string.Empty,
            string.Empty,
            ArtworkIconLoader.FallbackIcon,
            base.Command ?? new NoOpCommand(),
            null));
    }

    internal Task ArtworkUpdate
    {
        get
        {
            lock (_gate) return _artworkUpdate;
        }
    }

    // A blank fallback title must stay blank rather than falling back to Command.Name.
    public override string Title
    {
        get => Volatile.Read(ref _state)?.Title ?? string.Empty;
        set
        {
            if (Volatile.Read(ref _state) is null) base.Title = value;
            else UpdatePublishedState(state => state with { Title = value });
        }
    }

    public override string Subtitle
    {
        get => Volatile.Read(ref _state)?.Subtitle ?? base.Subtitle;
        set
        {
            if (Volatile.Read(ref _state) is null) base.Subtitle = value;
            else UpdatePublishedState(state => state with { Subtitle = value });
        }
    }

    public override IIconInfo? Icon
    {
        get => Volatile.Read(ref _state)?.Icon ?? base.Icon;
        set
        {
            if (Volatile.Read(ref _state) is null) base.Icon = value;
            else UpdatePublishedState(state => state with { Icon = value });
        }
    }

    public override ICommand? Command
    {
        get => Volatile.Read(ref _state)?.Command ?? base.Command;
        set
        {
            if (Volatile.Read(ref _state) is null) base.Command = value;
            else UpdatePublishedState(state => state with { Command = value });
        }
    }

    // The SDK callback is synchronous. CmdPal dispatches fallback callbacks on its
    // background workers; finish the catalog match before returning so its title-change
    // detection observes the result. Saved-icon decoding remains independent/asynchronous.
    public override void UpdateQuery(string query) => UpdateQueryAsync(query).GetAwaiter().GetResult();

    internal Task UpdateQueryAsync(string? query)
    {
        CancellationTokenSource? superseded;
        CancellationTokenSource current;
        CancellationToken queryToken;
        int version;
        FallbackState previous;
        FallbackState cleared;
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;

            superseded = _queryCancellation;
            current = new CancellationTokenSource();
            queryToken = current.Token;
            _queryCancellation = current;
            version = ++_version;
            _artworkUpdate = Task.CompletedTask;
            previous = _state!;
            cleared = EmptyState();
            Volatile.Write(ref _state, cleared);
        }

        CancelAndDispose(superseded);
        NotifyStateChanged(previous, cleared);
        if (string.IsNullOrWhiteSpace(query)) return Task.CompletedTask;
        // Capture the token before property notifications: listeners may re-enter with
        // a newer query and dispose this source while the notifications are in flight.
        return ResolveAndPublishAsync(query, version, queryToken);
    }

    private async Task ResolveAndPublishAsync(string query, int version, CancellationToken token)
    {
        try
        {
            var game = await _directLaunch.FindMatchAsync(query, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (game is null) return;

            FallbackState previous;
            FallbackState published;
            lock (_gate)
            {
                if (_disposed || version != _version || token.IsCancellationRequested) return;

                previous = _state!;
                published = new FallbackState(
                    game.Title,
                    ActionSubtitle,
                    ArtworkIconLoader.FallbackIcon,
                    new FallbackLaunchCommand(this, version, query, game.GameId),
                    game);
                Volatile.Write(ref _state, published);

                // Starting decode on a worker prevents a synchronously completing resolver
                // from holding up the host's fallback callback. The callback never awaits it.
                _artworkUpdate = Task.Run(() => LoadArtworkAsync(game, version, token));
            }

            NotifyStateChanged(previous, published);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer query, invocation, or disposal invalidated this work.
        }
        catch (Exception)
        {
            // An unavailable/invalid catalog yields no noisy fallback row.
        }
    }

    private async Task LoadArtworkAsync(GameEntry game, int version, CancellationToken token)
    {
        try
        {
            var icon = await _artwork.GetIconAsync(game, token).ConfigureAwait(false)
                ?? ArtworkIconLoader.FallbackIcon;
            bool changed;
            lock (_gate)
            {
                var current = _state;
                if (_disposed || version != _version || token.IsCancellationRequested || current is null ||
                    current.Game?.GameId != game.GameId || ReferenceEquals(current.Icon, icon))
                {
                    return;
                }

                Volatile.Write(ref _state, current with { Icon = icon });
                changed = true;
            }

            // A property listener can synchronously call back into this item. Never raise
            // SDK notifications while holding the query/state lock.
            if (changed) OnPropertyChanged(nameof(Icon));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer query, invocation, or disposal no longer needs this image.
        }
        catch (Exception)
        {
            // Missing/corrupt saved icons are optional and never prevent a valid launch.
        }
    }

    private async Task<CommandResult> InvokeAsync(int version, string query, string gameId)
    {
        CancellationToken token;
        lock (_gate)
        {
            if (_disposed || version != _version || _state?.Game?.GameId != gameId || _queryCancellation is null)
            {
                return Unavailable();
            }

            token = _queryCancellation.Token;
        }

        try
        {
            var valid = await _directLaunch.RevalidateAsync(query, gameId, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!valid)
            {
                ClearIfCurrent(version);
                return Unavailable();
            }

            // Consume this result before dispatch. A stale retained command or overlapping
            // invocation cannot launch a second game or retarget a later query.
            if (!TryClearCurrent(version, out var cancellation)) return Unavailable();
            CancelAndDispose(cancellation);
            return _launch(gameId);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            ClearIfCurrent(version);
            return Unavailable();
        }
        catch (Exception)
        {
            ClearIfCurrent(version);
            return Unavailable();
        }
    }

    private void ClearIfCurrent(int version)
    {
        if (TryClearCurrent(version, out var cancellation)) CancelAndDispose(cancellation);
    }

    private bool TryClearCurrent(int version, out CancellationTokenSource? cancellation)
    {
        FallbackState previous;
        FallbackState cleared;
        lock (_gate)
        {
            if (_disposed || version != _version)
            {
                cancellation = null;
                return false;
            }

            ++_version;
            cancellation = _queryCancellation;
            _queryCancellation = null;
            _artworkUpdate = Task.CompletedTask;
            previous = _state!;
            cleared = EmptyState();
            Volatile.Write(ref _state, cleared);
        }

        NotifyStateChanged(previous, cleared);
        return true;
    }

    private void UpdatePublishedState(Func<FallbackState, FallbackState> update)
    {
        FallbackState previous;
        FallbackState next;
        lock (_gate)
        {
            previous = _state!;
            next = update(previous);
            Volatile.Write(ref _state, next);
        }

        NotifyStateChanged(previous, next);
    }

    private void NotifyStateChanged(FallbackState previous, FallbackState next)
    {
        if (!ReferenceEquals(previous.Command, next.Command)) OnPropertyChanged(nameof(Command));
        if (!string.Equals(previous.Subtitle, next.Subtitle, StringComparison.Ordinal)) OnPropertyChanged(nameof(Subtitle));
        if (!ReferenceEquals(previous.Icon, next.Icon)) OnPropertyChanged(nameof(Icon));
        if (!string.Equals(previous.Title, next.Title, StringComparison.Ordinal)) OnPropertyChanged(nameof(Title));
    }

    private static FallbackState EmptyState() => new(
        string.Empty,
        string.Empty,
        ArtworkIconLoader.FallbackIcon,
        new NoOpCommand(),
        null);

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null) return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { }
        cancellation.Dispose();
    }

    private static CommandResult Unavailable() => CommandResult.ShowToast(
        "This installed Playnite match is no longer available or couldn't be verified. Open Search games to browse the library and choose the intended game.");

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        FallbackState previous;
        FallbackState cleared;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_version;
            cancellation = _queryCancellation;
            _queryCancellation = null;
            _artworkUpdate = Task.CompletedTask;
            previous = _state!;
            cleared = EmptyState();
            Volatile.Write(ref _state, cleared);
        }

        CancelAndDispose(cancellation);
        NotifyStateChanged(previous, cleared);
        GC.SuppressFinalize(this);
    }

    private sealed record FallbackState(
        string Title,
        string Subtitle,
        IIconInfo? Icon,
        ICommand? Command,
        GameEntry? Game);

    private sealed partial class FallbackLaunchCommand : InvokableCommand
    {
        private readonly PlayniteGameFallback _owner;
        private readonly int _version;
        private readonly string _query;
        private readonly string _gameId;

        internal FallbackLaunchCommand(PlayniteGameFallback owner, int version, string query, string gameId)
        {
            _owner = owner;
            _version = version;
            _query = query;
            _gameId = gameId;
            Id = $"playnitecmdpal.open-game.{gameId}";
        }

        public override string Name => ActionSubtitle;

        public override CommandResult Invoke() => _owner.InvokeAsync(_version, _query, _gameId).GetAwaiter().GetResult();
    }
}
