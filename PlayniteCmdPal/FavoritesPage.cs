using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

internal sealed partial class FavoritesPage : DynamicListPage
{
    private readonly IReadOnlyPlayniteCatalog _catalog = new PlayniteCatalogAdapter();
    private readonly FavoritesStore _favorites = FavoritesPreferences.Store;
    private readonly LatestRequestGate _requestGate = new();
    private IListItem[] _items = [CreateStatusItem("Loading your favorites…")];
    private string _searchText = string.Empty;

    public FavoritesPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Favorites";
        Name = "Pinned games";
        _favorites.FavoritesChanged += OnFavoritesChanged;
        LoadItems();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _requestGate.Synchronize(() => _searchText = newSearch ?? string.Empty);
        LoadItems();
    }

    public override IListItem[] GetItems() => _requestGate.Synchronize(() => _items);

    // Never refresh a Command Palette page inline inside the command's Invoke call.
    // A page notification here can re-enter the extension while it is dispatching the
    // command result, suppressing the toast and leaving the page in a stale loading state.
    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(() =>
        {
            try { LoadItems(); }
            catch { /* A refresh must not turn a successful pin/unpin into a failed command. */ }
        });
    }

    private void LoadItems()
    {
        var request = _requestGate.BeginRequest(() =>
        {
            _items = [CreateStatusItem("Loading your favorites…")];
            return _searchText;
        });
        RaiseItemsChanged();
        _ = LoadItemsAsync(request.Version, request.State);
    }

    private async Task LoadItemsAsync(int version, string query)
    {
        try
        {
            var catalog = await _catalog.GetAsync().ConfigureAwait(false);
            if (!catalog.IsAvailable)
            {
                PublishIfCurrent(version,
                [
                    CreateStatusItem(catalog.Issue?.Message
                        ?? "The Playnite catalog is unavailable. Start Playnite and refresh.")
                ]);
                return;
            }

            var favoriteIds = _favorites.GetFavoriteIds().ToHashSet(StringComparer.Ordinal);
            var games = catalog.Games.Where(game => favoriteIds.Contains(game.GameId)).ToArray();
            var matches = GameSearch.Search(games, query);

            if (matches.Count == 0)
            {
                var message = favoriteIds.Count == 0
                    ? "No favorites yet. Pin a game from its search result actions."
                    : "No saved favorites match this search in the current Playnite library.";
                PublishIfCurrent(version, [CreateStatusItem(message)]);
                return;
            }

            var items = matches.Select(game => new ListItem(new LaunchGameCommand(game.GameId))
            {
                Title = game.Title,
                Subtitle = BuildMetadata(game),
                MoreCommands =
                [
                    new CommandContextItem(new ShowGameDetailsCommand(game.GameId)),
                    new CommandContextItem(new ToggleFavoriteCommand(_favorites, game.GameId)),
                ],
            }).ToArray();

            PublishIfCurrent(version, items);
        }
        catch (Exception)
        {
            PublishIfCurrent(version,
            [
                CreateStatusItem("Couldn't load Favorites. Check the local preferences file and Playnite catalog, then try again.")
            ]);
        }
    }

    // Keep the gate around ownership/state mutation only. Never call the Command Palette
    // notification API while holding its lock.
    private void PublishIfCurrent(int version, IListItem[] items)
    {
        var published = _requestGate.TryPublish(version, () => _items = items);
        if (published) RaiseItemsChanged();
    }

    private static string BuildMetadata(GameEntry game)
    {
        var metadata = new List<string>();
        if (!string.IsNullOrWhiteSpace(game.Source)) metadata.Add(game.Source);
        if (!string.IsNullOrWhiteSpace(game.Platform)) metadata.Add(game.Platform);
        if (game.IsInstalled.HasValue) metadata.Add(game.IsInstalled.Value ? "Installed" : "Not installed");
        return string.Join(" · ", metadata);
    }

    private static IListItem CreateStatusItem(string message) =>
        new ListItem(new NoOpCommand()) { Title = message };
}
