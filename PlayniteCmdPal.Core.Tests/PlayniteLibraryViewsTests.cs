using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class PlayniteLibraryViewsTests
{
    [Fact]
    public void SearchViewUsesFullCatalogRankingAndOptionalFilters()
    {
        var games = new[]
        {
            new GameEntry("a", "Portal Stories", IsInstalled: true),
            new GameEntry("b", "Portal 2", IsInstalled: false),
            new GameEntry("c", "Halo", IsInstalled: true),
        };

        var results = PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.Search,
            "portal",
            filters: new GameSearchFilters(IsInstalled: true));

        Assert.Equal("a", Assert.Single(results).GameId);
    }

    [Fact]
    public void FavoritesViewUsesStableFavoriteIdsAndSearchRanking()
    {
        var games = new[]
        {
            new GameEntry("a", "Portal Stories"),
            new GameEntry("b", "Portal 2"),
            new GameEntry("c", "Halo"),
        };

        var results = PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.Favorites,
            "portal",
            new List<string> { "b", "c" });

        Assert.Equal("b", Assert.Single(results).GameId);
    }

    [Fact]
    public void FavoritesViewReturnsEmptyWhenNoFavoriteIdsExist()
    {
        var games = new[] { new GameEntry("a", "Portal 2") };

        Assert.Empty(PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.Favorites,
            query: null));
    }

    [Fact]
    public void RecentlyPlayedViewPreservesNewestFirstOrderingWhenSearching()
    {
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var games = new[]
        {
            new GameEntry("older", "Portal 2", LastActivity: now.AddDays(-2)),
            new GameEntry("newer", "Portal Stories", LastActivity: now.AddDays(-1)),
            new GameEntry("unplayed", "Portal 3"),
            new GameEntry("newest", "Halo", LastActivity: now),
        };

        var results = PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.RecentlyPlayed,
            "  portal  ");

        Assert.Collection(results,
            game => Assert.Equal("newer", game.GameId),
            game => Assert.Equal("older", game.GameId));
    }

    [Fact]
    public void RecentlyPlayedViewExcludesGamesWithoutActivity()
    {
        var games = new[] { new GameEntry("never", "Never played") };

        Assert.Empty(PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.RecentlyPlayed,
            query: null));
    }

    [Fact]
    public void RejectsNullCatalog()
    {
        Assert.Throws<ArgumentNullException>(() => PlayniteLibraryViews.Search(
            null!,
            PlayniteLibraryViewMode.Search,
            query: null));
    }

    [Fact]
    public void RejectsUnknownViewMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayniteLibraryViews.Search(
            Array.Empty<GameEntry>(),
            (PlayniteLibraryViewMode)999,
            query: null));
    }

    [Fact]
    public void SharedFiltersApplyToFavoritesWithoutChangingFavoriteSearchRanking()
    {
        var games = new[]
        {
            new GameEntry("a", "Portal Stories", "Steam", IsInstalled: true, Platform: "PC"),
            new GameEntry("b", "Portal 2", "Epic", IsInstalled: true, Platform: "PC"),
            new GameEntry("c", "Portal 3", "Steam", IsInstalled: true, Platform: "Linux"),
        };

        var results = PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.Favorites,
            "portal",
            new List<string> { "a", "b", "c" },
            new GameSearchFilters(IsInstalled: true, Sources: ["Steam"], Platforms: ["PC"]));

        Assert.Equal("a", Assert.Single(results).GameId);
    }

    [Fact]
    public void SharedFiltersApplyToRecentlyPlayedWithoutChangingActivityOrdering()
    {
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var games = new[]
        {
            new GameEntry("older", "Portal 2", "Steam", IsInstalled: true, Platform: "PC", LastActivity: now.AddDays(-2)),
            new GameEntry("newer", "Portal Stories", "Steam", IsInstalled: true, Platform: "PC", LastActivity: now.AddDays(-1)),
            new GameEntry("source-filtered", "Portal 3", "Epic", IsInstalled: true, Platform: "PC", LastActivity: now),
            new GameEntry("platform-filtered", "Portal 4", "Steam", IsInstalled: true, Platform: "Linux", LastActivity: now.AddHours(1)),
        };

        var results = PlayniteLibraryViews.Search(
            games,
            PlayniteLibraryViewMode.RecentlyPlayed,
            "portal",
            filters: new GameSearchFilters(IsInstalled: true, Sources: ["Steam"], Platforms: ["PC"]));

        Assert.Collection(results,
            game => Assert.Equal("newer", game.GameId),
            game => Assert.Equal("older", game.GameId));
    }
}
