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
    private IListItem[] _items = [CreateStatusItem("Loading your favorites…")];
    private string _searchText = string.Empty;
    private int _requestVersion;

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
        _searchText = newSearch ?? string.Empty;
        LoadItems();
    }

    public override IListItem[] GetItems() => _items;

    private void OnFavoritesChanged(object? sender, EventArgs e) => LoadItems();

    private void LoadItems()
    {
        var version = ++_requestVersion;
        _items = [CreateStatusItem("Loading your favorites…")];
        RaiseItemsChanged();
        _ = LoadItemsAsync(version, _searchText);
    }

    private async Task LoadItemsAsync(int version, string query)
    {
        try
        {
            var catalog = await _catalog.GetAsync().ConfigureAwait(false);
            if (version != _requestVersion)
            {
                return;
            }

            if (!catalog.IsAvailable)
            {
                _items = [CreateStatusItem(catalog.Issue?.Message ?? "The Playnite catalog is unavailable. Start Playnite and refresh.")];
                RaiseItemsChanged();
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
                _items = [CreateStatusItem(message)];
                RaiseItemsChanged();
                return;
            }

            _items = matches.Select(game => new ListItem(new LaunchGameCommand(game.GameId))
            {
                Title = game.Title,
                Subtitle = BuildMetadata(game),
                MoreCommands =
                [
                    new CommandContextItem(new ShowGameDetailsCommand(game.GameId)),
                    new CommandContextItem(new ToggleFavoriteCommand(_favorites, game.GameId)),
                ],
            }).ToArray();
            RaiseItemsChanged();
        }
        catch (Exception)
        {
            if (version == _requestVersion)
            {
                _items = [CreateStatusItem("Couldn't load Favorites. Check the local preferences file and Playnite catalog, then try again.")];
                RaiseItemsChanged();
            }
        }
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
