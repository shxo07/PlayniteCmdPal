using PlayniteCmdPal.Core;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class PlayniteCatalogAdapterTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z"));

    public PlayniteCatalogAdapterTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "catalog-v1.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task MissingFileWithoutCacheIsUnavailableNotAnEmptyLibrary()
    {
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        var result = await adapter.GetAsync();
        Assert.False(result.IsAvailable);
        Assert.Empty(result.Games);
        Assert.Equal(CatalogErrorCode.Missing, result.Issue!.Code);
    }

    [Fact]
    public async Task CanceledReadDoesNotStartOrJoinFileWork()
    {
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.GetAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.RefreshAsync(cancellation.Token));
        Assert.False((await adapter.GetAsync()).IsAvailable);
    }

    [Fact]
    public async Task ValidSnapshotMapsToCoreGameEntries()
    {
        await PublishAsync(NewSnapshot(("Halo", true, "Steam", "PC")));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        var result = await adapter.GetAsync();

        Assert.True(result.IsAvailable);
        Assert.Null(result.Issue);
        Assert.False(result.IsStale);
        Assert.False(result.IsFromCache);
        var game = Assert.Single(result.Games);
        Assert.Equal(GameA, game.GameId);
        Assert.Equal("Halo", game.Title);
        Assert.Equal("Steam", game.Source);
        Assert.True(game.IsInstalled);
        Assert.Equal("PC", game.Platform);
        Assert.Null(game.IconReference);
        Assert.Single(GameSearch.Search(result.Games, "halo"));
    }

    [Fact]
    public async Task EmptyValidSnapshotIsAnAvailableEmptyLibrary()
    {
        await PublishAsync(new CatalogSnapshot(_time.GetUtcNow(), Array.Empty<SnapshotGame>()));
        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Games);
        Assert.Null(result.Issue);
    }

    [Fact]
    public async Task SnapshotAtStaleBoundaryRemainsUsableButIsMarkedStale()
    {
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        Assert.False((await adapter.GetAsync()).IsStale);

        _time.Advance(PlayniteCatalogAdapter.StaleAfter);
        var stale = await adapter.RefreshAsync();
        Assert.True(stale.IsAvailable);
        Assert.True(stale.IsStale);
        Assert.Single(stale.Games);
    }

    [Fact]
    public async Task StalenessIsRecomputedEvenWhenTheRefreshIntervalReturnsCachedData()
    {
        // Load a snapshot one second below the stale threshold, then cross the threshold
        // while still inside the 30-second revalidation interval.
        var generatedAt = _time.GetUtcNow() - PlayniteCatalogAdapter.StaleAfter + TimeSpan.FromSeconds(1);
        await PublishAsync(new CatalogSnapshot(generatedAt, new[] { new SnapshotGame(GameA, "Halo", false) }));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        var beforeBoundary = await adapter.GetAsync();
        Assert.False(beforeBoundary.IsFromCache);
        Assert.False(beforeBoundary.IsStale);

        _time.Advance(TimeSpan.FromSeconds(1));
        var atBoundary = await adapter.GetAsync();
        Assert.True(atBoundary.IsFromCache);
        Assert.True(atBoundary.IsStale);
        Assert.Single(atBoundary.Games);
    }

    [Fact]
    public async Task MissingFileAfterValidReadRetainsLastKnownGoodAndSurfacesError()
    {
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        await adapter.GetAsync();
        File.Delete(_path);

        var result = await adapter.RefreshAsync();
        Assert.True(result.IsAvailable);
        Assert.True(result.IsFromCache);
        Assert.Single(result.Games);
        Assert.Equal(CatalogErrorCode.Missing, result.Issue!.Code);
    }

    [Theory]
    [InlineData("not json", CatalogErrorCode.Malformed)]
    [InlineData("""{"schemaVersion":2,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""", CatalogErrorCode.UnsupportedSchema)]
    public async Task InvalidLaterFileNeverReplacesLastKnownGood(string json, CatalogErrorCode expected)
    {
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        await adapter.GetAsync();
        await File.WriteAllTextAsync(_path, json);

        var result = await adapter.RefreshAsync();
        Assert.True(result.IsAvailable);
        Assert.Single(result.Games);
        Assert.Equal(expected, result.Issue!.Code);
    }

    [Fact]
    public async Task InvalidFirstFileIsUnavailable()
    {
        await File.WriteAllTextAsync(_path, "not json");
        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();
        Assert.False(result.IsAvailable);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public async Task ExplicitRefreshRecoversAfterMalformedFileIsReplaced()
    {
        await File.WriteAllTextAsync(_path, "not json");
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        var failed = await adapter.GetAsync();
        Assert.False(failed.IsAvailable);
        Assert.Equal(CatalogErrorCode.Malformed, failed.Issue!.Code);

        await PublishAsync(NewSnapshot(("Halo", true, "Steam", "PC")));
        var recovered = await adapter.RefreshAsync();
        Assert.True(recovered.IsAvailable);
        Assert.Null(recovered.Issue);
        Assert.False(recovered.IsFromCache);
        Assert.Equal(GameA, Assert.Single(recovered.Games).GameId);
    }

    [Fact]
    public async Task OversizedFileIsRejected()
    {
        using (var stream = new FileStream(_path, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(SnapshotLimits.MaxBytes + 1);
        }

        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();
        Assert.False(result.IsAvailable);
        Assert.Equal(CatalogErrorCode.TooLarge, result.Issue!.Code);
    }

    [Fact]
    public async Task GetUsesRefreshIntervalButExplicitRefreshBypassesIt()
    {
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        Assert.Equal("Halo", Assert.Single((await adapter.GetAsync()).Games).Title);

        await PublishAsync(NewSnapshot(("Halo 2", false, null, null)));
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal("Halo", Assert.Single((await adapter.GetAsync()).Games).Title);

        var refreshed = await adapter.RefreshAsync();
        Assert.Equal("Halo 2", Assert.Single(refreshed.Games).Title);

        await PublishAsync(NewSnapshot(("Halo 3", false, null, null)));
        _time.Advance(PlayniteCatalogAdapter.RefreshInterval);
        Assert.Equal("Halo 3", Assert.Single((await adapter.GetAsync()).Games).Title);
    }

    [Fact]
    public async Task ReconnectsAfterMissingFileIsPublished()
    {
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        Assert.False((await adapter.GetAsync()).IsAvailable);
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var result = await adapter.RefreshAsync();
        Assert.True(result.IsAvailable);
        Assert.Null(result.Issue);
        Assert.Single(result.Games);
    }

    [Fact]
    public async Task ValidEmptySnapshotReplacesPreviousLibrary()
    {
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        Assert.Single((await adapter.GetAsync()).Games);
        await PublishAsync(new CatalogSnapshot(_time.GetUtcNow(), Array.Empty<SnapshotGame>()));
        var result = await adapter.RefreshAsync();
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Games);
    }

    [Fact]
    public async Task ConcurrentReadersAllReceiveAValidCatalog()
    {
        await PublishAsync(NewSnapshot(("Halo", false, null, null)));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => adapter.RefreshAsync()));
        Assert.All(results, result => Assert.Single(result.Games));
        // Each actual file read maps a new array; shared references demonstrate coalescing.
        Assert.All(results, result => Assert.Same(results[0].Games, result.Games));
    }

    [Fact]
    public async Task SkippedInvalidRecordsAreReported()
    {
        await File.WriteAllTextAsync(_path, $$"""
        {"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[
          {"id":"{{GameA}}","name":"Halo","isInstalled":false},
          {"id":"bad","name":"Bad","isInstalled":false}
        ]}
        """);
        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();
        Assert.True(result.IsAvailable);
        Assert.Single(result.Games);
        Assert.Equal(1, result.SkippedRecordCount);
    }

    private CatalogSnapshot NewSnapshot(params (string Name, bool Installed, string? Source, string? Platform)[] games) =>
        new(_time.GetUtcNow(), games.Select((game, index) => new SnapshotGame(index == 0 ? GameA : GameB, game.Name, game.Installed, game.Source, game.Platform)).ToArray());

    private Task PublishAsync(CatalogSnapshot snapshot) => SnapshotFilePublisher.PublishAsync(_path, snapshot);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
