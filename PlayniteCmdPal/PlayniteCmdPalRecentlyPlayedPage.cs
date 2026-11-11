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

internal sealed partial class PlayniteCmdPalRecentlyPlayedPage : DynamicListPage, IDisposable
{
    private readonly PlayniteCatalogAdapter _catalog = new();
    private readonly ArtworkIconLoader _artwork = new(artworkId => PlayniteCatalogAdapter.ResolveArtworkPath(artworkId));
    private readonly SynchronizationContext? _uiContext;
    private IListItem[] _statusItems = Array.Empty<IListItem>();
    private IListItem[] _gameItems = Array.Empty<IListItem>();
    private IListItem[] _items = Array.Empty<IListItem>();
    private string _searchText = string.Empty;
    private readonly Timer _refreshTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private int _loadVersion;
    private int _disposed;

    public PlayniteCmdPalRecentlyPlayedPage()
    {
        _uiContext = SynchronizationContext.Current;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Playnite";
        Name = "Recently played games";
        _statusItems = [CreateStatusItem("Loading recently played games…")];
        _items = _statusItems;
        _lifetimeToken = _lifetime.Token;

        // Poll the adapter cache while the page is open. GetAsync observes the adapter's
        // 30-second disk revalidation interval, while still refreshing relative labels.
        _refreshTimer = new Timer(
            _ => RefreshView(),
            null,
            dueTime: TimeSpan.FromSeconds(10),
            period: TimeSpan.FromSeconds(10));
        _ = LoadItemsAsync();
    }

    public override IListItem[] GetItems() => _items;

