using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class GameSearchFilterTests
{
    private static readonly GameEntry[] Catalog =
    [
        new("steam-installed", "Halo Infinite", "Steam", true, Platform: "PC"),
        new("steam-not-installed", "Halo", "Steam", false, Platform: "PC"),
        new("epic-installed", "Halo Wars", "Epic", true, Platform: "Linux"),
        new("xbox-installed", "Halo: Spartan Assault", "Xbox", true, Platform: "Xbox"),
        new("unknown", "Halo: The Master Chief Collection"),
        new("other", "Apex Legends", "Steam", true, Platform: "PC"),
    ];

    [Fact]
    public void InstalledFilterIncludesOnlyKnownInstalledGames()
    {
        var results = GameSearch.Search(Catalog, null, new GameSearchFilters(IsInstalled: true));

        Assert.Equal(new[] { "epic-installed", "other", "steam-installed", "xbox-installed" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void NotInstalledFilterIncludesOnlyKnownNotInstalledGames()
    {
        var results = GameSearch.Search(Catalog, null, new GameSearchFilters(IsInstalled: false));

        Assert.Equal(new[] { "steam-not-installed" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void MultipleValuesWithinOneMetadataDimensionAreOred()
    {
        var filters = new GameSearchFilters(
            Sources: new[] { "Steam", "Xbox" },
            Platforms: new[] { "PC", "Xbox" });

        var results = GameSearch.Search(Catalog, "halo", filters);

        Assert.Equal(new[] { "steam-not-installed", "steam-installed", "xbox-installed" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void MultipleValuesUseOrWithinDimensionsAndAndAcrossAllDimensions()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        state.SetInstalledStatuses([true]);
        state.SetSources(["Steam", "Epic"]);
        state.SetPlatforms(["PC", "Linux"]);

        var results = GameSearch.Search(Catalog, "halo", state.ToSearchFilters());

        Assert.Equal(new[] { "steam-installed", "epic-installed" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void SelectingBothInstalledStatusesLeavesStatusDimensionUnfiltered()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        state.SetInstalledStatuses([true, false]);

        var results = GameSearch.Search(Catalog, "halo", state.ToSearchFilters());

        Assert.Contains(results, game => game.GameId == "steam-installed");
        Assert.Contains(results, game => game.GameId == "steam-not-installed");
        Assert.DoesNotContain(results, game => game.GameId == "unknown");
    }

    [Fact]
    public void MissingMetadataDoesNotHideEntriesWhenThatDimensionIsNotFiltered()
    {
        var results = GameSearch.Search(Catalog, "halo", new GameSearchFilters(IsInstalled: true));

        Assert.Contains(results, game => game.GameId == "steam-installed");
        Assert.Contains(results, game => game.GameId == "epic-installed");
        Assert.DoesNotContain(results, game => game.GameId == "unknown");
    }

    [Fact]
    public void MetadataFiltersAreCaseInsensitiveAndIgnoreBlankSelections()
    {
        var results = GameSearch.Search(
            Catalog,
            null,
            new GameSearchFilters(Sources: new[] { "", "  ", "sTeAm" }, Platforms: new[] { "pc" }));

        Assert.Equal(new[] { "other", "steam-installed", "steam-not-installed" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void EmptyQueryReturnsAllEntriesMatchingFiltersInStableOrder()
    {
        var results = GameSearch.Search(
            Catalog.Reverse(),
            "   ",
            new GameSearchFilters(Platforms: new[] { "PC" }));

        Assert.Equal(new[] { "other", "steam-installed", "steam-not-installed" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void FilteringDoesNotChangeDeterministicTitleRanking()
    {
        var catalog = new[]
        {
            new GameEntry("substring", "Superhalo", "Steam", true, Platform: "PC"),
            new GameEntry("word", "The Halo Effect", "Steam", true, Platform: "PC"),
            new GameEntry("prefix", "Halo Infinite", "Steam", true, Platform: "PC"),
            new GameEntry("exact", "Halo", "Steam", true, Platform: "PC"),
            new GameEntry("excluded", "Halo", "GOG", false, Platform: "PC"),
        };

        var results = GameSearch.Search(
            catalog,
            "halo",
            new GameSearchFilters(IsInstalled: true, Sources: new[] { "Steam" }));

        Assert.Equal(new[] { "exact", "prefix", "word", "substring" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void NoMatchingFiltersReturnAnEmptyCollection()
    {
        Assert.Empty(GameSearch.Search(
            Catalog,
            "halo",
            new GameSearchFilters(Sources: new[] { "PlayStation" })));
    }

    [Fact]
    public void EmptyCatalogReturnsNoResults()
    {
        Assert.Empty(GameSearch.Search(Array.Empty<GameEntry>(), null, new GameSearchFilters(IsInstalled: true)));
    }
}
