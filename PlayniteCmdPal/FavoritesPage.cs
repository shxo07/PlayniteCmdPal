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

internal sealed partial class FavoritesPage : DynamicListPage
{
    private readonly IReadOnlyPlayniteCatalog _catalog = new PlayniteCatalogAdapter();
    private readonly FavoritesStore _favorites = FavoritesPreferences.Store;
    private readonly ArtworkIconLoader _artwork = new(artworkId => PlayniteCatalogAdapter.ResolveArtworkPath(artworkId));
    private readonly LatestRequestGate _requestGate = new();
    private readonly object _lifecycleGate = new();
    private readonly EventHandler _favoritesChangedHandler;
    private CancellationTokenSource? _requestCancellation;
    private IListItem[] _items = [CreateStatusItem("Loading your favorites…")];
    private string _searchText = string.Empty;

    public FavoritesPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Favorites";
        Name = "Pinned games";

        // The shared store must not keep retired page instances alive. Its event delegate
        // captures only a WeakReference, so the page can be collected with the host page.
        var weakSelf = new WeakReference<FavoritesPage>(this);
        EventHandler weakHandler = null!;
        weakHandler = (sender, args) =>
        {
            if (weakSelf.TryGetTarget(out var page))
            {
                page.OnFavoritesChanged(sender, args);
            }
            else if (sender is FavoritesStore store && weakHandler is not null)
            {
                store.FavoritesChanged -= weakHandler;
            }
        };
        _favoritesChangedHandler = weakHandler;
        _favorites.FavoritesChanged += _favoritesChangedHandler;
        LoadItems();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _requestGate.Synchronize(() => _searchText = newSearch ?? string.Empty);
        LoadItems();
    }

    public override IListItem[] GetItems() => _requestGate.Synchronize(() => _items);

    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        // Wait until the command has returned KeepOpen before publishing the new list.
        _ = RefreshAfterFavoriteChangeAsync();
    }

    private async Task RefreshAfterFavoriteChangeAsync()
    {
        await Task.Delay(50).ConfigureAwait(false);
        LoadItems();
    }

    private void LoadItems()
    {
        CancellationToken token;
        (int Version, string State) request;
        lock (_lifecycleGate)
        {
            _requestCancellation?.Cancel();
            _requestCancellation?.Dispose();
            _requestCancellation = new CancellationTokenSource();
            token = _requestCancellation.Token;
            request = _requestGate.BeginRequest(() =>
            {
                _items = [CreateStatusItem("Loading your favorites…")];
                return _searchText;
            });
        }

        RaiseItemsChanged(_items.Length);
        _ = LoadItemsAsync(request.Version, request.State, token);
    }

    private async Task LoadItemsAsync(int version, string query, CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!catalog.IsAvailable)
            {
                PublishIfCurrent(version,
                [
                    CreateStatusItem(catalog.Issue?.Message
                        ?? "The Playnite catalog is unavailable. Start Playnite and refresh.")
                ], cancellationToken);
                return;
            }

            var favoriteIds = _favorites.GetFavoriteIds().ToHashSet(StringComparer.Ordinal);
            var games = catalog.Games.Where(game => favoriteIds.Contains(game.GameId)).ToArray();
            var matches = GameSearch.Search(games, query);
            cancellationToken.ThrowIfCancellationRequested();

            if (matches.Count == 0)
            {
                var message = favoriteIds.Count == 0
                    ? "No favorites yet. Pin a game from its search result actions."
                    : "No saved favorites match this search in the current Playnite library.";
                PublishIfCurrent(version, [CreateStatusItem(message)], cancellationToken);
                return;
            }

            var items = new List<IListItem>();
            var artworkItems = new List<(ListItem Item, GameEntry Game)>();
            foreach (var game in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
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

            if (!PublishIfCurrent(version, items.ToArray(), cancellationToken))
            {
                return;
            }

            _ = LoadArtworkAsync(version, artworkItems, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer query or pin/unpin event superseded this request.
        }
        catch (Exception)
        {
            PublishIfCurrent(version,
            [
                CreateStatusItem("Couldn't load Favorites. Check the local preferences file and Playnite catalog, then try again.")
            ], cancellationToken);
        }
    }

    private async Task LoadArtworkAsync(
        int version,
        IReadOnlyList<(ListItem Item, GameEntry Game)> artworkItems,
        CancellationToken cancellationToken)
    {
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
                        var published = _requestGate.TryPublish(version, () =>
                        {
                            entry.Item.Icon = icon ?? ArtworkIconLoader.FallbackIcon;
                        });
                        if (published) RaiseItemsChanged(_items.Length);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        // Artwork is optional; one bad image must not affect other favorites.
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer Favorites request cancelled obsolete artwork work.
        }
    }

    private bool PublishIfCurrent(int version, IListItem[] items, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return false;
        var published = _requestGate.TryPublish(version, () => _items = items);
        if (published) RaiseItemsChanged(_items.Length);
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
