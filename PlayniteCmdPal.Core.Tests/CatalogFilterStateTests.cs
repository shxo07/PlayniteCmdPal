using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class CatalogFilterStateTests
{
    [Fact]
    public void UpdateCatalogReportsOnlyActualOptionChanges()
    {
        var state = new CatalogFilterState();
        var initial = new[]
        {
            new GameEntry("1", "Halo", "Steam", true, Platform: "PC"),
            new GameEntry("2", "Apex", "GOG", false, Platform: "Deck"),
        };

        Assert.True(state.UpdateCatalog(initial));
        Assert.False(state.UpdateCatalog(initial.Reverse()));
        Assert.Equal(new[] { "GOG", "Steam" }, state.Sources);
        Assert.Equal(new[] { "Deck", "PC" }, state.Platforms);

        Assert.True(state.UpdateCatalog(new[]
        {
            new GameEntry("1", "Halo", "Steam", true, Platform: "PC"),
        }));
        Assert.Equal(new[] { "Steam" }, state.Sources);
        Assert.Equal(new[] { "PC" }, state.Platforms);
    }

    [Fact]
    public void RemovedSourceSelectionResetsToAllGames()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(new[] { new GameEntry("1", "Halo", "Steam", true) });
        state.SelectFilter("source:Steam");

        state.UpdateCatalog(new[] { new GameEntry("2", "Apex", "GOG", true) });

        Assert.Equal(CatalogFilterState.AllGamesId, state.CurrentFilterId);
        Assert.Null(state.ToSearchFilters().Sources);
    }

    [Fact]
    public void RemovedPlatformSelectionResetsToAllGames()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(new[] { new GameEntry("1", "Halo", "Steam", true, Platform: "PC") });
        state.SelectFilter("platform:PC");

        state.UpdateCatalog(new[] { new GameEntry("2", "Apex", "Steam", true, Platform: "Deck") });

        Assert.Equal(CatalogFilterState.AllGamesId, state.CurrentFilterId);
        Assert.Null(state.ToSearchFilters().Platforms);
    }

    [Fact]
    public void SelectedMetadataFilterSurvivesUnchangedCatalog()
    {
        var games = new[] { new GameEntry("1", "Halo", "Steam", true, Platform: "PC") };
        var state = new CatalogFilterState();
        state.UpdateCatalog(games);
        state.SelectFilter("source:Steam");

        Assert.False(state.UpdateCatalog(games));
        Assert.Equal("source:Steam", state.CurrentFilterId);
        Assert.Equal(new[] { "Steam" }, state.ToSearchFilters().Sources);
    }

    [Fact]
    public void SelectedMetadataFilterTracksCanonicalCasingAfterRefresh()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(new[] { new GameEntry("1", "Halo", "Steam", true) });
        state.SelectFilter("source:Steam");

        Assert.True(state.UpdateCatalog(new[] { new GameEntry("1", "Halo", "STEAM", true) }));

        Assert.Equal("source:STEAM", state.CurrentFilterId);
        Assert.Equal(new[] { "STEAM" }, state.ToSearchFilters().Sources);
    }

    [Fact]
    public void UnknownOrMalformedSelectionFallsBackToAllGames()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(new[] { new GameEntry("1", "Halo", "Steam", true) });

        state.SelectFilter("source:missing");
        Assert.Equal(CatalogFilterState.AllGamesId, state.CurrentFilterId);

        state.SelectFilter("unsupported-filter");
        Assert.Equal(CatalogFilterState.AllGamesId, state.CurrentFilterId);
    }
}
