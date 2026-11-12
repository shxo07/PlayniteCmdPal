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
    private string SnapshotPath => Path.Combine(_directory, "catalog-v1.json");

    [Fact]
    public async Task HealthySnapshotCanMatchAndRevalidateTheSameId()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));

        var match = await service.FindMatchAsync("  ORBIT RALLY 16  ");

        Assert.Equal(GameA, match?.GameId);
        Assert.Equal(Title, match?.Title);
        Assert.True(await service.RevalidateAsync(Title, GameA));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("replaced")]
    [InlineData("ambiguous")]
    public async Task InvocationRefreshesInsideTheCacheIntervalAndNeverRetargets(string change)
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var adapter = new PlayniteCatalogAdapter(SnapshotPath, _time);
        var service = new PlayniteDirectLaunch(adapter);
        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);

        var games = change switch
        {
            "removed" => Array.Empty<SnapshotGame>(),
            "renamed" => new[] { new SnapshotGame(GameA, "Orbit Rally 17", true) },
            "replaced" => new[] { new SnapshotGame(GameB, Title, true) },
            _ => new[] { new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, false) },
        };
        await PublishAsync(games);

        // The normal cache still holds A; revalidation must explicitly bypass it.
        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task StaleSnapshotRemainsBrowsableButCannotDirectLaunch()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var adapter = new PlayniteCatalogAdapter(SnapshotPath, _time);
        var service = new PlayniteDirectLaunch(adapter);
        Assert.NotNull(await service.FindMatchAsync(Title));

        _time.Advance(PlayniteCatalogAdapter.StaleAfter);

        Assert.Null(await service.FindMatchAsync(Title));
        Assert.False(await service.RevalidateAsync(Title, GameA));
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
        Assert.NotNull(await service.FindMatchAsync(Title));
        if (missing)
        {
            File.Delete(SnapshotPath);
        }
        else
        {
            await File.WriteAllTextAsync(SnapshotPath, "not json");
        }

        Assert.False(await service.RevalidateAsync(Title, GameA));
        Assert.Null(await service.FindMatchAsync(Title));
        Assert.Single((await adapter.GetAsync()).Games);
    }

    [Theory]
    [InlineData("not-an-id", "Orbit Rally 16")]
    [InlineData(GameA, "Conflicting duplicate record")]
    public async Task PartialCatalogCannotConcealAnotherMatch(string invalidId, string invalidTitle)
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
        Assert.Null(await service.FindMatchAsync(Title));
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task UnavailableCatalogAndNonmatchingQueriesDoNotProduceACandidate()
    {
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Null(await service.FindMatchAsync(Title));

        await PublishAsync(new SnapshotGame(GameA, Title, true));
        service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Null(await service.FindMatchAsync(null));
        Assert.Null(await service.FindMatchAsync("Unknown"));
        Assert.Null(await service.FindMatchAsync("Orbit"));
    }

    private Task PublishAsync(params SnapshotGame[] games) =>
        SnapshotFilePublisher.PublishAsync(SnapshotPath, new CatalogSnapshot(_time.GetUtcNow(), games));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
