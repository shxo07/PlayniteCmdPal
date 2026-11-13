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
    public async Task HealthyCatalogMatchesAnInstalledExactTitleAndRevalidatesItsStableId()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true, "Store A", "PC"));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));

        Assert.Null(await service.FindMatchAsync("Orbit"));
        var match = await service.FindMatchAsync("  ORBIT RALLY 16  ");

        Assert.Equal(new GameEntry(GameA, Title, "Store A", true, Platform: "PC"), match);
        Assert.True(await service.RevalidateAsync(Title, GameA));
        Assert.False(await service.RevalidateAsync(Title, GameB));
    }

    [Fact]
    public async Task UninstalledEntriesAreExcludedAndDoNotMakeAnInstalledDuplicateAmbiguous()
    {
        await PublishAsync(
            new SnapshotGame(GameA, Title, true, "Store A"),
            new SnapshotGame(GameB, Title, false, "Store B"));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));

        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);
        Assert.True(await service.RevalidateAsync(Title, GameA));

        await PublishAsync(new SnapshotGame(GameB, Title, false, "Store B"));
        Assert.Null(await service.FindMatchAsync(Title, forceRefresh: true));
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task SameTitleOwnedByMultipleInstalledGamesIsSuppressed()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));

        Assert.Null(await service.FindMatchAsync(Title));
        Assert.False(await service.RevalidateAsync(Title, GameA));
        Assert.False(await service.RevalidateAsync(Title, GameB));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("replaced")]
    [InlineData("uninstalled")]
    [InlineData("ambiguous")]
    public async Task InvocationForceRefreshNeverRetargetsChangedOrUnavailableMatches(string change)
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);

        var games = change switch
        {
            "removed" => Array.Empty<SnapshotGame>(),
            "renamed" => new[] { new SnapshotGame(GameA, "Orbit Rally 17", true) },
            "replaced" => new[] { new SnapshotGame(GameB, Title, true) },
            "uninstalled" => new[] { new SnapshotGame(GameA, Title, false) },
            _ => new[] { new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, true) },
        };
        await PublishAsync(games);

        // The normal read can still see the adapter cache; invocation bypasses it.
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task OptionalArtworkAndActivityChangesDoNotInvalidateAnInstalledMatch()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);
        await PublishAsync(new SnapshotGame(GameA, Title, true,
            iconArtworkId: new string('a', 64), coverArtworkId: new string('b', 64),
            lastActivity: new DateTime(2026, 11, 12, 11, 0, 0, DateTimeKind.Utc)));

        Assert.True(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task StaleSnapshotCannotAuthorizeOrRevalidateAGlobalFallback()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);

        _time.Advance(PlayniteCatalogAdapter.StaleAfter);

        Assert.Null(await service.FindMatchAsync(Title, forceRefresh: true));
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRefreshDoesNotAuthorizeFromLastKnownGood(bool missing)
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Equal(GameA, (await service.FindMatchAsync(Title))?.GameId);
        if (missing) File.Delete(SnapshotPath);
        else await File.WriteAllTextAsync(SnapshotPath, "not json");

        Assert.False(await service.RevalidateAsync(Title, GameA));
        Assert.Null(await service.FindMatchAsync(Title, forceRefresh: true));
    }

    [Fact]
    public async Task PartialCatalogCannotAuthorizeAnInstalledMatch()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SnapshotPath, $$"""
        {"schemaVersion":1,"generatedAtUtc":"2026-11-12T12:00:00Z","games":[
          {"id":"{{GameA}}","name":"{{Title}}","isInstalled":true},
          {"id":"not-an-id","name":"Unrelated","isInstalled":false}
        ]}
        """);
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));

        Assert.Null(await service.FindMatchAsync(Title));
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task EmptyUnavailableAndMissingIdsDoNotAuthorizeACommand()
    {
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        Assert.Null(await service.FindMatchAsync(Title));
        Assert.False(await service.RevalidateAsync(Title, GameA));

        await PublishAsync();
        Assert.Null(await service.FindMatchAsync(Title, forceRefresh: true));
        await PublishAsync(new SnapshotGame(GameB, Title, true));
        Assert.False(await service.RevalidateAsync(Title, GameA));
    }

    [Fact]
    public async Task CancelledRequestsDoNotReadOrAuthorizeAMatch()
    {
        await PublishAsync(new SnapshotGame(GameA, Title, true));
        var service = new PlayniteDirectLaunch(new PlayniteCatalogAdapter(SnapshotPath, _time));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.FindMatchAsync(Title, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RevalidateAsync(Title, GameA, cancellation.Token));
    }

    private Task PublishAsync(params SnapshotGame[] games)
    {
        Directory.CreateDirectory(_directory);
        var schemaVersion = games.Any(game => game.IconArtworkId is not null || game.CoverArtworkId is not null || game.LastActivity.HasValue)
            ? SnapshotLimits.SchemaVersion : SnapshotLimits.LegacySchemaVersion;
        return SnapshotFilePublisher.PublishAsync(SnapshotPath,
            new CatalogSnapshot(_time.GetUtcNow(), games, schemaVersion),
            CancellationToken.None, schemaVersion, includeLastActivity: true);
    }

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
