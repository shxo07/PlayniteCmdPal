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

    [Fact]
    public void AtomicTogglesAddAndRemoveSelectionsWithoutLosingOtherValues()
    {
        var state = CreateState();

        Assert.True(state.ToggleSource("Steam"));
        Assert.True(state.ToggleSource("Epic"));
        Assert.Equal(new[] { "Epic", "Steam" }, state.SelectedSources);
        Assert.True(state.ToggleSource("steam"));
        Assert.Equal(new[] { "Epic" }, state.SelectedSources);

        Assert.True(state.TogglePlatform("PC"));
        Assert.True(state.TogglePlatform("Linux"));
        Assert.True(state.TogglePlatform("pc"));
        Assert.Equal(new[] { "Linux" }, state.SelectedPlatforms);

        Assert.True(state.ToggleInstalledStatus(false));
        Assert.Equal(new[] { true, false }, state.InstalledStatuses);
        Assert.True(state.ToggleInstalledStatus(true));
        Assert.Equal(new[] { false }, state.InstalledStatuses);

        Assert.False(state.ToggleSource("Missing"));
        Assert.False(state.TogglePlatform("Missing"));
    }

    [Fact]
    public void CapturedSelectionArraysAreDetachedFromMutableState()
    {
        var state = CreateState();
        state.SetSources(["Steam"]);
        state.SetPlatforms(["PC"]);

        var captured = state.CaptureSelections();
        captured.Sources[0] = "Changed";
        captured.Platforms[0] = "Changed";
        captured.InstalledStatuses[0] = false;

        Assert.Equal(new[] { "Steam" }, state.SelectedSources);
        Assert.Equal(new[] { "PC" }, state.SelectedPlatforms);
        Assert.Equal(new[] { true }, state.InstalledStatuses);
    }

    [Fact]
    public void SelectionNotificationsAreRaisedOutsideTheStateLock()
    {
        var state = CreateState();
        state.FilterSelectionsChanged += (_, _) =>
        {
            // If the callback ran while the state lock was held by this thread, this
            // independent reader could not complete until the callback returned.
            var read = Task.Run(() => state.CaptureSelections());
            Assert.True(read.Wait(TimeSpan.FromSeconds(2)));
        };

        state.SetSources(["Steam"]);
    }

    [Fact]
    public async Task ConcurrentCatalogRefreshAndSelectionChangesRemainConsistent()
    {
        var state = CreateState();
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

        var catalogUpdates = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 500; i++)
                {
                    state.UpdateCatalog(i % 2 == 0
                        ? Catalog
                        : [new GameEntry("other", "Other", "GOG", true, Platform: "Linux")]);
                    _ = state.CaptureSelections();
                    _ = state.ToSearchFilters();
                }
            }
            catch (Exception exception)
            {
                failures.Enqueue(exception);
            }
        });

        var selectionUpdates = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 500; i++)
                {
                    state.SetSources(i % 2 == 0 ? ["Steam"] : ["GOG"]);
                    state.SetPlatforms(i % 2 == 0 ? ["PC"] : ["Linux"]);
                    state.SetInstalledStatuses(i % 2 == 0 ? [true] : [false]);
                    _ = state.ActiveFilterCount;
                    _ = state.SelectedSources.ToArray();
                    _ = state.SelectedPlatforms.ToArray();
                }
            }
            catch (Exception exception)
            {
                failures.Enqueue(exception);
            }
        });

        await Task.WhenAll(catalogUpdates, selectionUpdates);

        Assert.Empty(failures);
        var snapshot = state.CaptureSelections();
        Assert.NotNull(snapshot.InstalledStatuses);
        Assert.NotNull(snapshot.Sources);
        Assert.NotNull(snapshot.Platforms);
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
