using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class CatalogFilterStateTests
{
    private static readonly GameEntry[] Catalog =
    [
        new("steam-installed", "Halo Infinite", "Steam", true, Platform: "PC"),
        new("steam-not-installed", "Halo", "Steam", false, Platform: "PC"),
        new("epic-installed", "Halo Wars", "Epic", true, Platform: "Linux"),
        new("gog-installed", "Apex Legends", "GOG", true, Platform: "PC"),
        new("unknown", "Halo Unknown", null, null, Platform: null),
    ];

    [Fact]
    public void FreshStateDefaultsToInstalledAndHasNoAllGamesOption()
    {
        var state = new CatalogFilterState();

        Assert.Equal(new[] { true }, state.InstalledStatuses);
        Assert.True(state.ToSearchFilters().IsInstalled);
        Assert.Equal(1, state.ActiveFilterCount);
        Assert.Empty(state.Sources);
        Assert.Empty(state.Platforms);
    }

    [Fact]
    public void StatusSelectionsAreIndependentAndNeitherOrBothAreUnrestricted()
    {
        var state = CreateState();

        state.SetInstalledStatuses([false]);
        Assert.False(state.ToSearchFilters().IsInstalled);

        state.SetInstalledStatuses([]);
        Assert.Null(state.ToSearchFilters().IsInstalled);
        Assert.Equal(0, state.ActiveFilterCount);

        state.SetInstalledStatuses([true, false]);
        Assert.Null(state.ToSearchFilters().IsInstalled);
        Assert.Equal(0, state.ActiveFilterCount);

        Assert.Contains("unknown", GameSearch.Search(Catalog, "", state.ToSearchFilters()).Select(game => game.GameId));
    }

    [Fact]
    public void SourceAndPlatformOptionsAreMultiSelectAndCanonicalized()
    {
        var state = CreateState();
        state.SetSources(["sTeAm", "Epic"]);
        state.SetPlatforms(["pc", "Linux"]);

        Assert.Equal(new[] { "Epic", "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "Linux", "PC" }, state.SelectedPlatforms);
        Assert.Equal(5, state.ActiveFilterCount);
    }

    [Fact]
    public void CatalogRefreshRetainsValidSelectionsAndDropsStaleValues()
    {
        var state = CreateState();
        state.SetInstalledStatuses([false]);
        state.SetSources(["Steam", "Epic"]);
        state.SetPlatforms(["PC", "Linux"]);

        state.UpdateCatalog(
        [
            new GameEntry("a", "Halo", "Steam", true, Platform: "PC"),
            new GameEntry("b", "Apex", "GOG", false, Platform: "PC"),
        ]);

        Assert.False(state.ToSearchFilters().IsInstalled);
        Assert.Equal(new[] { "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);
    }

    [Fact]
    public void UnavailableCatalogDoesNotUpdateFilterStateUnlessExplicitlyReplaced()
    {
        var state = CreateState();
        state.SetSources(["Steam"]);
        state.SetPlatforms(["PC"]);

        // An unavailable read is not passed to UpdateCatalog by the results page. A later
        // successful refresh with a real empty library is an actual catalog and may drop options.
        Assert.Equal(new[] { "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);

        state.UpdateCatalog(Array.Empty<GameEntry>());
        Assert.Empty(state.SelectedSources);
        Assert.Empty(state.SelectedPlatforms);
        Assert.Empty(state.Sources);
        Assert.Empty(state.Platforms);
    }

    [Fact]
    public void OptionChangesNotifyAndUnchangedCatalogDoesNot()
    {
        var state = new CatalogFilterState();
        var notifications = 0;
        state.FilterOptionsChanged += (_, _) => notifications++;

        Assert.True(state.UpdateCatalog(Catalog));
        Assert.Equal(1, notifications);
        Assert.False(state.UpdateCatalog(Catalog.Reverse()));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void PerDimensionAndClearAllDoNotReEnableInstalledByDefault()
    {
        var state = CreateState();
        state.SetInstalledStatuses([false]);
        state.SetSources(["Steam"]);
        state.SetPlatforms(["PC"]);

        state.ClearSources();
        Assert.False(state.ToSearchFilters().IsInstalled);
        Assert.Null(state.ToSearchFilters().Sources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);

        state.ClearInstalledStatuses();
        Assert.Null(state.ToSearchFilters().IsInstalled);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);

        state.SetInstalledStatuses([true]);
        state.ClearPlatforms();
        Assert.True(state.ToSearchFilters().IsInstalled);
        Assert.Null(state.ToSearchFilters().Platforms);

        state.ClearAll();
        Assert.Null(state.ToSearchFilters().IsInstalled);
        Assert.Null(state.ToSearchFilters().Sources);
        Assert.Null(state.ToSearchFilters().Platforms);
        Assert.Equal(0, state.ActiveFilterCount);
    }

    private static CatalogFilterState CreateState()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        return state;
    }

    [Fact]
    public void RestoredSelectionsSurviveUntilCatalogOptionsAreLoadedAndCanBeCapturedAgain()
    {
        var state = new CatalogFilterState();
        var saved = new CatalogFilterSelection([false], ["Steam"], ["PC"]);

        state.RestoreSelections(saved);
        Assert.False(state.ToSearchFilters().IsInstalled);
        Assert.Equal(new[] { "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);

        state.UpdateCatalog(Catalog);

        var captured = state.CaptureSelections();
        Assert.Equal(new[] { false }, captured.InstalledStatuses);
        Assert.Equal(new[] { "Steam" }, captured.Sources);
        Assert.Equal(new[] { "PC" }, captured.Platforms);
    }

    [Fact]
    public void SelectionChangesNotifyPersistenceListenerButNoOpChangesDoNot()
    {
        var state = CreateState();
        var notifications = 0;
        state.FilterSelectionsChanged += (_, _) => notifications++;

        state.SetSources(["Steam"]);
        state.SetSources(["steam"]);
        state.SetInstalledStatuses([false]);
        state.ClearPlatforms();
        state.ClearAll();

        Assert.Equal(3, notifications);
    }
}
