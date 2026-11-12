using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

/// <summary>
/// One ordinary SDK fallback, not a global query provider. CmdPal owns whether and
/// where it is shown. See docs/CMDPAL-GLOBAL-SEARCH.md for versioned host evidence.
/// </summary>
internal sealed partial class PlayniteGameFallback : FallbackCommandItem, IDisposable
{
    private const string FallbackId = "playnitecmdpal.open-game";
    private readonly object _gate = new();
    private readonly PlayniteDirectLaunch _directLaunch;
    private readonly Func<string, CommandResult> _launch;
    private CancellationTokenSource? _queryCancellation;
    private int _version;
    private bool _disposed;
    private string _title = string.Empty;

    public PlayniteGameFallback(IReadOnlyPlayniteCatalog? catalog = null, Func<string, CommandResult>? launch = null)
        : base(new NoOpCommand(), "Open a game with Playnite", FallbackId)
    {
        _directLaunch = new PlayniteDirectLaunch(catalog ?? new PlayniteCatalogAdapter());
        _launch = launch ?? (id => new LaunchGameCommand(id).Invoke());
        Icon = new IconInfo("\uE7FC");
    }

    // CommandItem normally substitutes Command.Name for an empty Title. For a
    // fallback, empty must really mean hidden, never an unrelated generic action.
    public override string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    // The SDK ABI is synchronous. The inspected host dispatches fallback RPC on
    // background workers. Complete the update before returning so its title-change
    // detection sees the result; do not use async void or fire-and-forget publication.
    public override void UpdateQuery(string query) => UpdateQueryAsync(query).GetAwaiter().GetResult();

    internal async Task UpdateQueryAsync(string? query)
    {
        int version;
        CancellationToken token;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _queryCancellation?.Cancel();
            _queryCancellation?.Dispose();
            _queryCancellation = new CancellationTokenSource();
            token = _queryCancellation.Token;
            version = ++_version;
            ClearResult();
        }

        try
        {
            var game = await _directLaunch.FindMatchAsync(query, token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || version != _version || token.IsCancellationRequested || game is null)
                {
                    return;
                }

                // Each published command captures an immutable ID and query generation.
                // A host retaining an earlier command can never launch the newer match.
                Command = new FallbackLaunchCommand(this, version, query!, game.GameId);
                Subtitle = "Open with Playnite";
                Title = game.Title;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer query or disposal invalidated this request.
        }
        catch (Exception)
        {
            // No noisy error row in global search. Search games exposes recovery UX.
        }
    }

    private async Task<CommandResult> InvokeAsync(int version, string query, string gameId)
    {
        CancellationToken token;
        lock (_gate)
        {
            if (_disposed || version != _version)
            {
                return Unavailable();
            }

            token = _queryCancellation!.Token;
        }

        try
        {
            var valid = await _directLaunch.RevalidateAsync(query, gameId, token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || version != _version || token.IsCancellationRequested)
                {
                    return Unavailable();
                }

                // Consume this generation before dispatch, including when validation
                // fails. Overlapping invocations cannot launch the same row twice.
                ++_version;
                ClearResult();
                return valid ? _launch(gameId) : Unavailable();
            }
        }
        catch (Exception)
        {
            lock (_gate)
            {
                if (version == _version)
                {
                    ++_version;
                    ClearResult();
                }
            }

            return Unavailable();
        }
    }

    private void ClearResult()
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Command = new NoOpCommand();
    }

    private static CommandResult Unavailable() => CommandResult.ShowToast(
        "This Playnite match is no longer available or couldn't be verified. Open Search games, refresh the catalog, and choose the game there.");

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            ++_version;
            _queryCancellation?.Cancel();
            _queryCancellation?.Dispose();
            _queryCancellation = null;
            ClearResult();
        }

        GC.SuppressFinalize(this);
    }

    private sealed partial class FallbackLaunchCommand : InvokableCommand
    {
        private readonly PlayniteGameFallback _owner;
        private readonly int _version;
        private readonly string _query;
        private readonly string _gameId;

        public FallbackLaunchCommand(PlayniteGameFallback owner, int version, string query, string gameId)
        {
            _owner = owner;
            _version = version;
            _query = query;
            _gameId = gameId;
            Id = $"playnitecmdpal.open-game.{gameId}";
        }

        public override string Name => "Open with Playnite";

        public override CommandResult Invoke() => _owner.InvokeAsync(_version, _query, _gameId).GetAwaiter().GetResult();
    }
}
