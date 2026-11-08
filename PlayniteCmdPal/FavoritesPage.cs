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
    private readonly ArtworkIconLoader _artwork = new(artworkId => PlayniteCatalogAdapter.ResolveArtworkPath(artworkId));
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

    // Defer page refresh until the command invocation has returned to the host.
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

            var items = new List<IListItem>();
            var artworkItems = new List<(ListItem Item, GameEntry Game)>();
            foreach (var game in matches)
            {
                var item = new ListItem(new LaunchGameCommand(game.GameId))
                {
                    Title = game.Title,
                    Subtitle = BuildMetadata(game),
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

            if (!PublishIfCurrent(version, items.ToArray()))
            {
                return;
            }

            _ = LoadArtworkAsync(version, artworkItems);
        }
        catch (Exception)
        {
            PublishIfCurrent(version,
            [
                CreateStatusItem("Couldn't load Favorites. Check the local preferences file and Playnite catalog, then try again.")
            ]);
        }
    }

    private async Task LoadArtworkAsync(int version, IReadOnlyList<(ListItem Item, GameEntry Game)> artworkItems)
    {
        await Parallel.ForEachAsync(
            artworkItems,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (entry, _) =>
            {
                try
                {
                    var icon = await _artwork.GetIconAsync(entry.Game, default).ConfigureAwait(false);
                    var published = _requestGate.TryPublish(version, () =>
                    {
                        entry.Item.Icon = icon ?? ArtworkIconLoader.FallbackIcon;
                    });
                    if (published) RaiseItemsChanged();
                }
                catch (Exception)
                {
                    // Artwork is optional; one bad image must not affect other favorites.
                }
            }).ConfigureAwait(false);
    }

    private bool PublishIfCurrent(int version, IListItem[] items)
    {
        var published = _requestGate.TryPublish(version, () => _items = items);
        if (published) RaiseItemsChanged();
        return published;
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
