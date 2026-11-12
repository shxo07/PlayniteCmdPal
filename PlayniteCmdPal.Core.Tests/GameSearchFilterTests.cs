using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class GameSearchFilterTests
{
    private static readonly GameEntry[] Catalog =
    [
        new("s1", "Halo Infinite", "Steam", true, Platform: "PC"),
        new("s2", "Halo", "Steam", false, Platform: "PC"),
        new("e1", "Halo Wars", "Epic", true, Platform: "Linux"),
        new("g1", "Apex Legends", "GOG", true, Platform: "PC"),
        new("missing", "Halo Unknown", null, null, Platform: null),
    ];

    [Fact]
    public void UsesOrWithinDimensionsAndAndAcrossDimensions()
    {
        var filters = new GameSearchFilters(
            IsInstalled: true,
            Sources: ["Steam", "Epic"],
            Platforms: ["PC"]);

        var results = GameSearch.Search(Catalog, "", filters);

        Assert.Equal(new[] { "s1" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void AllowsAnyCombinationWithinOneDimension()
    {
        var filters = new GameSearchFilters(Sources: ["Steam", "Epic"]);

        var results = GameSearch.Search(Catalog, "halo", filters);

        Assert.Equal(new[] { "s2", "s1", "e1" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void NeitherOrBothStatusesAreUnrestrictedIncludingUnknownMetadata()
    {
        Assert.Contains("missing", GameSearch.Search(Catalog, "", new GameSearchFilters()).Select(game => game.GameId));
        Assert.DoesNotContain("missing", GameSearch.Search(Catalog, "", new GameSearchFilters(IsInstalled: true)).Select(game => game.GameId));
        Assert.DoesNotContain("missing", GameSearch.Search(Catalog, "", new GameSearchFilters(IsInstalled: false)).Select(game => game.GameId));

        // CatalogFilterState intentionally maps both selected statuses to a null status filter.
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        state.SetInstalledStatuses([true, false]);
        Assert.Contains("missing", GameSearch.Search(Catalog, "", state.ToSearchFilters()).Select(game => game.GameId));

        state.ClearInstalledStatuses();
        Assert.Contains("missing", GameSearch.Search(Catalog, "", state.ToSearchFilters()).Select(game => game.GameId));
    }

    [Fact]
    public void FiltersBeforeTitleRankingAndKeepRankingDeterministic()
    {
        var filters = new GameSearchFilters(Sources: ["Steam"]);
        var expected = new[] { "s2", "s1" };

        Assert.Equal(expected, GameSearch.Search(Catalog, "halo", filters).Select(game => game.GameId));
        Assert.Equal(expected, GameSearch.Search(Catalog.Reverse(), "halo", filters).Select(game => game.GameId));
    }

    [Fact]
    public void EmptySearchReturnsFilteredCatalogAndNoMatchesStayEmpty()
    {
        var filtered = GameSearch.Search(Catalog, "   ", new GameSearchFilters(Platforms: ["Linux"]));
        Assert.Equal(new[] { "e1" }, filtered.Select(game => game.GameId));
        Assert.Empty(GameSearch.Search(Catalog, "missing title", new GameSearchFilters(Sources: ["Steam"])));
    }

    [Fact]
    public void MissingMetadataDoesNotMatchASelectedMetadataRestriction()
    {
        Assert.Empty(GameSearch.Search(Catalog, "halo", new GameSearchFilters(Sources: ["Steam"])).Where(game => game.GameId == "missing"));
        Assert.Empty(GameSearch.Search(Catalog, "halo", new GameSearchFilters(Platforms: ["PC"])).Where(game => game.GameId == "missing"));
    }
}
