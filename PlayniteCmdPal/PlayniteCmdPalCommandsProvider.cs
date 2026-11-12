// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See third-party/Microsoft-PowerToys-LICENSE.txt for the license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

public partial class PlayniteCmdPalCommandsProvider : CommandProvider
{
    internal static readonly TimeSpan CatalogPollInterval = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private readonly ICommandItem[] _navigationCommands;
    private readonly PlayniteDirectLaunch _directLaunch;
    private readonly ArtworkIconLoader _artwork;
    private readonly Func<string, CommandResult> _launch;
    private readonly ITimer? _refreshTimer;
    private ICommandItem[] _commands;
    private GameRow[] _gameRows = [];
    private CancellationTokenSource? _refreshCancellation;
    private CancellationTokenSource? _publicationCancellation = new();
    private Task _refreshTask = Task.CompletedTask;
    private Task _artworkUpdate = Task.CompletedTask;
    private int _refreshVersion;
    private int _publicationVersion;
    private bool _disposed;

    // Page construction lives in the other partial. This constructor keeps the actual
    // provider/SDK path testable without starting pages or reading user preferences.
    internal PlayniteCmdPalCommandsProvider(
        IReadOnlyPlayniteCatalog catalog,
        ICommandItem[] navigationCommands,
        ArtworkIconLoader? artwork = null,
        Func<string, CommandResult>? launch = null,
        bool startBackgroundRefresh = true,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(navigationCommands);
        DisplayName = "Playnite";
        Frozen = false;
        _navigationCommands = navigationCommands.ToArray();
        _commands = _navigationCommands;
        _directLaunch = new PlayniteDirectLaunch(catalog);
        _artwork = artwork ?? new ArtworkIconLoader(id => PlayniteCatalogAdapter.ResolveArtworkPath(id));
        _launch = launch ?? (id => new LaunchGameCommand(id).Invoke());

        if (startBackgroundRefresh)
        {
            _refreshTimer = (timeProvider ?? TimeProvider.System).CreateTimer(
                state => { _ = RefreshCatalogAsync(); }, null, CatalogPollInterval, CatalogPollInterval);
            _ = RefreshCatalogAsync();
        }
    }

    // CmdPal indexes these ordinary root commands and owns query filtering/ranking.
    // Never do disk/image work or recreate the command array in this synchronous call.
    public override ICommandItem[] TopLevelCommands()
    {
        lock (_gate) return _commands;
    }

    public override IFallbackCommandItem[] FallbackCommands() => [];