    internal void RefreshCatalog()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _ = LoadItemsAsync(forceRefresh: true);
    }

    private void RefreshView()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _ = LoadItemsAsync();
    }

    internal IContextItem CreateRefreshContextItem() =>
        new CommandContextItem(new RefreshCatalogCommand(this));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _refreshTimer.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _searchText = newSearch;
        ApplySearch();
    }

    private void ApplySearch()
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        _items = RecentlyPlayedGames.ComposeVisibleItems(
            _statusItems,
            _gameItems,
            _searchText,
            item => item.Title).ToArray();
        RaiseItemsChangedOnUIContext();
    }

    private void RaiseItemsChangedOnUIContext()
    {
        void Raise()
        {
            if (Volatile.Read(ref _disposed) == 0)
                RaiseItemsChanged();
        }

        if (_uiContext is { } context &&
            !ReferenceEquals(context, SynchronizationContext.Current))
        {
            context.Post(_ => Raise(), null);
        }
        else
        {
            Raise();
        }
    }

    private bool IsCurrentLoad(int version) =>
        Volatile.Read(ref _disposed) == 0 &&
        !_lifetimeToken.IsCancellationRequested &&
        version == Volatile.Read(ref _loadVersion);

    private async Task LoadItemsAsync(bool forceRefresh = false)
    {
        if (_lifetimeToken.IsCancellationRequested) return;
        var version = Interlocked.Increment(ref _loadVersion);

        try
        {
            var catalog = forceRefresh
                ? await _catalog.RefreshAsync(_lifetimeToken).ConfigureAwait(false)
                : await _catalog.GetAsync(_lifetimeToken).ConfigureAwait(false);
            if (!IsCurrentLoad(version)) return;
            if (forceRefresh)
                _artwork.Invalidate();
            var statusItems = new List<IListItem>();

            if (!catalog.IsAvailable)
            {
                statusItems.Add(CreateStatusItem(catalog.Issue?.Message
                    ?? "The Playnite catalog is unavailable. Install and start the companion plugin."));
                if (!IsCurrentLoad(version)) return;
                _statusItems = statusItems.ToArray();
                _gameItems = Array.Empty<IListItem>();
                ApplySearch();
                return;
            }

            // Status messages are independent of the active game-name query.
            if (catalog.IsStale)
                statusItems.Add(CreateStatusItem("The Playnite catalog is over 24 hours old."));
            if (catalog.Issue is not null)
                statusItems.Add(CreateStatusItem("The latest catalog refresh failed. Showing the last valid snapshot."));
            if (catalog.SkippedRecordCount > 0)
                statusItems.Add(CreateStatusItem(
                    $"The catalog skipped {catalog.SkippedRecordCount} invalid game record(s). Results may be incomplete."));

            var games = RecentlyPlayedGames.Sort(catalog.Games);
            if (games.Count == 0)
            {
                switch (RecentlyPlayedGames.GetEmptyState(catalog.Games.Count, catalog.SkippedRecordCount))
                {
                    case RecentlyPlayedEmptyState.NoUsableRecords:
                        statusItems.Add(CreateStatusItem(
                            "No usable game records were found. Check the companion plugin and refresh the catalog."));
                        break;
                    case RecentlyPlayedEmptyState.EmptyLibrary:
                        statusItems.Add(CreateStatusItem(
                            "Your Playnite library is empty. Add games in Playnite."));
                        break;
                    default:
                        statusItems.Add(CreateStatusItem("No games have a recorded last-played date yet."));
                        break;
                }

                if (!IsCurrentLoad(version)) return;
                _statusItems = statusItems.ToArray();
                _gameItems = Array.Empty<IListItem>();
                ApplySearch();
                return;
            }

            var duplicateTitles = games
                .GroupBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var gameItems = new List<IListItem>();
            var artworkItems = new List<(ListItem Item, GameEntry Game)>();

            foreach (var game in games)
            {
                var lastActivity = game.LastActivity!.Value;
                var relativeTime = PlayniteCmdPal.Core.RecentlyPlayedGames.FormatRelativeLastPlayed(lastActivity, DateTimeOffset.UtcNow);
                var metadata = new List<string>();
                if (!string.IsNullOrWhiteSpace(game.Source)) metadata.Add(game.Source);
                if (!string.IsNullOrWhiteSpace(game.Platform)) metadata.Add(game.Platform);
                if (duplicateTitles.Contains(game.Title))
                {
                    var discriminator = game.GameId.Length > 8 ? game.GameId[..8] : game.GameId;
                    metadata.Add($"ID {discriminator}");
                }

                var item = new ListItem(new LaunchGameCommand(game.GameId))
                {
                    Title = game.Title,
                    Subtitle = string.Join(" · ", metadata),
                    Icon = ArtworkIconLoader.FallbackIcon,
                    Tags = [new Tag(relativeTime)],
                };
                gameItems.Add(item);
                artworkItems.Add((item, game));
            }

            if (!IsCurrentLoad(version)) return;
            _statusItems = statusItems.ToArray();
            _gameItems = gameItems.ToArray();
            ApplySearch();
            _ = LoadArtworkAsync(version, artworkItems);
        }
        catch (OperationCanceledException)
        {
            // The page can be disposed while the catalog read is in flight.
        }
        catch (Exception)
        {
            if (!IsCurrentLoad(version)) return;
            _statusItems =
            [
                CreateStatusItem("Couldn't read the Playnite catalog. Check the companion plugin and snapshot permissions."),
            ];
            _gameItems = Array.Empty<IListItem>();
            ApplySearch();
        }
    }

    private async Task LoadArtworkAsync(
        int version,
        IReadOnlyList<(ListItem Item, GameEntry Game)> artworkItems)
    {
        if (artworkItems.Count == 0) return;

        try
        {
            await Parallel.ForEachAsync(
                artworkItems,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = _lifetimeToken,
                },
                async (entry, token) =>
                {
                    try
                    {
                        var icon = await _artwork.GetIconAsync(entry.Game, token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        if (!IsCurrentLoad(version)) return;

                        entry.Item.Icon = icon ?? ArtworkIconLoader.FallbackIcon;
                        RaiseItemsChangedOnUIContext();
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        // Artwork is optional; a failure for one game must not affect other rows.
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            // The page was disposed while artwork was loading.
        }
    }

    private static ListItem CreateStatusItem(string message) =>
        new ListItem(new NoOpCommand())
        {
            Title = message,
        };

    private sealed partial class RefreshCatalogCommand : InvokableCommand
    {
        private readonly PlayniteCmdPalRecentlyPlayedPage _page;

        public RefreshCatalogCommand(PlayniteCmdPalRecentlyPlayedPage page) => _page = page;

        public override string Name => "Refresh Playnite catalog";

        public override CommandResult Invoke()
        {
            _page.RefreshCatalog();
            return CommandResult.KeepOpen();
        }
    }
}
