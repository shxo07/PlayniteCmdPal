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
    private readonly CatalogFilterState _filterState = new();
    private readonly FavoritesStore _favorites = FavoritesPreferences.Store;
    private readonly ArtworkIconLoader _artwork = new(artworkId => PlayniteCatalogAdapter.ResolveArtworkPath(artworkId));
    private readonly EventHandler _favoritesChangedHandler;
    private readonly SynchronizationContext? _uiContext;
    private IListItem[] _items = [CreateStatusItem("Loading your Playnite library…")];
    private CancellationTokenSource? _searchCancellation;
    private string _searchText = string.Empty;
    private int _requestVersion;
    private readonly object _requestGate = new();
    private bool _disposed;

    public PlayniteCmdPalPage()
    {
        // Capture the UI synchronization context so cross-thread notifications
        // (artwork loading, deferred favorites refresh) can be marshaled back
        // to the UI thread where the Command Palette host processes them.
        _uiContext = SynchronizationContext.Current;

        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Playnite";
        Name = "Search games";

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

    private void BeginSearch(bool forceRefresh = false, string? searchText = null)
    {
        int version;
        string query;
        CancellationToken cancellationToken;
        int count;

        lock (_requestGate)
        {
            if (_disposed)
                return;

            if (searchText is not null)
            {
                _searchText = searchText;
            }

            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = new CancellationTokenSource();

            version = ++_requestVersion;
            query = _searchText;
            cancellationToken = _searchCancellation.Token;

            _items = [CreateStatusItem("Searching your Playnite library…")];
            count = _items.Length;
        }

        // Notify outside the request lock: the host may synchronously re-enter GetItems.
        RaiseItemsChangedOnUIContext(count);
        _ = LoadItemsAsync(version, query, forceRefresh, cancellationToken);
    }

    private async Task LoadItemsAsync(
        int version,
        string query,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        int count = 0;
        var failureStage = "catalog read";
        try
        {
            var catalog = forceRefresh
                ? await _catalog.RefreshAsync(cancellationToken)
                : await _catalog.GetAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Do not erase the user's source/platform selections when the catalog is
            // temporarily unavailable. A valid snapshot (including last-known-good data)
            // is the only source allowed to update the available filter options.
            if (catalog.IsAvailable)
            {
                _filterState.UpdateCatalog(catalog.Games);
            }

            failureStage = "filtering and result construction";

            // A normal search/pin refresh reuses validated artwork. Only an explicit
            // catalog refresh should invalidate artwork references and force disk resolution.
            if (forceRefresh)
            {
                _artwork.Invalidate();
            }

            var built = BuildItems(catalog, query);
            lock (_requestGate)
            {
                if (version != _requestVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

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
        catch (Exception exception)
        {
            lock (_requestGate)
            {
                if (version != _requestVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // The former generic message blamed the bridge for failures that could
                // actually occur after a successful read, during filtering or UI result
                // construction. Surface the phase and exception so failures are actionable.
                var detail = string.IsNullOrWhiteSpace(exception.Message)
                    ? "No additional details were provided."
                    : exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (detail.Length > 180)
                {
                    detail = detail[..180] + "…";
                }

                _items =
                [
                    CreateAdvancedFiltersItem(),
                    CreateStatusItem($"Playnite {failureStage} failed ({exception.GetType().Name}): {detail}"),
                ];
            }

            count = _items.Length;
            RaiseItemsChangedOnUIContext(count);
        }
    }

    private (IListItem[] Items, IReadOnlyList<(ListItem Item, GameEntry Game)> ArtworkItems) BuildItems(CatalogReadResult catalog, string query)
    {
        var items = new List<IListItem> { CreateAdvancedFiltersItem() };

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
            items.Add(CreateRefreshItem("Your Playnite library is empty. Add games in Playnite, then refresh."));
            return (items.ToArray(), Array.Empty<(ListItem Item, GameEntry Game)>());
        }

        var activeFilters = _filterState.ToSearchFilters();
        var matches = GameSearch.Search(catalog.Games, query, activeFilters);
        if (matches.Count == 0)
        {
            var hasActiveFilters = activeFilters.IsInstalled.HasValue
                || activeFilters.Sources?.Count > 0
                || activeFilters.Platforms?.Count > 0;
            var message = hasActiveFilters
                ? string.IsNullOrWhiteSpace(query)
                    ? "No games match the selected filters. Clear one or more filters to see more games."
                    : $"No games match “{query.Trim()}” with the selected filters. Clear a filter or change your search."
                : string.IsNullOrWhiteSpace(query)
                    ? "No games are available in the current catalog."
                    : $"No games match “{query.Trim()}”.";
            items.Add(CreateStatusItem(message));
            return (items.ToArray(), Array.Empty<(ListItem Item, GameEntry Game)>());
        }

        var duplicateIds = matches
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

            if (game.IsInstalled.HasValue)
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

    private ListItem CreateAdvancedFiltersItem()
    {
        var count = _filterState.ActiveFilterCount;
        return new ListItem(new AdvancedFiltersPage(_filterState, () => BeginSearch()))
        {
            Title = count == 0 ? "Advanced filters" : $"Advanced filters ({count})",
            Subtitle = count == 0 ? "Installed status, source, and platform" : "Filters are active · Select to edit",
            Icon = new IconInfo("\uE71C"),
        };
    }

    private static ListItem CreateStatusItem(string message) =>
        new ListItem(new NoOpCommand()) { Title = message };

    private ListItem CreateRefreshItem(string message) =>
        new ListItem(new RefreshCatalogCommand(this)) { Title = message, Subtitle = "Press Enter to retry" };

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
