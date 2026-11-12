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
    ];

    [Fact]
    public void InstalledStatusCanBeEnabledAsTheSearchPageDefault()
    {
        var defaultState = new CatalogFilterState(defaultToInstalled: true);
        var unfilteredState = new CatalogFilterState();

        Assert.True(defaultState.ToSearchFilters().IsInstalled);
        Assert.Null(unfilteredState.ToSearchFilters().IsInstalled);
    }

    [Fact]
    public void SupportsMultipleSelectionsWithinEveryDimension()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);

        state.SetInstalledStatuses([true, false]);
        state.SetSources(["Steam", "Epic"]);
        state.SetPlatforms(["PC", "Linux"]);

        var filters = state.ToSearchFilters();
        Assert.Null(filters.IsInstalled);
        Assert.Equal(new[] { "Epic", "Steam" }, filters.Sources);
        Assert.Equal(new[] { "Linux", "PC" }, filters.Platforms);
        Assert.Equal(4, state.ActiveFilterCount);
    }

    [Fact]
    public void SingleInstalledStatusAppliesRestrictionButBothOrNoneDoNot()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);

        state.SetInstalledStatuses([true]);
        Assert.True(state.ToSearchFilters().IsInstalled);

        state.SetInstalledStatuses([false]);
        Assert.False(state.ToSearchFilters().IsInstalled);

        state.SetInstalledStatuses([true, false]);
        Assert.Null(state.ToSearchFilters().IsInstalled);

        state.ClearInstalledStatuses();
        Assert.Null(state.ToSearchFilters().IsInstalled);
    }

    [Fact]
    public void ClearingOneDimensionPreservesSelectionsInOtherDimensions()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        state.SetInstalledStatuses([true]);
        state.SetSources(["Steam", "Epic"]);
        state.SetPlatforms(["PC"]);

        state.ClearSources();

        var filters = state.ToSearchFilters();
        Assert.True(filters.IsInstalled);
        Assert.Null(filters.Sources);
        Assert.Equal(new[] { "PC" }, filters.Platforms);
    }

    [Fact]
    public void CatalogRefreshPreservesValidSelectionsAndDropsUnavailableValues()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        state.SetInstalledStatuses([false]);
        state.SetSources(["Steam", "Epic"]);
        state.SetPlatforms(["PC", "Linux"]);

        state.UpdateCatalog(
        [
            new GameEntry("steam-installed", "Halo Infinite", "Steam", true, Platform: "PC"),
            new GameEntry("gog-installed", "Apex Legends", "GOG", true, Platform: "PC"),
        ]);

        var filters = state.ToSearchFilters();
        Assert.False(filters.IsInstalled);
        Assert.Equal(new[] { "Steam" }, filters.Sources);
        Assert.Equal(new[] { "PC" }, filters.Platforms);
    }

    [Fact]
    public void NewCatalogOptionsDoNotDiscardStillValidSelections()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog([new GameEntry("1", "Halo", "Steam", true, Platform: "PC")]);
        state.SetSources(["Steam"]);
        state.SetPlatforms(["PC"]);

        state.UpdateCatalog(
        [
            new GameEntry("1", "Halo", "Steam", true, Platform: "PC"),
            new GameEntry("2", "Apex", "Epic", true, Platform: "Linux"),
        ]);

        Assert.Equal(new[] { "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);
        Assert.Contains("Epic", state.Sources);
        Assert.Contains("Linux", state.Platforms);
    }

    [Fact]
    public void OptionChangesNotifyOnceAndUnchangedCatalogDoesNotNotify()
    {
        var state = new CatalogFilterState();
        var notifications = 0;
        state.FilterOptionsChanged += (_, _) => notifications++;

        Assert.True(state.UpdateCatalog(Catalog));
        Assert.Equal(1, notifications);

        Assert.False(state.UpdateCatalog(Catalog.Reverse()));
        Assert.Equal(1, notifications);

        state.UpdateCatalog([new GameEntry("1", "Halo", "Steam", true, Platform: "PC")]);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void MetadataSelectionsAreCaseInsensitiveAndCanonicalized()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);

        state.SetSources(["sTeAm"]);
        state.SetPlatforms(["pc"]);

        Assert.Equal(new[] { "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);
    }

    [Fact]
    public void ClearAllResetsEveryDimension()
    {
        var state = new CatalogFilterState();
        state.UpdateCatalog(Catalog);
        state.SetInstalledStatuses([true]);
        state.SetSources(["Steam"]);
        state.SetPlatforms(["PC"]);

        state.ClearAll();

        var filters = state.ToSearchFilters();
        Assert.Null(filters.IsInstalled);
        Assert.Null(filters.Sources);
        Assert.Null(filters.Platforms);
        Assert.Equal(0, state.ActiveFilterCount);
    }
}
