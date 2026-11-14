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

internal sealed partial class PlayniteCmdPalPage : DynamicListPage, IDisposable
{
    private readonly PlayniteCatalogAdapter _catalog = new();
    private readonly FavoritesStore _favorites = FavoritesPreferences.Store;
    private readonly ArtworkIconLoader _artwork = new(artworkId => PlayniteCatalogAdapter.ResolveArtworkPath(artworkId));
    private readonly CatalogFilterState _filterState = new();
    private readonly EventHandler _favoritesChangedHandler;
    private readonly SynchronizationContext? _uiContext;
    private readonly FiltersPage _filtersPage;
    private IListItem[] _items = [CreateStatusItem("Loading your Playnite library…")];
    private CancellationTokenSource? _searchCancellation;
    private readonly PlayniteLibraryNavigationState _navigation = new();
    private readonly System.Timers.Timer _recentRefreshTimer;
    private int _requestVersion;
    private readonly object _requestGate = new();
    private static readonly object PreferencesSaveGate = new();
    private static long _preferencesWriteVersion;
    private bool _disposed;

    public PlayniteCmdPalPage()
    {
        // Capture the UI synchronization context so cross-thread notifications
        // (artwork loading, deferred favorites refresh) can be marshaled back
        // to the UI thread where the Command Palette host processes them.
        _uiContext = SynchronizationContext.Current;
        _filterState.RestoreSelections(CatalogFilterPreferences.Load());
        _filterState.FilterSelectionsChanged += OnFilterSelectionsChanged;
        _filtersPage = new FiltersPage(_filterState, () => BeginSearch());

        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Playnite";
        Name = "Playnite";

        // Keep Search Games in sync when a game is pinned or unpinned from any page.
        // Capture this page weakly so the shared store cannot retain retired pages.
        var weakSelf = new WeakReference<PlayniteCmdPalPage>(this);
        EventHandler weakHandler = null!;
        weakHandler = (sender, args) =>
        {
            if (weakSelf.TryGetTarget(out var page))
            {
                page.OnFavoritesChanged(sender, args);
            }
            else if (sender is FavoritesStore store)
            {
                store.FavoritesChanged -= weakHandler;
            }
        };
        _favoritesChangedHandler = weakHandler;
        _favorites.FavoritesChanged += _favoritesChangedHandler;

        _recentRefreshTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds)
        {
            AutoReset = true,
        };
        _recentRefreshTimer.Elapsed += (_, _) => RefreshRecentlyPlayedView();
        _recentRefreshTimer.Start();
        BeginSearch();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        BeginSearch(searchText: newSearch ?? string.Empty);
    }

    public override IListItem[] GetItems()
    {
        lock (_requestGate) return _items;
    }

    /// <summary>
    /// Raises the items-changed notification on the UI synchronization context when one was
    /// captured and we are not already on it. Falls back to a direct call when already on the
    /// UI context or when no context was captured. This ensures the Command Palette host
    /// processes notifications regardless of which thread calls them.
    /// </summary>
    private void RaiseItemsChangedOnUIContext(int count)
    {
        void raise() => RaiseItemsChanged(count);
        if (_uiContext is { } context &&
            !ReferenceEquals(context, SynchronizationContext.Current))
        {
            context.Post(_ => raise(), null);
        }
        else
        {
            raise();
        }
    }

    internal void RefreshCatalog()
    {
        BeginSearch(forceRefresh: true);
    }

    internal IContextItem CreateRefreshContextItem() =>
        new CommandContextItem(new RefreshCatalogCommand(this));

    private void SetViewMode(PlayniteLibraryViewMode mode)
    {
        lock (_requestGate)
        {
            if (_disposed || !_navigation.SwitchView(mode))
                return;
        }

        BeginSearch();
    }

    private void RefreshRecentlyPlayedView()
    {
        if (Volatile.Read(ref _disposed))
            return;

        lock (_requestGate)
        {
            if (_navigation.Capture().ViewMode != PlayniteLibraryViewMode.RecentlyPlayed)
                return;
        }

        // Keep the populated list visible while polling; the previous Recently Played
        // page refreshed in the background without replacing results with a loading row.
        BeginSearch(preserveItems: true);
    }

    private void BeginSearch(
        bool forceRefresh = false,
        string? searchText = null,
        bool preserveItems = false)
    {
        int version;
        string query;
        PlayniteLibraryViewMode mode;
        CancellationToken cancellationToken;
        int count = 0;

        lock (_requestGate)
        {
            if (_disposed)
                return;

            if (searchText is not null)
            {
                _navigation.SetSearchText(searchText);
            }

            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = new CancellationTokenSource();

            version = ++_requestVersion;
            var navigation = _navigation.Capture();
            query = navigation.SearchText;
            mode = navigation.ViewMode;
            cancellationToken = _searchCancellation.Token;

            if (!preserveItems)
            {
                var loadingItems = CreateViewItems(mode);
                loadingItems.Add(CreateStatusItem("Searching your Playnite library…"));
                _items = loadingItems.ToArray();
                count = _items.Length;
            }
        }

        // Notify outside the request lock: the host may synchronously re-enter GetItems.
        // A timer-driven refresh deliberately leaves the current list untouched until
        // the refreshed snapshot is ready, avoiding a periodic loading-state flicker.
        if (!preserveItems)
        {
            RaiseItemsChangedOnUIContext(count);
        }
        _ = LoadItemsAsync(version, query, mode, forceRefresh, cancellationToken);
    }

    private async Task LoadItemsAsync(
        int version,
        string query,
        PlayniteLibraryViewMode mode,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        int count = 0;
        try
        {
            var catalog = forceRefresh
                ? await _catalog.RefreshAsync(cancellationToken)
                : await _catalog.GetAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // A normal search/pin refresh reuses validated artwork. Only an explicit
            // catalog refresh should invalidate artwork references and force disk resolution.
            if (forceRefresh)
            {
                _artwork.Invalidate();
            }

            // Reading favorites takes a cross-process file lock. Keep that I/O outside
            // the page request lock so GetItems() and view changes are never blocked by it.
            var favoriteIds = mode == PlayniteLibraryViewMode.Favorites
                ? _favorites.GetFavoriteIds()
                : Array.Empty<string>();
            cancellationToken.ThrowIfCancellationRequested();

            (IListItem[] Items, IReadOnlyList<(ListItem Item, GameEntry Game)> ArtworkItems) built;
            lock (_requestGate)
            {
                if (version != _requestVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Do not reinterpret a transient unavailable catalog as an empty snapshot:
                // that would discard valid source/platform choices while the picker is open.
                if (catalog.IsAvailable)
                {
                    _filterState.UpdateCatalog(catalog.Games);
                }

                built = BuildItems(catalog, query, mode, favoriteIds);
                _items = built.Items;
                count = _items.Length;
            }

            RaiseItemsChangedOnUIContext(count);
            _ = LoadArtworkAsync(version, built.ArtworkItems, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer query or explicit refresh superseded this request.
        }
        catch (Exception)
        {
            lock (_requestGate)
            {
                if (version != _requestVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var failureItems = CreateViewItems(mode);
                failureItems.Add(CreateStatusItem(mode == PlayniteLibraryViewMode.Favorites
                    ? "Couldn't load Favorites. Check the local preferences file and Playnite catalog, then use Refresh Playnite catalog."
                    : "Couldn't read the Playnite catalog. Check the companion plugin and snapshot permissions, then use Refresh Playnite catalog."));
                _items = failureItems.ToArray();
            }

            count = _items.Length;
            RaiseItemsChangedOnUIContext(count);
        }
    }

    private (IListItem[] Items, IReadOnlyList<(ListItem Item, GameEntry Game)> ArtworkItems) BuildItems(
        CatalogReadResult catalog,
        string query,
        PlayniteLibraryViewMode mode,
        IReadOnlyCollection<string> favoriteIds)
    {
        var items = CreateViewItems(mode);
        if (!catalog.IsAvailable)
        {
            var message = catalog.Issue?.Message
                ?? "The Playnite catalog is unavailable. Install and start the companion plugin, then refresh.";
            items.Add(CreateRefreshItem(message));
            return (items.ToArray(), Array.Empty<(ListItem Item, GameEntry Game)>());
        }
        if (catalog.IsStale)
        {
            items.Add(CreateStatusItem("The Playnite catalog is over 24 hours old. Open Playnite to refresh it."));
        }

        if (catalog.Issue is not null)
        {
            items.Add(CreateStatusItem("The latest catalog refresh failed. Showing the last valid snapshot; check the companion plugin and refresh."));
        }

        if (catalog.SkippedRecordCount > 0)
        {
            items.Add(CreateStatusItem($"{catalog.SkippedRecordCount} invalid catalog entr{(catalog.SkippedRecordCount == 1 ? "y was" : "ies were")} skipped."));
        }

        if (catalog.Games.Count == 0)
        {
            var emptyMessage = mode switch
            {
                PlayniteLibraryViewMode.Favorites => favoriteIds.Count == 0
                    ? "No favorites yet. Pin a game from its search result actions."
                    : "No saved favorites match this search in the current Playnite library.",
                PlayniteLibraryViewMode.RecentlyPlayed => RecentlyPlayedGames.GetEmptyState(
                    catalog.Games.Count,
                    catalog.SkippedRecordCount) == RecentlyPlayedEmptyState.NoUsableRecords
                    ? "No usable game records were found. Check the companion plugin and refresh the catalog."
                    : "Your Playnite library is empty. Add games in Playnite.",
                _ => "Your Playnite library is empty. Add games in Playnite, then use Refresh Playnite catalog.",
            };
            items.Add(CreateStatusItem(emptyMessage));
            return (items.ToArray(), Array.Empty<(ListItem Item, GameEntry Game)>());
        }
        var matches = PlayniteLibraryViews.Search(
            catalog.Games,
            mode,
            query,
            favoriteIds,
            _filterState.ToSearchFilters());

        if (matches.Count == 0)
        {
            string message;
            var hasActiveFilters = _filterState.ActiveFilterCount > 0;
            if (mode == PlayniteLibraryViewMode.Favorites)
            {
                message = favoriteIds.Count == 0
                    ? "No favorites yet. Pin a game from its search result actions."
                    : hasActiveFilters
                        ? string.IsNullOrWhiteSpace(query)
                            ? "No favorites match the selected filters. Clear one or more filters to see more games."
                            : $"No favorites match “{query.Trim()}” with the selected filters."
                        : "No saved favorites match this search in the current Playnite library.";
            }
            else if (mode == PlayniteLibraryViewMode.RecentlyPlayed)
            {
                var recentEmptyState = RecentlyPlayedGames.GetEmptyState(catalog.Games.Count, catalog.SkippedRecordCount);
                var hasRecentGames = RecentlyPlayedGames.Sort(catalog.Games).Count > 0;
                message = recentEmptyState switch
                {
                    RecentlyPlayedEmptyState.EmptyLibrary => "Your Playnite library is empty. Add games in Playnite.",
                    RecentlyPlayedEmptyState.NoUsableRecords => "No usable game records were found. Check the companion plugin and refresh the catalog.",
                    _ when !hasRecentGames => "No games have a recorded last-played date yet.",
                    _ when hasActiveFilters && string.IsNullOrWhiteSpace(query) => "No recently played games match the selected filters. Clear one or more filters to see more games.",
                    _ when hasActiveFilters => $"No recently played games match “{query.Trim()}” with the selected filters.",
                    _ when string.IsNullOrWhiteSpace(query) => "No recently played games match the current view.",
                    _ => $"No recently played games match “{query.Trim()}”.",
                };
            }
            else
            {
                message = hasActiveFilters
                    ? string.IsNullOrWhiteSpace(query)
                        ? "No games match the selected filters. Clear one or more filters to see more games."
                        : $"No games match “{query.Trim()}” with the selected filters. Clear a filter or change your search."
                    : string.IsNullOrWhiteSpace(query)
                        ? "No games are available in the current catalog."
                        : $"No games match “{query.Trim()}”.";
            }

            items.Add(CreateStatusItem(message));
            return (items.ToArray(), Array.Empty<(ListItem Item, GameEntry Game)>());
        }

        var duplicateTitleGames = mode == PlayniteLibraryViewMode.RecentlyPlayed
            ? RecentlyPlayedGames.Sort(catalog.Games)
            : matches;
        var duplicateIds = duplicateTitleGames
            .GroupBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(game => game.GameId))
            .ToHashSet(StringComparer.Ordinal);

        var artworkItems = new List<(ListItem Item, GameEntry Game)>();
        foreach (var game in matches)
        {
            var metadata = new List<string>();
            if (!string.IsNullOrWhiteSpace(game.Source))
            {
                metadata.Add(game.Source);
            }

            if (!string.IsNullOrWhiteSpace(game.Platform))
            {
                metadata.Add(game.Platform);
            }

            // Recently Played historically shows source/platform and duplicate-title
            // identity only; keep installation status exclusive to Search and Favorites.
            if (mode != PlayniteLibraryViewMode.RecentlyPlayed && game.IsInstalled.HasValue)
            {
                metadata.Add(game.IsInstalled.Value ? "Installed" : "Not installed");
            }

            if (duplicateIds.Contains(game.GameId))
            {
                metadata.Add($"ID {game.GameId[..8]}");
            }

            var item = new ListItem(new LaunchGameCommand(game.GameId))
            {
                Title = game.Title,
                Subtitle = string.Join(" · ", metadata),
                Icon = ArtworkIconLoader.FallbackIcon,
                Tags = mode == PlayniteLibraryViewMode.RecentlyPlayed && game.LastActivity.HasValue
                    ? [new Tag(RecentlyPlayedGames.FormatRelativeLastPlayed(game.LastActivity.Value, DateTimeOffset.UtcNow))]
                    : [],
                MoreCommands =
                [
                    new CommandContextItem(new ShowGameDetailsCommand(game.GameId)),
                    new CommandContextItem(new ToggleFavoriteCommand(_favorites, game.GameId)),
                ],
            };

            items.Add(item);
            artworkItems.Add((item, game));
        }

        return (items.ToArray(), artworkItems);
    }

    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        // Defer the refresh until after ToggleFavoriteCommand.Invoke() returns and the
        // toast is shown. await Task.Delay(50) without ConfigureAwait(false) resumes the
        // continuation on the captured UI synchronization context so RaiseItemsChanged is
        // processed by the Command Palette host. Never call BeginSearch() synchronously
        // from within the event handler, as it runs inside ToggleFavoriteCommand.Invoke()
        // and would nest host notifications inside command dispatch, suppressing the toast.
        _ = RefreshAfterFavoriteChangeAsync();
    }

    private async Task RefreshAfterFavoriteChangeAsync()
    {
        await Task.Delay(50);
        BeginSearch();
    }

    private async Task LoadArtworkAsync(
        int version,
        IReadOnlyList<(ListItem Item, GameEntry Game)> artworkItems,
        CancellationToken cancellationToken)
    {
        if (artworkItems.Count == 0) return;

        try
        {
            await Parallel.ForEachAsync(
                artworkItems,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
                async (entry, token) =>
                {
                    try
                    {
                        var icon = await _artwork.GetIconAsync(entry.Game, token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();

                        var updated = false;
                        int itemsCount = 0;
                        lock (_requestGate)
                        {
                            // Do not mutate objects belonging to an obsolete search request.
                            if (version != _requestVersion || token.IsCancellationRequested)
                            {
                                return;
                            }

                            entry.Item.Icon = icon ?? ArtworkIconLoader.FallbackIcon;
                            updated = true;
                            itemsCount = _items.Length;
                        }

                        if (updated)
                        {
                            RaiseItemsChangedOnUIContext(itemsCount);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        // A failure for one row must not prevent artwork for other rows.
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer query replaced this result set.
        }
    }

    private List<IListItem> CreateViewItems(PlayniteLibraryViewMode mode)
    {
        // Keep the view selector order stable. The extension SDK does not expose
        // the host ListView's SelectedItem/SelectedIndex, so the semantic active view
        // cannot drive the native keyboard-selection accent from this page.
        return
        [
            CreatePlayniteLibraryViewModeItem(PlayniteLibraryViewMode.Search),
            CreatePlayniteLibraryViewModeItem(PlayniteLibraryViewMode.RecentlyPlayed),
            CreatePlayniteLibraryViewModeItem(PlayniteLibraryViewMode.Favorites),
            CreateFiltersItem(),
        ];
    }

    private ListItem CreatePlayniteLibraryViewModeItem(PlayniteLibraryViewMode mode)
    {
        var title = mode switch
        {
            PlayniteLibraryViewMode.Search => "Search games",
            PlayniteLibraryViewMode.RecentlyPlayed => "Recently played",
            PlayniteLibraryViewMode.Favorites => "Favorites",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        var icon = mode switch
        {
            PlayniteLibraryViewMode.Search => new IconInfo("\uE721"),
            PlayniteLibraryViewMode.RecentlyPlayed => new IconInfo("\uE823"),
            PlayniteLibraryViewMode.Favorites => new IconInfo("\uE734"),
            _ => ArtworkIconLoader.FallbackIcon,
        };

        var isActive = _navigation.Capture().ViewMode == mode;
        return new ListItem(new SwitchViewCommand(this, mode))
        {
            Title = title,
            Subtitle = isActive ? "Current view" : string.Empty,
            Icon = icon,
        };
    }

    private void OnFilterSelectionsChanged(object? sender, EventArgs e)
    {
        // Catalog metadata can prune stale selections while LoadItemsAsync holds the page
        // request gate. Never perform filesystem I/O synchronously from this event: capture
        // an immutable snapshot and serialize only the newest pending write on the thread pool.
        var selection = _filterState.CaptureSelections();
        var version = Interlocked.Increment(ref _preferencesWriteVersion);
        _ = Task.Run(() =>
        {
            lock (PreferencesSaveGate)
            {
                if (version == Volatile.Read(ref _preferencesWriteVersion))
                    CatalogFilterPreferences.Save(selection);
            }
        });
    }

    private ListItem CreateFiltersItem()
    {
        var count = _filterState.ActiveFilterCount;
        return new ListItem(_filtersPage)
        {
            Title = count == 0 ? "Filters" : $"Filters ({count})",
            Subtitle = count == 0 ? "Installed status · source · platform" : "Active · Select to edit",
            Icon = new IconInfo("\uE71C"),
        };
    }

    private static ListItem CreateStatusItem(string message) =>
        new ListItem(new NoOpCommand()) { Title = message };

    private static ListItem CreateRefreshItem(string message) =>
        CreateStatusItem(message + " Use the page-level Refresh Playnite catalog action to retry.");

    public void Dispose()
    {
        lock (_requestGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _requestVersion++;
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
        }

        _recentRefreshTimer.Dispose();
        _filtersPage.Dispose();
        _filterState.FilterSelectionsChanged -= OnFilterSelectionsChanged;
    }

    private sealed partial class SwitchViewCommand : InvokableCommand
    {
        private readonly PlayniteCmdPalPage _page;
        private readonly PlayniteLibraryViewMode _mode;

        public SwitchViewCommand(PlayniteCmdPalPage page, PlayniteLibraryViewMode mode)
        {
            _page = page;
            _mode = mode;
        }

        public override string Name => _mode switch
        {
            PlayniteLibraryViewMode.Search => "Show search games",
            PlayniteLibraryViewMode.RecentlyPlayed => "Show recently played games",
            PlayniteLibraryViewMode.Favorites => "Show favorites",
            _ => "Switch Playnite view",
        };

        public override CommandResult Invoke()
        {
            _page.SetViewMode(_mode);
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class RefreshCatalogCommand : InvokableCommand
    {
        private readonly PlayniteCmdPalPage _page;

        public RefreshCatalogCommand(PlayniteCmdPalPage page)
        {
            _page = page;
        }

        public override string Name => "Refresh Playnite catalog";

        public override CommandResult Invoke()
        {
            _page.RefreshCatalog();
            return CommandResult.KeepOpen();
        }
    }
}
