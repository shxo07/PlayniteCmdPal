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

internal sealed partial class PlayniteCmdPalPage : DynamicListPage
{
    private readonly IReadOnlyPlayniteCatalog _catalog = new PlayniteCatalogAdapter();
    private readonly CatalogFilterState _filterState = new();
    private readonly FavoritesStore _favorites = FavoritesPreferences.Store;
    private IListItem[] _items = [CreateStatusItem("Loading your Playnite library…")];
    private CancellationTokenSource? _searchCancellation;
    private string _searchText = string.Empty;
    private int _requestVersion;
    private readonly object _requestGate = new();

    public PlayniteCmdPalPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Playnite";
        Name = "Search games";
        BeginSearch();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        BeginSearch(searchText: newSearch ?? string.Empty);
    }

    public override IListItem[] GetItems() => _items;

    internal void RefreshCatalog()
    {
        BeginSearch(forceRefresh: true);
    }

    private void BeginSearch(bool forceRefresh = false, string? searchText = null)
    {
        int version;
        string query;
        CancellationToken cancellationToken;

        lock (_requestGate)
        {
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
            RaiseItemsChanged();
        }

        _ = LoadItemsAsync(version, query, forceRefresh, cancellationToken);
    }

    private async Task LoadItemsAsync(
        int version,
        string query,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var failureStage = "catalog read";
        try
        {
            var catalog = forceRefresh
                ? await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false)
                : await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);

            failureStage = "filtering and result construction";
            lock (_requestGate)
            {
                if (version != _requestVersion)
                {
                    return;
                }

                _filterState.UpdateCatalog(catalog.Games);
                _items = BuildItems(catalog, query);
                RaiseItemsChanged();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer query or explicit refresh superseded this request.
        }
        catch (Exception exception)
        {
            lock (_requestGate)
            {
                if (version != _requestVersion)
                {
                    return;
                }

                // Keep the failure stage and exception type visible. The old generic message
                // incorrectly blamed the bridge even when the snapshot had loaded and a later
                // filtering/result/UI operation had failed.
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
                RaiseItemsChanged();
            }
        }
    }

    private IListItem[] BuildItems(CatalogReadResult catalog, string query)
    {
        var items = new List<IListItem> { CreateAdvancedFiltersItem() };

        if (!catalog.IsAvailable)
        {
            items.Add(CreateRefreshItem(catalog.Issue?.Message
                ?? "The Playnite catalog is unavailable. Install and start the companion plugin, then refresh."));
            return items.ToArray();
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
            return items.ToArray();
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
            return items.ToArray();
        }

        var duplicateIds = matches
            .GroupBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(game => game.GameId))
            .ToHashSet(StringComparer.Ordinal);

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

            items.Add(new ListItem(new LaunchGameCommand(game.GameId))
            {
                Title = game.Title,
                Subtitle = string.Join(" · ", metadata),
                MoreCommands =
                [
                    new CommandContextItem(new ShowGameDetailsCommand(game.GameId)),
                    new CommandContextItem(new ToggleFavoriteCommand(_favorites, game.GameId, () => BeginSearch())),
                ],
            });
        }

        return items.ToArray();
    }

    private IListItem CreateAdvancedFiltersItem()
    {
        var count = _filterState.ActiveFilterCount;
        return new ListItem(new AdvancedFiltersPage(_filterState, () => BeginSearch()))
        {
            Title = count == 0 ? "Advanced filters" : $"Advanced filters ({count})",
            Subtitle = count == 0 ? "Installed status, source, and platform" : "Filters are active · Select to edit",
            Icon = new IconInfo("\uE71C"),
        };
    }

    private static IListItem CreateStatusItem(string message) =>
        new ListItem(new NoOpCommand()) { Title = message };

    private IListItem CreateRefreshItem(string message) =>
        new ListItem(new RefreshCatalogCommand(this)) { Title = message, Subtitle = "Press Enter to retry" };

    private sealed class RefreshCatalogCommand : InvokableCommand
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
