using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Command.Tests;

public sealed class PlayniteTopLevelCommandsTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string Title = "Orbit Rally 16";
    private const string OtherTitle = "Moon Orchard";
    private const string IconId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherIconId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string CoverId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string IconPath = @"C:\artwork\synthetic-icon.png";
    private const string OtherIconPath = @"C:\artwork\other-synthetic-icon.png";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Command.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();
    private readonly List<ProcessStartInfo> _dispatched = [];
    private readonly ICommandItem[] _navigation = [
        new CommandItem(new NoOpCommand()) { Title = "Search games" },
        new CommandItem(new NoOpCommand()) { Title = "Favorites" },
        new CommandItem(new NoOpCommand()) { Title = "Recently played games" },
    ];
    private string SnapshotPath => Path.Combine(_directory, "catalog.json");

    [Fact]
    public async Task SdkRootCommandsContainIndividualGamesAndNoFallbackAndLaunchTheCapturedStableId()
    {
        using var provider = CreateProvider(await CreateCatalogAsync(new SnapshotGame(GameA, Title, true)));
        ICommandProvider sdk = provider;
        Assert.False(sdk.Frozen);
        Assert.Equal(_navigation, sdk.TopLevelCommands());
        Assert.Empty(sdk.FallbackCommands());

        await provider.RefreshCatalogAsync();

        var cached = sdk.TopLevelCommands();
        var item = Assert.Single(GameItems(provider));
        Assert.Equal(Title, item.Title);
        Assert.Equal("Open with Playnite · Installed", item.Subtitle);
        Assert.False(item is IFallbackCommandItem);
        var command = Assert.IsAssignableFrom<InvokableCommand>(item.Command);
        Assert.Equal("Open with Playnite", command.Name);
        Assert.Equal($"playnitecmdpal.open-game.{GameA}", command.Id);
        Assert.Same(cached, sdk.TopLevelCommands());
        Assert.Same(item, ((ICommandProvider4)provider).GetCommandItem(command.Id));
        Assert.Same(command, sdk.GetCommand(command.Id));
        Assert.Null(provider.GetCommandItem("unknown"));

        Assert.Equal(CommandResultKind.Dismiss, command.Invoke().Kind);
        Assert.Equal($"playnite://playnite/start/{GameA}", Assert.Single(_dispatched).FileName);
        Assert.True(_dispatched[0].UseShellExecute);
        // Root commands are reusable after reopening CmdPal, not one-use query fallbacks.
        Assert.Equal(CommandResultKind.Dismiss, command.Invoke().Kind);
        Assert.Equal(2, _dispatched.Count);
        Assert.Same(cached, sdk.TopLevelCommands());
    }

    [Fact]
    public async Task CatalogChangesPublishTheNewArrayBeforeTheSdkEventAndDoNotChurnUnchangedCommands()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        using var provider = CreateProvider(adapter);
        var observed = new List<ICommandItem[]>();
        var totals = new List<int>();
        provider.ItemsChanged += (_, args) =>
        {
            // Simulate a host COM listener re-entering the getter on another thread.
            observed.Add(Task.Run(provider.TopLevelCommands).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            totals.Add(args.TotalItems);
        };
        await provider.RefreshCatalogAsync();
        var first = provider.TopLevelCommands();
        var oldCommand = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        await provider.RefreshCatalogAsync();
        Assert.Same(first, provider.TopLevelCommands());
        Assert.Single(observed);

        await PublishAsync(new SnapshotGame(GameB, OtherTitle, false));
        await provider.RefreshCatalogAsync(forceRefresh: true);

        var current = provider.TopLevelCommands();
        Assert.Equal(_navigation, current.Take(_navigation.Length));
        Assert.Equal(OtherTitle, Assert.Single(GameItems(provider)).Title);
        Assert.Same(current, observed[1]);
        Assert.Equal(new[] { 4, 4 }, totals);
        Assert.Equal(CommandResultKind.ShowToast, oldCommand.Invoke().Kind);
        Assert.Empty(_dispatched);
        Assert.Null(provider.GetCommand(oldCommand.Id));
    }

    [Fact]
    public async Task ActivityAndCoverUpdatesDoNotChurnRootCommandsButDisplayedMetadataUpdatesDo()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        using var provider = CreateProvider(adapter);
        await provider.RefreshCatalogAsync();
        var cached = provider.TopLevelCommands();
        var previous = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        var notifications = 0;
        provider.ItemsChanged += (_, _) => Interlocked.Increment(ref notifications);
        await PublishAsync(new SnapshotGame(GameA, Title, true, coverArtworkId: CoverId,
            lastActivity: new DateTime(2026, 11, 12, 11, 0, 0, DateTimeKind.Utc)));

        await provider.RefreshCatalogAsync(forceRefresh: true);

        Assert.Same(cached, provider.TopLevelCommands());
        Assert.Equal(0, notifications);
        Assert.Equal(CommandResultKind.Dismiss, previous.Invoke().Kind);
        await PublishAsync(new SnapshotGame(GameA, Title, false, "Other store", "Other platform"));
        await provider.RefreshCatalogAsync(forceRefresh: true);

        Assert.NotSame(cached, provider.TopLevelCommands());
        Assert.Equal(1, notifications);
        Assert.Equal("Open with Playnite · Other store · Other platform · Not installed", Assert.Single(GameItems(provider)).Subtitle);
        Assert.Equal(CommandResultKind.ShowToast, previous.Invoke().Kind);
        Assert.Single(_dispatched);
    }

    [Theory]
    [InlineData("different-metadata")]
    [InlineData("identical-metadata")]
    [InlineData("case-variant-and-id-prefix-collision")]
    public async Task EqualTitlesHaveDistinctMetadataAndFullIdsAndLaunchOnlyTheExplicitlySelectedGame(string variant)
    {
        var secondId = variant == "case-variant-and-id-prefix-collision"
            ? "11111111-2222-4222-8222-222222222222" : GameB;
        var secondTitle = variant == "case-variant-and-id-prefix-collision" ? Title.ToUpperInvariant() : Title;
        var secondInstalled = variant != "different-metadata";
        var secondSource = variant == "different-metadata" ? "Store B" : "Store A";
        using var provider = CreateProvider(await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true, "Store A", "PC"),
            new SnapshotGame(secondId, secondTitle, secondInstalled, secondSource, "PC")));

        await provider.RefreshCatalogAsync();

        var games = GameItems(provider);
        Assert.Equal(2, games.Length);
        Assert.Equal(_navigation.Length + 2, provider.TopLevelCommands().Length);
        Assert.Empty(provider.FallbackCommands());
        var first = Assert.Single(games, item => item.Command.Id == $"playnitecmdpal.open-game.{GameA}");
        var second = Assert.Single(games, item => item.Command.Id == $"playnitecmdpal.open-game.{secondId}");
        Assert.Equal(Title, first.Title);
        Assert.Equal(secondTitle, second.Title);
        Assert.Equal($"Open with Playnite · Store A · PC · Installed · ID {GameA}", first.Subtitle);
        Assert.Contains($"Open with Playnite · {secondSource} · PC · ", second.Subtitle);
        Assert.Contains(secondInstalled ? " · Installed · " : " · Not installed · ", second.Subtitle);
        Assert.EndsWith($"ID {secondId}", second.Subtitle);
        Assert.NotEqual(first.Subtitle, second.Subtitle);

        Assert.Equal(CommandResultKind.Dismiss, Assert.IsAssignableFrom<InvokableCommand>(second.Command).Invoke().Kind);
        Assert.Equal(CommandResultKind.Dismiss, Assert.IsAssignableFrom<InvokableCommand>(first.Command).Invoke().Kind);
        Assert.Equal(new[] { $"playnite://playnite/start/{secondId}", $"playnite://playnite/start/{GameA}" },
            _dispatched.Select(start => start.FileName));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("stale")]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("replaced")]
    [InlineData("source")]
    [InlineData("platform")]
    [InlineData("installation")]
    [InlineData("partial")]
    public async Task RetainedRootCommandRevalidatesAndCannotLaunchAChangedOrUnhealthyCatalog(string change)
    {
        using var provider = CreateProvider(await CreateCatalogAsync(new SnapshotGame(GameA, Title, true)));
        await provider.RefreshCatalogAsync();
        var oldItem = Assert.Single(GameItems(provider));
        var command = Assert.IsAssignableFrom<InvokableCommand>(oldItem.Command);
        switch (change)
        {
            case "missing": File.Delete(SnapshotPath); break;
            case "malformed": await File.WriteAllTextAsync(SnapshotPath, "not json"); break;
            case "stale": _time.Advance(PlayniteCatalogAdapter.StaleAfter); break;
            case "removed": await PublishAsync(); break;
            case "renamed": await PublishAsync(new SnapshotGame(GameA, OtherTitle, true)); break;
            case "replaced": await PublishAsync(new SnapshotGame(GameB, Title, true)); break;
            case "source": await PublishAsync(new SnapshotGame(GameA, Title, true, "Other store")); break;
            case "platform": await PublishAsync(new SnapshotGame(GameA, Title, true, platform: "Other platform")); break;
            case "installation": await PublishAsync(new SnapshotGame(GameA, Title, false)); break;
            default:
                await File.WriteAllTextAsync(SnapshotPath, $$"""
                {"schemaVersion":1,"generatedAtUtc":"2026-11-12T12:00:00Z","games":[
                  {"id":"{{GameA}}","name":"{{Title}}","isInstalled":true},
                  {"id":"invalid","name":"{{Title}}","isInstalled":false}
                ]}
                """);
                break;
        }

        var result = command.Invoke();

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        Assert.Contains("refresh the catalog", Assert.IsAssignableFrom<IToastArgs>(result.Args).Message);
        Assert.Empty(_dispatched);
        Assert.DoesNotContain(oldItem, GameItems(provider));
        if (change is "missing" or "malformed" or "stale" or "removed" or "partial")
            Assert.Empty(GameItems(provider));
    }

    [Fact]
    public async Task NewEqualTitleDoesNotRetargetARetainedSelectionAndRefreshAddsBothDisambiguatedChoices()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        using var provider = CreateProvider(adapter);
        await provider.RefreshCatalogAsync();
        var retained = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        await PublishAsync(new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, false));

        Assert.Equal(CommandResultKind.Dismiss, retained.Invoke().Kind);
        Assert.Equal($"playnite://playnite/start/{GameA}", Assert.Single(_dispatched).FileName);
        await provider.RefreshCatalogAsync(forceRefresh: true);

        Assert.Equal(2, GameItems(provider).Length);
        Assert.All(GameItems(provider), item => Assert.Contains(" · ID ", item.Subtitle));
        Assert.Equal(CommandResultKind.ShowToast, retained.Invoke().Kind);
        Assert.Single(_dispatched);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupersededReadOrFailureCannotOverwriteTheNewerRootCatalog(bool failOlder)
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var newer = await (await CreateCatalogAsync(new SnapshotGame(GameB, OtherTitle, true))).GetAsync();
        var readStarted = Signal();
        var olderRead = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new StubCatalog(healthy)
        {
            Read = _ => { readStarted.TrySetResult(true); return olderRead.Task; },
            Refresh = _ => Task.FromResult(newer),
        };
        using var provider = CreateProvider(catalog);
        var older = provider.RefreshCatalogAsync();
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await provider.RefreshCatalogAsync(forceRefresh: true);
        if (failOlder) olderRead.SetException(new IOException("Synthetic obsolete read failure"));
        else olderRead.SetResult(healthy);
        await older.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OtherTitle, Assert.Single(GameItems(provider)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRevalidationSupersedesAnOlderReadRatherThanRestoringUnsafeCommands(bool throwOnRefresh)
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        File.Delete(SnapshotPath);
        var unavailable = await new PlayniteCatalogAdapter(SnapshotPath, _time).GetAsync();
        var catalog = new StubCatalog(healthy);
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        var command = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        var entered = Signal();
        var oldRead = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Read = _ => { entered.TrySetResult(true); return oldRead.Task; };
        catalog.Refresh = _ => throwOnRefresh
            ? Task.FromException<CatalogReadResult>(new IOException("Synthetic revalidation failure"))
            : Task.FromResult(unavailable);
        var older = provider.RefreshCatalogAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var invocation = Task.Run(() => command.Invoke());
            Assert.Equal(CommandResultKind.ShowToast, (await invocation.WaitAsync(TimeSpan.FromSeconds(5))).Kind);
            Assert.Equal(_navigation, provider.TopLevelCommands());
            Assert.Empty(_dispatched);
        }
        finally { oldRead.TrySetResult(healthy); }
        await older.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(_navigation, provider.TopLevelCommands());
    }

    [Fact]
    public async Task CatalogReplacementWhileInvocationIsRevalidatingPreventsDispatch()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var newer = await (await CreateCatalogAsync(new SnapshotGame(GameB, OtherTitle, true))).GetAsync();
        var entered = Signal();
        var revalidation = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshes = 0;
        var catalog = new StubCatalog(healthy)
        {
            Refresh = _ =>
            {
                if (Interlocked.Increment(ref refreshes) != 1) return Task.FromResult(newer);
                entered.TrySetResult(true);
                return revalidation.Task;
            },
        };
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        var command = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        var invocation = Task.Run(() => command.Invoke());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await provider.RefreshCatalogAsync(forceRefresh: true);
        revalidation.SetResult(healthy);

        Assert.Equal(CommandResultKind.ShowToast, (await invocation.WaitAsync(TimeSpan.FromSeconds(5))).Kind);
        Assert.Empty(_dispatched);
        Assert.Equal(OtherTitle, Assert.Single(GameItems(provider)).Title);
    }

    [Fact]
    public async Task OverlappingInvocationsOfOneRootCommandCannotDispatchTwice()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var entered = Signal();
        var revalidation = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new StubCatalog(healthy)
        {
            Refresh = _ => { entered.TrySetResult(true); return revalidation.Task; },
        };
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        var command = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        var first = Task.Run(() => command.Invoke());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CommandResultKind.ShowToast, command.Invoke().Kind);
        revalidation.SetResult(healthy);

        Assert.Equal(CommandResultKind.Dismiss, (await first.WaitAsync(TimeSpan.FromSeconds(5))).Kind);
        Assert.Single(_dispatched);
    }

    [Fact]
    public async Task ReadFailureWithdrawsGamesButPreservesNavigationAndCanRecover()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var catalog = new StubCatalog(healthy);
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        catalog.Read = _ => Task.FromException<CatalogReadResult>(new IOException("Synthetic read failure"));

        await provider.RefreshCatalogAsync();
        Assert.Equal(_navigation, provider.TopLevelCommands());
        catalog.Read = _ => Task.FromResult(healthy);
        await provider.RefreshCatalogAsync();
        Assert.Equal(Title, Assert.Single(GameItems(provider)).Title);
    }

    [Fact]
    public async Task PeriodicRefreshCoalescesSlowReadsAndDisposalStopsTheTimer()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var entered = Signal();
        var read = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var catalog = new StubCatalog(healthy)
        {
            Read = _ => { Interlocked.Increment(ref reads); entered.TrySetResult(true); return read.Task; },
        };
        using var provider = CreateProvider(catalog, startBackgroundRefresh: true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var timer = Assert.IsType<ManualTimer>(_time.Timer);
        Assert.Equal(PlayniteCmdPalCommandsProvider.CatalogPollInterval, timer.Period);
        timer.Tick();
        timer.Tick();
        var initial = provider.RefreshCatalogAsync();
        read.SetResult(healthy);
        await initial.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, reads);

        var secondEntered = Signal();
        var secondRead = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Read = _ => { Interlocked.Increment(ref reads); secondEntered.TrySetResult(true); return secondRead.Task; };
        timer.Tick();
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = provider.RefreshCatalogAsync();
        secondRead.SetResult(healthy);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, reads);
        provider.Dispose();
        timer.Tick();
        await provider.RefreshCatalogAsync();
        Assert.True(timer.IsDisposed);
        Assert.Equal(2, reads);
        Assert.Empty(provider.TopLevelCommands());
    }

    [Fact]
    public async Task RefreshContextActionKeepsThePaletteOpenAndBypassesTheCatalogCache()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var newer = await (await CreateCatalogAsync(new SnapshotGame(GameB, OtherTitle, true))).GetAsync();
        var entered = Signal();
        var read = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new StubCatalog(healthy)
        {
            Refresh = _ => { entered.TrySetResult(true); return read.Task; },
        };
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        var context = Assert.IsType<CommandContextItem>(provider.CreateRefreshContextItem());
        var command = Assert.IsAssignableFrom<InvokableCommand>(context.Command);

        Assert.Equal("Refresh game commands", command.Name);
        Assert.Equal(CommandResultKind.KeepOpen, command.Invoke().Kind);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var refresh = provider.RefreshCatalogAsync();
        read.SetResult(newer);
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OtherTitle, Assert.Single(GameItems(provider)).Title);
    }

    [Fact]
    public async Task DisposingDuringAReadPreventsLatePublicationAndInvalidatesRetainedCommands()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var catalog = new StubCatalog(healthy);
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        var command = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        var entered = Signal();
        var read = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Read = _ => { entered.TrySetResult(true); return read.Task; };
        var pending = provider.RefreshCatalogAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var notifications = 0;
        provider.ItemsChanged += (_, _) => Interlocked.Increment(ref notifications);

        provider.Dispose();
        read.SetResult(healthy);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(provider.TopLevelCommands());
        Assert.Equal(0, notifications);
        Assert.Equal(CommandResultKind.ShowToast, command.Invoke().Kind);
        Assert.Empty(_dispatched);
    }

    [Fact]
    public async Task DisposingDuringInvocationRevalidationPreventsDispatchEvenIfTheReadIgnoresCancellation()
    {
        var healthy = await (await CreateCatalogAsync(new SnapshotGame(GameA, Title, true))).GetAsync();
        var entered = Signal();
        var revalidation = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new StubCatalog(healthy)
        {
            Refresh = _ => { entered.TrySetResult(true); return revalidation.Task; },
        };
        using var provider = CreateProvider(catalog);
        await provider.RefreshCatalogAsync();
        var command = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command);
        var invocation = Task.Run(() => command.Invoke());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        provider.Dispose();
        revalidation.SetResult(healthy);

        Assert.Equal(CommandResultKind.ShowToast, (await invocation.WaitAsync(TimeSpan.FromSeconds(5))).Kind);
        Assert.Empty(_dispatched);
    }

    [Fact]
    public async Task LaunchFailurePreservesTheExistingActionableToast()
    {
        using var provider = CreateProvider(await CreateCatalogAsync(new SnapshotGame(GameA, Title, true)),
            launch: id => new LaunchGameCommand(id, _ => throw new Win32Exception("Synthetic failure")).Invoke());
        await provider.RefreshCatalogAsync();

        var result = Assert.IsAssignableFrom<InvokableCommand>(Assert.Single(GameItems(provider)).Command).Invoke();

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        Assert.Contains("Windows couldn't open Playnite", Assert.IsAssignableFrom<IToastArgs>(result.Args).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootPublicationAndLaunchDoNotWaitForTheSavedIconAndTheIconNotifiesWithoutRebuilding(bool installed)
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, installed,
            coverArtworkId: CoverId, iconArtworkId: IconId));
        var entered = Signal();
        var release = Signal();
        var resolved = new ConcurrentQueue<string>();
        var loader = Loader(id => { resolved.Enqueue(id); return IconPath; }, async (_, token) =>
        {
            entered.TrySetResult(true);
            await release.Task.WaitAsync(token);
            return true;
        });
        using var provider = CreateProvider(adapter, loader);
        var rootNotifications = 0;
        provider.ItemsChanged += (_, _) => Interlocked.Increment(ref rootNotifications);
        await provider.RefreshCatalogAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var cached = provider.TopLevelCommands();
        var item = Assert.IsAssignableFrom<CommandItem>(Assert.Single(GameItems(provider)));
        var iconChanged = Signal();
        item.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(item.Icon) && !ReferenceEquals(item.Icon, ArtworkIconLoader.FallbackIcon))
            {
                // A COM listener can synchronously query the provider on another
                // thread. Property notifications must not hold its getter lock.
                var reentered = Task.Run(provider.TopLevelCommands)
                    .WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                iconChanged.TrySetResult(ReferenceEquals(cached, reentered));
            }
        };
        Assert.Same(ArtworkIconLoader.FallbackIcon, item.Icon);
        Assert.False(provider.ArtworkUpdate.IsCompleted);

        var result = await Task.Run(() => Assert.IsAssignableFrom<InvokableCommand>(item.Command).Invoke())
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(CommandResultKind.Dismiss, result.Kind);
        Assert.Single(_dispatched);
        release.TrySetResult(true);
        await provider.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(iconChanged.Task.IsCompletedSuccessfully);
        Assert.True(await iconChanged.Task);
        Assert.Equal(new Uri(IconPath).AbsoluteUri, item.Icon!.Light.Icon);
        Assert.Equal(new[] { IconId }, resolved.ToArray());
        Assert.Same(cached, provider.TopLevelCommands());
        Assert.Equal(1, rootNotifications);
    }

    [Theory]
    [InlineData("missing-icon")]
    [InlineData("missing-cache")]
    [InlineData("corrupt-icon")]
    [InlineData("read-failure")]
    public async Task ArtworkFailuresKeepTheNeutralSymbolAndNeverUseCoverOrPreventLaunch(string failure)
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true,
            coverArtworkId: CoverId, iconArtworkId: failure == "missing-icon" ? null : IconId));
        var resolved = new ConcurrentQueue<string>();
        var loader = Loader(id =>
        {
            resolved.Enqueue(id);
            if (failure == "read-failure") throw new IOException("Synthetic artwork failure");
            return failure == "missing-cache" ? null : IconPath;
        }, (_, _) => Task.FromResult(failure != "corrupt-icon"));
        using var provider = CreateProvider(adapter, loader);
        await provider.RefreshCatalogAsync();
        await provider.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        var item = Assert.Single(GameItems(provider));

        Assert.Same(ArtworkIconLoader.FallbackIcon, item.Icon);
        Assert.Equal(failure == "missing-icon" ? Array.Empty<string>() : new[] { IconId }, resolved.ToArray());
        Assert.Equal(CommandResultKind.Dismiss, Assert.IsAssignableFrom<InvokableCommand>(item.Command).Invoke().Kind);
        Assert.Equal($"playnite://playnite/start/{GameA}", Assert.Single(_dispatched).FileName);
    }

    [Fact]
    public async Task UnchangedCatalogRetriesMissingArtworkAndExplicitRefreshInvalidatesTheSuccessfulCache()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true, iconArtworkId: IconId));
        var attempts = 0;
        var loader = Loader(_ => Interlocked.Increment(ref attempts) switch
        {
            1 => null,
            2 => IconPath,
            _ => OtherIconPath,
        }, (_, _) => Task.FromResult(true));
        using var provider = CreateProvider(adapter, loader);
        await provider.RefreshCatalogAsync();
        await provider.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        var cached = provider.TopLevelCommands();
        var item = Assert.Single(GameItems(provider));
        Assert.Same(ArtworkIconLoader.FallbackIcon, item.Icon);

        await provider.RefreshCatalogAsync();
        await provider.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new Uri(IconPath).AbsoluteUri, item.Icon.Light.Icon);
        await provider.RefreshCatalogAsync(forceRefresh: true);
        await provider.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(cached, provider.TopLevelCommands());
        Assert.Equal(3, attempts);
        Assert.Equal(new Uri(OtherIconPath).AbsoluteUri, item.Icon.Light.Icon);
    }

    [Fact]
    public async Task ObsoleteImageCannotOverwriteTheNewCatalogEvenWhenItsDecoderFinishesLate()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true, iconArtworkId: IconId));
        var entered = Signal();
        var release = Signal();
        var oldDecodeFinished = Signal();
        var loader = Loader(id => id == IconId ? IconPath : OtherIconPath, async (path, _) =>
        {
            if (path == IconPath)
            {
                entered.TrySetResult(true);
                await release.Task; // Simulate an underlying decoder that cannot abort.
                oldDecodeFinished.TrySetResult(true);
            }
            return true;
        });
        using var provider = CreateProvider(adapter, loader);
        await provider.RefreshCatalogAsync();
        var oldArtwork = provider.ArtworkUpdate;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await PublishAsync(new SnapshotGame(GameB, OtherTitle, true, iconArtworkId: OtherIconId));
            await provider.RefreshCatalogAsync(forceRefresh: true);
            await provider.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
            await oldArtwork.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new Uri(OtherIconPath).AbsoluteUri, Assert.Single(GameItems(provider)).Icon.Light.Icon);
        }
        finally { release.TrySetResult(true); }
        await oldDecodeFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var current = Assert.Single(GameItems(provider));
        Assert.Equal(OtherTitle, current.Title);
        Assert.Equal(new Uri(OtherIconPath).AbsoluteUri, current.Icon.Light.Icon);
    }

    [Fact]
    public async Task DisposingCancelsArtworkWithoutALateIconNotification()
    {
        var entered = Signal();
        var cancelled = Signal();
        var loader = Loader(_ => IconPath, async (_, token) =>
        {
            entered.TrySetResult(true);
            using var registration = token.Register(() => cancelled.TrySetResult(true));
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        });
        using var provider = CreateProvider(await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true, iconArtworkId: IconId)), loader);
        await provider.RefreshCatalogAsync();
        var item = Assert.IsAssignableFrom<CommandItem>(Assert.Single(GameItems(provider)));
        var artwork = provider.ArtworkUpdate;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var notifications = 0;
        item.PropChanged += (_, args) => { if (args.PropertyName == nameof(item.Icon)) Interlocked.Increment(ref notifications); };

        provider.Dispose();
        await artwork.WaitAsync(TimeSpan.FromSeconds(5));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, notifications);
        Assert.Same(ArtworkIconLoader.FallbackIcon, item.Icon);
    }

    private PlayniteCmdPalCommandsProvider CreateProvider(
        IReadOnlyPlayniteCatalog catalog,
        ArtworkIconLoader? artwork = null,
        Func<string, CommandResult>? launch = null,
        bool startBackgroundRefresh = false) =>
        new(catalog, _navigation,
            artwork ?? Loader(_ => null, (_, _) => Task.FromResult(false)),
            launch ?? (id => new LaunchGameCommand(id, _dispatched.Add).Invoke()),
            startBackgroundRefresh, _time);

    private ICommandItem[] GameItems(PlayniteCmdPalCommandsProvider provider) =>
        provider.TopLevelCommands().Skip(_navigation.Length).ToArray();

    private static ArtworkIconLoader Loader(Func<string, string?> resolve, Func<string, CancellationToken, Task<bool>> decode) =>
        new(resolve, decode, fileExists: _ => true);

    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task<PlayniteCatalogAdapter> CreateCatalogAsync(params SnapshotGame[] games)
    {
        await PublishAsync(games);
        return new PlayniteCatalogAdapter(SnapshotPath, _time);
    }

    private Task PublishAsync(params SnapshotGame[] games)
    {
        Directory.CreateDirectory(_directory);
        var schemaVersion = games.Any(game => game.IconArtworkId is not null || game.CoverArtworkId is not null || game.LastActivity.HasValue)
            ? SnapshotLimits.SchemaVersion : SnapshotLimits.LegacySchemaVersion;
        return SnapshotFilePublisher.PublishAsync(SnapshotPath, new CatalogSnapshot(_time.GetUtcNow(), games, schemaVersion),
            CancellationToken.None, schemaVersion, includeLastActivity: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class StubCatalog : IReadOnlyPlayniteCatalog
    {
        public StubCatalog(CatalogReadResult result)
        {
            Read = _ => Task.FromResult(result);
            Refresh = _ => Task.FromResult(result);
        }
        public Func<CancellationToken, Task<CatalogReadResult>> Read { get; set; }
        public Func<CancellationToken, Task<CatalogReadResult>> Refresh { get; set; }
        public Task<CatalogReadResult> GetAsync(CancellationToken cancellationToken = default) => Read(cancellationToken);
        public Task<CatalogReadResult> RefreshAsync(CancellationToken cancellationToken = default) => Refresh(cancellationToken);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public ManualTimer? Timer { get; private set; }
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Timer = new ManualTimer(callback, state, dueTime, period);
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        public TimeSpan DueTime { get; private set; } = dueTime;
        public TimeSpan Period { get; private set; } = period;
        public bool IsDisposed { get; private set; }
        public void Tick() { if (!IsDisposed) callback(state); }
        public bool Change(TimeSpan newDueTime, TimeSpan newPeriod)
        {
            if (IsDisposed) return false;
            DueTime = newDueTime;
            Period = newPeriod;
            return true;
        }
        public void Dispose() => IsDisposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