    public override ICommandItem? GetCommandItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate) return _commands.FirstOrDefault(item => item.Command?.Id == id);
    }

    public override ICommand? GetCommand(string id) => GetCommandItem(id)?.Command;

    internal Task ArtworkUpdate
    {
        get
        {
            lock (_gate) return _artworkUpdate;
        }
    }

    internal IContextItem CreateRefreshContextItem() => new CommandContextItem(new RefreshCommandsCommand(this));

    internal Task RefreshCatalogAsync(bool forceRefresh = false)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            // Timer ticks coalesce. An explicit refresh may supersede an older read.
            if (!forceRefresh && !_refreshTask.IsCompleted) return _refreshTask;

            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            _refreshCancellation = new CancellationTokenSource();
            var version = ++_refreshVersion;
            var token = _refreshCancellation.Token;
            // Cached-catalog projection also runs off the synchronous caller. Do not
            // capture a UI synchronization context that command invocation could block.
            _refreshTask = Task.Run(() => LoadCatalogAsync(version, forceRefresh, token));
            return _refreshTask;
        }
    }

    private async Task LoadCatalogAsync(int version, bool forceRefresh, CancellationToken token)
    {
        try
        {
            var games = await _directLaunch.GetCandidatesAsync(forceRefresh, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            PublishCatalog(games, version, forceRefresh, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer explicit refresh or disposal owns the next publication.
        }
        catch (Exception)
        {
            // Navigation/recovery pages stay available, but failed reads cannot leave
            // last-known-good direct launch commands advertised at the root.
            PublishCatalog(Array.Empty<GameEntry>(), version, forceRefresh: false, token);
        }
    }

    private void PublishCatalog(
        IReadOnlyList<GameEntry> games,
        int version,
        bool forceRefresh,
        CancellationToken token)
    {
        GameCommandItem[] resetIcons = [];
        bool unchanged;
        lock (_gate)
        {
            if (!IsCurrentRefresh(version, token)) return;
            unchanged = SamePresentation(games);
            if (unchanged)
            {
                if (forceRefresh)
                {
                    _artwork.Invalidate();
                    ResetArtworkLocked();
                    resetIcons = _gameRows.Select(row => row.Item)
                        .Where(item => item.SetIconWithoutNotification(ArtworkIconLoader.FallbackIcon)).ToArray();
                }
                StartArtworkLocked();
            }
        }

        // SDK notifications can call back across COM. Never hold the getter lock
        // while notifying the host, including individual Icon property changes.
        foreach (var item in resetIcons) item.NotifyIconChanged();
        if (unchanged) return;

        // Equal-title games remain explicit choices with source/platform/install
        // metadata and their full stable IDs (even GUID-prefix collisions are safe).
        var duplicateTitles = games.GroupBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = games.Select(game => new GameRow(game, new GameCommandItem(
            new OpenGameCommand(this, version, game))
        {
            Title = game.Title,
            Subtitle = CreateGameSubtitle(game, duplicateTitles.Contains(game.Title)),
        })).ToArray();
        ICommandItem[] commands = [.. _navigationCommands, .. rows.Select(row => row.Item)];
        lock (_gate)
        {
            if (!IsCurrentRefresh(version, token)) return;
            if (forceRefresh) _artwork.Invalidate();
            ResetArtworkLocked();
            _publicationVersion = version;
            _gameRows = rows;
            _commands = commands;
            StartArtworkLocked();
        }

        // Cache the complete snapshot before the event. A reentrant getter sees it.
        RaiseItemsChanged(commands.Length);
    }

    private static string CreateGameSubtitle(GameEntry game, bool duplicateTitle)
    {
        var parts = new List<string> { "Open with Playnite" };
        if (!string.IsNullOrWhiteSpace(game.Source)) parts.Add(game.Source);
        if (!string.IsNullOrWhiteSpace(game.Platform)) parts.Add(game.Platform);
        if (game.IsInstalled.HasValue) parts.Add(game.IsInstalled.Value ? "Installed" : "Not installed");
        if (duplicateTitle) parts.Add($"ID {game.GameId}");
        return string.Join(" · ", parts);
    }

    private bool IsCurrentRefresh(int version, CancellationToken token) =>
        !_disposed && version == _refreshVersion && !token.IsCancellationRequested;

    private bool SamePresentation(IReadOnlyList<GameEntry> games) =>
        games.Count == _gameRows.Length && games.Select((game, index) =>
            game.GameId == _gameRows[index].Game.GameId &&
            game.Title == _gameRows[index].Game.Title &&
            game.Source == _gameRows[index].Game.Source &&
            game.Platform == _gameRows[index].Game.Platform &&
            game.IsInstalled == _gameRows[index].Game.IsInstalled &&
            game.IconReference == _gameRows[index].Game.IconReference).All(equal => equal);

    private void ResetArtworkLocked()
    {
        _publicationCancellation?.Cancel();
        _publicationCancellation?.Dispose();
        _publicationCancellation = new CancellationTokenSource();
        _artworkUpdate = Task.CompletedTask;
    }

    private void StartArtworkLocked()
    {
        if (!_artworkUpdate.IsCompleted) return;
        var missing = _gameRows.Where(row => row.Game.IconReference is not null &&
            ReferenceEquals(row.Item.Icon, ArtworkIconLoader.FallbackIcon)).ToArray();
        if (missing.Length != 0)
        {
            var version = _publicationVersion;
            var token = _publicationCancellation!.Token;
            _artworkUpdate = Task.Run(() => LoadArtworkAsync(missing, version, token));
        }
    }

    private async Task LoadArtworkAsync(GameRow[] rows, int version, CancellationToken token)
    {
        try
        {
            await Parallel.ForEachAsync(rows,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token },
                async (row, cancellation) =>
                {
                    try
                    {
                        var icon = await _artwork.GetIconAsync(row.Game, cancellation).ConfigureAwait(false);
                        bool changed;
                        lock (_gate)
                        {
                            if (_disposed || version != _publicationVersion || cancellation.IsCancellationRequested) return;
                            changed = row.Item.SetIconWithoutNotification(icon ?? ArtworkIconLoader.FallbackIcon);
                        }
                        // Use the public SDK notification, but not under a provider
                        // lock: a COM event listener may re-enter on another thread.
                        if (changed) row.Item.NotifyIconChanged();
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception) { /* Artwork is optional; the command remains usable. */ }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { /* A failed image batch must not affect root commands. */ }
    }

    private async Task<CommandResult> InvokeGameAsync(int version, GameEntry displayedGame)
    {
        CancellationToken token;
        lock (_gate)
        {
            if (_disposed || version != _publicationVersion) return Unavailable();
            token = _publicationCancellation!.Token;
        }

        try
        {
            var valid = await _directLaunch.RevalidateAsync(displayedGame, token).ConfigureAwait(false);
            if (!valid)
            {
                // Supersede older pending reads as well as updating the cached root
                // catalog. A pre-revalidation timer read must not restore unsafe rows.
                await RefreshCatalogAsync(forceRefresh: true).ConfigureAwait(false);
                return Unavailable();
            }

            lock (_gate)
            {
                if (_disposed || version != _publicationVersion || token.IsCancellationRequested) return Unavailable();
                return _launch(displayedGame.GameId);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return Unavailable();
        }
        catch (Exception)
        {
            await RefreshCatalogAsync(forceRefresh: true).ConfigureAwait(false);
            return Unavailable();
        }
    }

    private static CommandResult Unavailable() => CommandResult.ShowToast(
        "This Playnite game is no longer available or couldn't be verified. Open Search games, refresh the catalog, and choose the game there.");

    public override void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_refreshVersion;
            ++_publicationVersion;
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            _publicationCancellation?.Cancel();
            _publicationCancellation?.Dispose();
            _publicationCancellation = null;
            _gameRows = [];
            _commands = [];
        }

        _refreshTimer?.Dispose();
        foreach (var item in _navigationCommands)
        {
            if (item.Command is IDisposable disposable) disposable.Dispose();
        }
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record GameRow(GameEntry Game, GameCommandItem Item);

    // Separate atomic state publication from the toolkit's synchronous COM event.
    // This is an ordinary public-SDK CommandItem; no host implementation is used.
    private sealed partial class GameCommandItem(ICommand command) : CommandItem(command)
    {
        private IIconInfo? _icon = ArtworkIconLoader.FallbackIcon;

        public override IIconInfo? Icon
        {
            get => Volatile.Read(ref _icon);
            set
            {
                if (SetIconWithoutNotification(value)) NotifyIconChanged();
            }
        }

        internal bool SetIconWithoutNotification(IIconInfo? icon) =>
            !ReferenceEquals(Interlocked.Exchange(ref _icon, icon), icon);

        internal void NotifyIconChanged() => OnPropertyChanged(nameof(Icon));
    }

    private sealed partial class RefreshCommandsCommand(PlayniteCmdPalCommandsProvider owner) : InvokableCommand
    {
        public override string Name => "Refresh game commands";

        public override CommandResult Invoke()
        {
            _ = owner.RefreshCatalogAsync(forceRefresh: true);
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class OpenGameCommand : InvokableCommand
    {
        private readonly PlayniteCmdPalCommandsProvider _owner;
        private readonly int _version;
        private readonly GameEntry _game;
        private int _invoking;

        internal OpenGameCommand(PlayniteCmdPalCommandsProvider owner, int version, GameEntry game)
        {
            _owner = owner;
            _version = version;
            _game = game;
            Id = $"playnitecmdpal.open-game.{game.GameId}";
        }

        public override string Name => "Open with Playnite";

        public override CommandResult Invoke()
        {
            if (Interlocked.CompareExchange(ref _invoking, 1, 0) != 0)
            {
                return CommandResult.ShowToast("This Playnite launch is already being verified. Wait for it to finish before trying again.");
            }
            try
            {
                return _owner.InvokeGameAsync(_version, _game).GetAwaiter().GetResult();
            }
            finally
            {
                Volatile.Write(ref _invoking, 0);
            }
        }
    }
}
