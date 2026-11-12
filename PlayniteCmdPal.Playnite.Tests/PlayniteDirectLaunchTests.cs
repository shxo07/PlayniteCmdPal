using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class PlayniteDirectLaunchTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string Title = "Orbit Rally 16";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();
    private string SnapshotPath => Path.Combine(_directory, "catalog.json");

    [Fact]
    public async Task HealthySnapshotProvidesAndRevalidatesTheSelectedStableId()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true, "Store A", "PC"));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));

        var game = Assert.Single(await service.GetCandidatesAsync());

        Assert.Equal(new GameEntry(GameA, Title, "Store A", true, Platform: "PC"), game);
        Assert.True(await service.RevalidateAsync(game));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("replaced")]
    [InlineData("source")]
    [InlineData("platform")]
    [InlineData("installation")]
    public async Task InvocationRefreshesInsideTheCacheIntervalAndNeverRetargetsOrUsesChangedMetadata(string change)
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        var displayed = Assert.Single(await service.GetCandidatesAsync());

        var games = change switch
        {
            "removed" => Array.Empty<SnapshotGame>(),
            "renamed" => new[] { new SnapshotGame(GameA, "Orbit Rally 17", true) },
            "replaced" => new[] { new SnapshotGame(GameB, Title, true) },
            "source" => new[] { new SnapshotGame(GameA, Title, true, "Other store") },
            "platform" => new[] { new SnapshotGame(GameA, Title, true, platform: "Other platform") },
            _ => new[] { new SnapshotGame(GameA, Title, false) },
        };
        await PublishAsync(games);

        // The normal cache still holds the displayed game; invocation must bypass it.
        Assert.Equal(displayed, Assert.Single(await service.GetCandidatesAsync()));
        Assert.False(await service.RevalidateAsync(displayed));
    }

    [Fact]
    public async Task EqualTitlesCanRevalidateEachExplicitIdAndNewSameTitleIdsDoNotRetargetTheSelection()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true, "Store A"));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        var displayed = Assert.Single(await service.GetCandidatesAsync());
        await PublishAsync(new SnapshotGame(GameA, Title, true, "Store A"),
            new SnapshotGame(GameB, Title, false, "Store B"));

        Assert.Single(await service.GetCandidatesAsync());
        var games = await service.GetCandidatesAsync(forceRefresh: true);
        Assert.Equal(new[] { GameA, GameB }, games.Select(game => game.GameId));
        Assert.True(await service.RevalidateAsync(displayed));
        Assert.True(await service.RevalidateAsync(games[1]));

        await PublishAsync(new SnapshotGame(GameB, Title, false, "Store B"));
        Assert.False(await service.RevalidateAsync(displayed));
        Assert.True(await service.RevalidateAsync(games[1]));
    }

    [Fact]
    public async Task OptionalArtworkAndLastActivityChangesDoNotInvalidateTheSelectedGame()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        var displayed = Assert.Single(await service.GetCandidatesAsync());
        await PublishAsync(new SnapshotGame(GameA, Title, true,
            iconArtworkId: new string('a', 64), coverArtworkId: new string('b', 64),
            lastActivity: new DateTime(2026, 11, 12, 11, 0, 0, DateTimeKind.Utc)));

        Assert.True(await service.RevalidateAsync(displayed));
    }

    [Fact]
    public async Task StaleSnapshotRemainsBrowsableButCannotAuthorizeRootCommands()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var adapter = new PlayniteCatalogAdapter(SnapshotPath, _time);
        var service = new PlayniteDirectLaunch(adapter);
        var displayed = Assert.Single(await service.GetCandidatesAsync());

        _time.Advance(PlayniteCatalogAdapter.StaleAfter);

        Assert.Empty(await service.GetCandidatesAsync());
        Assert.False(await service.RevalidateAsync(displayed));
        Assert.Single((await adapter.GetAsync()).Games);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRefreshDoesNotDirectLaunchFromLastKnownGood(bool missing)
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var adapter = new PlayniteCatalogAdapter(SnapshotPath, _time);
        var service = new PlayniteDirectLaunch(adapter);
        var displayed = Assert.Single(await service.GetCandidatesAsync());
        if (missing) File.Delete(SnapshotPath);
        else await File.WriteAllTextAsync(SnapshotPath, "not json");

        Assert.False(await service.RevalidateAsync(displayed));
        Assert.Empty(await service.GetCandidatesAsync());
        Assert.Single((await adapter.GetAsync()).Games);
    }

    [Theory]
    [InlineData("not-an-id", "Orbit Rally 16")]
    [InlineData(GameA, "Conflicting duplicate record")]
    public async Task PartialOrConflictingCatalogCannotAuthorizeDirectCommands(string invalidId, string invalidTitle)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SnapshotPath, $$"""
        {"schemaVersion":1,"generatedAtUtc":"2026-11-12T12:00:00Z","games":[
          {"id":"{{GameA}}","name":"{{Title}}","isInstalled":true},
          {"id":"{{invalidId}}","name":"{{invalidTitle}}","isInstalled":false}
        ]}
        """);
        var adapter = new PlayniteCatalogAdapter(SnapshotPath, _time);
        var service = new PlayniteDirectLaunch(adapter);

        Assert.Equal(1, (await adapter.GetAsync()).SkippedRecordCount);
        Assert.Empty(await service.GetCandidatesAsync());
        Assert.False(await service.RevalidateAsync(new GameEntry(GameA, Title, IsInstalled: true)));
    }

    [Fact]
    public async Task UnavailableEmptyAndMissingIdsDoNotAuthorizeACommand()
    {
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Empty(await service.GetCandidatesAsync());
        Assert.False(await service.RevalidateAsync(new GameEntry(GameA, Title)));

        await PublishAsync();
        Assert.Empty(await service.GetCandidatesAsync(forceRefresh: true));
        await PublishAsync(new SnapshotGame(GameB, Title, true));
        Assert.False(await service.RevalidateAsync(new GameEntry(GameA, Title, IsInstalled: true)));
    }

    [Fact]
    public async Task RootCatalogCanRecoverAfterBeingUnavailableWithoutAProviderReload()
    {
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Empty(await service.GetCandidatesAsync());
        await PublishAsync(new SnapshotGame(GameA, Title, true));

        Assert.Equal(GameA, Assert.Single(await service.GetCandidatesAsync(forceRefresh: true)).GameId);
    }

    [Fact]
    public async Task CancelledRequestsDoNotReadOrAuthorizeACommand()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetCandidatesAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RevalidateAsync(
            new GameEntry(GameA, Title, IsInstalled: true), cancellation.Token));
    }

    private Task PublishAsync(params SnapshotGame[] games) =>
        SnapshotFilePublisher.PublishAsync(SnapshotPath, new CatalogSnapshot(_time.GetUtcNow(), games, SnapshotLimits.SchemaVersion),
            CancellationToken.None, SnapshotLimits.SchemaVersion, includeLastActivity: true);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
