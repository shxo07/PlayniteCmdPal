// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See third-party/Microsoft-PowerToys-LICENSE.txt for the license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

internal sealed partial class PlayniteCmdPalRecentlyPlayedPage : DynamicListPage
{
    private readonly IReadOnlyPlayniteCatalog _catalog = new PlayniteCatalogAdapter();
    private IListItem[] _statusItems = [CreateStatusItem("Loading recently played games…")];
    private IListItem[] _gameItems = Array.Empty<IListItem>();
    private IListItem[] _items = [CreateStatusItem("Loading recently played games…")];
    private string _searchText = string.Empty;

    public PlayniteCmdPalRecentlyPlayedPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Playnite";
        Name = "Recently played games";
        _ = LoadItemsAsync();
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _searchText = newSearch;
        ApplySearch();
    }

    private void ApplySearch()
    {
        _items = RecentlyPlayedGames.ComposeVisibleItems(
            _statusItems,
            _gameItems,
            _searchText,
            item => item.Title).ToArray();
        RaiseItemsChanged();
    }

    private async Task LoadItemsAsync(bool forceRefresh = false)
    {
        try
        {
            var catalog = forceRefresh
                ? await _catalog.RefreshAsync().ConfigureAwait(false)
                : await _catalog.GetAsync().ConfigureAwait(false);
            var statusItems = new List<IListItem>();

            if (!catalog.IsAvailable)
            {
                statusItems.Add(CreateRefreshItem(catalog.Issue?.Message
                    ?? "The Playnite catalog is unavailable. Install and start the companion plugin, then refresh."));
                _statusItems = statusItems.ToArray();
                _gameItems = Array.Empty<IListItem>();
                ApplySearch();
                return;
            }

            // Status and recovery actions are independent of the active game-name query.
            statusItems.Add(CreateRefreshItem("Refresh recently played games"));
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
                        statusItems.Add(CreateRefreshItem(
                            "Your Playnite library is empty. Add games in Playnite, then refresh."));
                        break;
                    default:
                        statusItems.Add(CreateRefreshItem("No games have a recorded last-played date yet."));
                        break;
                }

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

            foreach (var game in games)
            {
                var metadata = new List<string>
                {
                    $"Last played {RecentlyPlayedGames.FormatLastPlayed(game.LastActivity!.Value, TimeZoneInfo.Local)} (local time)",
                };
                if (!string.IsNullOrWhiteSpace(game.Source)) metadata.Add(game.Source);
                if (!string.IsNullOrWhiteSpace(game.Platform)) metadata.Add(game.Platform);
                if (duplicateTitles.Contains(game.Title))
                {
                    var discriminator = game.GameId.Length > 8 ? game.GameId[..8] : game.GameId;
                    metadata.Add($"ID {discriminator}");
                }

                gameItems.Add(new ListItem(new LaunchGameCommand(game.GameId))
                {
                    Title = game.Title,
                    Subtitle = string.Join(" · ", metadata),
                });
            }

            _statusItems = statusItems.ToArray();
            _gameItems = gameItems.ToArray();
            ApplySearch();
        }
        catch (OperationCanceledException)
        {
            // The page can be disposed while the catalog read is in flight.
        }
        catch (Exception)
        {
            _statusItems =
            [
                CreateStatusItem("Couldn't read the Playnite catalog. Check the companion plugin and snapshot permissions."),
                CreateRefreshItem("Refresh recently played games"),
            ];
            _gameItems = Array.Empty<IListItem>();
            ApplySearch();
        }
    }

    private static IListItem CreateStatusItem(string message) =>
        new ListItem(new NoOpCommand()) { Title = message };

    private IListItem CreateRefreshItem(string message) =>
        new ListItem(new RefreshCatalogCommand(this)) { Title = message, Subtitle = "Press Enter to retry" };

    private sealed class RefreshCatalogCommand : InvokableCommand
    {
        private readonly PlayniteCmdPalRecentlyPlayedPage _page;
        public RefreshCatalogCommand(PlayniteCmdPalRecentlyPlayedPage page) => _page = page;
        public override string Name => "Refresh Playnite catalog";
        public override CommandResult Invoke()
        {
            _ = _page.LoadItemsAsync(forceRefresh: true);
            return CommandResult.KeepOpen();
        }
    }
}
