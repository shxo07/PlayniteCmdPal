using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;
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
    public async Task LastActivitySurvivesSnapshotAdapterMapping()
    {
        var activity = new DateTime(2026, 10, 30, 14, 25, 0, DateTimeKind.Unspecified);
        await SnapshotFilePublisher.PublishAsync(_path, new CatalogSnapshot(_time.GetUtcNow(), new[] { new SnapshotGame(GameA, "Halo", true, lastActivity: activity) }), CancellationToken.None, schemaVersion: 2, includeLastActivity: true);
        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();
        Assert.Equal(activity, Assert.Single(result.Games).LastActivity);
        Assert.Equal(DateTimeKind.Unspecified, result.Games[0].LastActivity!.Value.Kind);
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
    [InlineData("""{"schemaVersion":3,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""", CatalogErrorCode.UnsupportedSchema)]
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


    [Fact]
    public async Task FallsBackToV1OnlyWhenV2SnapshotIsMissing()
    {
        var v2Path = Path.Combine(_directory, "catalog-v2.json");
        var v1Path = Path.Combine(_directory, "catalog-v1.json");
        await SnapshotFilePublisher.PublishAsync(v1Path,
            new CatalogSnapshot(_time.GetUtcNow(), new[] { new SnapshotGame(GameA, "Legacy Halo", true) }, SnapshotLimits.LegacySchemaVersion));

        var result = await new PlayniteCatalogAdapter(v2Path, _time, v1Path).GetAsync();

        Assert.True(result.IsAvailable);
        Assert.Null(result.Issue);
        Assert.Equal("Legacy Halo", Assert.Single(result.Games).Title);
        Assert.Null(result.Games[0].IconReference);
    }

    [Fact]
    public async Task MalformedV2DoesNotSilentlyFallBackToV1()
    {
        var v2Path = Path.Combine(_directory, "catalog-v2.json");
        var v1Path = Path.Combine(_directory, "catalog-v1.json");
        await File.WriteAllTextAsync(v2Path, "not json");
        await SnapshotFilePublisher.PublishAsync(v1Path,
            new CatalogSnapshot(_time.GetUtcNow(), new[] { new SnapshotGame(GameA, "Stale Legacy Halo", true) }, SnapshotLimits.LegacySchemaVersion));

        var result = await new PlayniteCatalogAdapter(v2Path, _time, v1Path).GetAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public void CatalogVerifierArtworkValidationAcceptsNullOrCanonicalSha256Ids()
    {
        const string validDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        Assert.True(SnapshotValidation.IsArtworkIdOrNull(null));
        Assert.True(SnapshotValidation.IsArtworkIdOrNull(validDigest));
        Assert.False(SnapshotValidation.IsArtworkIdOrNull(validDigest.ToUpperInvariant()));
        Assert.False(SnapshotValidation.IsArtworkIdOrNull(validDigest.Substring(1)));
        Assert.False(SnapshotValidation.IsArtworkIdOrNull(new string('g', 64)));
    }

    [Fact]
    public async Task V2ArtworkIdentifierMapsToTheCoreIconReference()
    {
        const string digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        await File.WriteAllTextAsync(_path, $$"""{"schemaVersion":2,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[{"id":"{{GameA}}","name":"Halo","isInstalled":true,"iconArtworkId":"{{digest}}","coverArtworkId":"{{digest}}"}]}""");

        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        var game = Assert.Single(result.Games);
        Assert.Equal(digest, game.IconReference);
        Assert.Equal(digest, game.CoverArtworkReference);
    }

    [Fact]
    public void ArtworkPathResolverAcceptsOnlyOpaqueIdsInsideTheCache()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        using var sha = System.Security.Cryptography.SHA256.Create();
        var digest = string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        var cache = Path.Combine(_directory, "artwork");
        Directory.CreateDirectory(cache);
        var image = Path.Combine(cache, digest + ".jpg");
        File.WriteAllBytes(image, bytes);

        Assert.Equal(image, PlayniteCatalogAdapter.ResolveArtworkPath(digest, cache));
        Assert.Equal(image, PlayniteCatalogAdapter.ResolveArtworkPath(digest, cache + Path.DirectorySeparatorChar));
        Assert.Null(PlayniteCatalogAdapter.ResolveArtworkPath("../" + digest, cache));
        Assert.Null(PlayniteCatalogAdapter.ResolveArtworkPath(null, cache));
        File.WriteAllBytes(image, new byte[] { 1, 2, 3 });
        Assert.Null(PlayniteCatalogAdapter.ResolveArtworkPath(digest, cache));
    }


    [Fact]
    public void ArtworkResolverCanReadPreviousGenerationDuringAtomicSnapshotSwap()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        using var sha = System.Security.Cryptography.SHA256.Create();
        var digest = string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        var cacheRoot = Path.Combine(_directory, "artwork-v2");
        var backupRoot = cacheRoot + ".backup-test";
        Directory.CreateDirectory(cacheRoot);
        Directory.CreateDirectory(backupRoot);
        var image = Path.Combine(backupRoot, digest + ".jpg");
        File.WriteAllBytes(image, bytes);

        Assert.Equal(image, PlayniteCatalogAdapter.ResolveArtworkPath(digest, cacheRoot));
        Assert.Equal(image, PlayniteCatalogAdapter.ResolveArtworkPath(digest, cacheRoot + Path.DirectorySeparatorChar));
    }

    [Fact]
    public async Task ReadsLegacyV1WhenNoEnrichedSnapshotExists()
    {
        var enrichedPath = Path.Combine(_directory, "catalog-v2.json");
        var legacyPath = Path.Combine(_directory, "catalog-v1.json");
        await SnapshotFilePublisher.PublishAsync(legacyPath, NewSnapshot(("Legacy game", true, null, null)));
        var result = await new PlayniteCatalogAdapter(enrichedPath, _time, legacyPath).GetAsync();
        Assert.True(result.IsAvailable);
        Assert.Equal("Legacy game", Assert.Single(result.Games).Title);
        Assert.Null(result.Games[0].LastActivity);
    }

    [Fact]
    public async Task PrefersNewerV2SnapshotAndPreservesLastActivity()
    {
        var enrichedPath = Path.Combine(_directory, "catalog-v2.json");
        var legacyPath = Path.Combine(_directory, "catalog-v1.json");
        var activity = new DateTime(2026, 10, 30, 14, 25, 0, DateTimeKind.Unspecified);
        var older = new CatalogSnapshot(_time.GetUtcNow() - TimeSpan.FromMinutes(1), new[] { new SnapshotGame(GameA, "Old legacy", true) });
        var newer = new CatalogSnapshot(_time.GetUtcNow(), new[] { new SnapshotGame(GameA, "New enriched", true, lastActivity: activity) });
        await SnapshotFilePublisher.PublishAsync(legacyPath, older);
        await SnapshotFilePublisher.PublishAsync(enrichedPath, newer, CancellationToken.None, schemaVersion: 2, includeLastActivity: true);

        var result = await new PlayniteCatalogAdapter(enrichedPath, _time, legacyPath).GetAsync();
        Assert.Equal("New enriched", Assert.Single(result.Games).Title);
        Assert.Equal(activity, result.Games[0].LastActivity);
    }

    [Fact]
    public async Task PrefersNewerLegacyV1AfterRollbackEvenWhenOldV2Remains()
    {
        var enrichedPath = Path.Combine(_directory, "catalog-v2.json");
        var legacyPath = Path.Combine(_directory, "catalog-v1.json");
        var staleV2 = new CatalogSnapshot(_time.GetUtcNow() - TimeSpan.FromMinutes(2), new[] { new SnapshotGame(GameA, "Before rollback", true, lastActivity: new DateTime(2026, 10, 29, 8, 0, 0)) });
        var updatedV1 = new CatalogSnapshot(_time.GetUtcNow(), new[] { new SnapshotGame(GameA, "After rollback", false) });
        await SnapshotFilePublisher.PublishAsync(enrichedPath, staleV2, CancellationToken.None, schemaVersion: 2, includeLastActivity: true);
        await SnapshotFilePublisher.PublishAsync(legacyPath, updatedV1);

        var result = await new PlayniteCatalogAdapter(enrichedPath, _time, legacyPath).GetAsync();
        Assert.Equal("After rollback", Assert.Single(result.Games).Title);
        Assert.False(result.Games[0].IsInstalled);
        Assert.Null(result.Games[0].LastActivity);
    }


    [Fact]
    public async Task RevalidatesV2AfterReadingANewerV1DuringPublication()
    {
        var enrichedPath = Path.Combine(_directory, "catalog-v2.json");
        var legacyPath = Path.Combine(_directory, "catalog-v1.json");
        var oldV2 = await ParseSnapshotAsync(new CatalogSnapshot(
            _time.GetUtcNow() - TimeSpan.FromMinutes(1),
            new[] { new SnapshotGame(GameA, "Old enriched", true, lastActivity: new DateTime(2026, 10, 29, 8, 0, 0)) }),
            schemaVersion: 2, includeLastActivity: true);
        var newV1 = await ParseSnapshotAsync(new CatalogSnapshot(
            _time.GetUtcNow(),
            new[] { new SnapshotGame(GameA, "New export", true) }));
        var newV2 = await ParseSnapshotAsync(new CatalogSnapshot(
            _time.GetUtcNow(),
            new[] { new SnapshotGame(GameA, "New export", true, lastActivity: new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc)) }),
            schemaVersion: 2, includeLastActivity: true);
        var enrichedReads = 0;

        Task<CatalogSnapshotParseResult> ReadPathAsync(string path)
        {
            if (path == enrichedPath)
                return Task.FromResult(Interlocked.Increment(ref enrichedReads) == 1 ? oldV2 : newV2);
            return Task.FromResult(newV1);
        }

        var adapter = new PlayniteCatalogAdapter(enrichedPath, legacyPath, _time, ReadPathAsync);
        var result = await adapter.GetAsync();

        Assert.Equal(2, enrichedReads);
        Assert.Equal("New export", Assert.Single(result.Games).Title);
        Assert.NotNull(result.Games[0].LastActivity);
    }

    [Fact]
    public async Task RevalidatesInitiallyMissingV2BeforeFallingBackToLegacy()
    {
        var enrichedPath = Path.Combine(_directory, "catalog-v2.json");
        var legacyPath = Path.Combine(_directory, "catalog-v1.json");
        var missing = CatalogSnapshotParseResult.Failure(
            CatalogErrorCode.Missing, "The enriched snapshot is not published yet.");
        var newV1 = await ParseSnapshotAsync(new CatalogSnapshot(
            _time.GetUtcNow(),
            new[] { new SnapshotGame(GameA, "New export", true) }));
        var newV2 = await ParseSnapshotAsync(new CatalogSnapshot(
            _time.GetUtcNow(),
            new[] { new SnapshotGame(GameA, "New export", true, lastActivity: new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc)) }),
            schemaVersion: 2, includeLastActivity: true);
        var enrichedReads = 0;

        Task<CatalogSnapshotParseResult> ReadPathAsync(string path)
        {
            if (path == enrichedPath)
                return Task.FromResult(Interlocked.Increment(ref enrichedReads) == 1 ? missing : newV2);
            return Task.FromResult(newV1);
        }

        var adapter = new PlayniteCatalogAdapter(enrichedPath, legacyPath, _time, ReadPathAsync);
        var result = await adapter.GetAsync();

        Assert.Equal(2, enrichedReads);
        Assert.Equal("New export", Assert.Single(result.Games).Title);
        Assert.NotNull(result.Games[0].LastActivity);
    }

    private CatalogSnapshot NewSnapshot(params (string Name, bool Installed, string? Source, string? Platform)[] games) =>
        new(_time.GetUtcNow(), games.Select((game, index) => new SnapshotGame(index == 0 ? GameA : GameB, game.Name, game.Installed, game.Source, game.Platform)).ToArray());


    private static async Task<CatalogSnapshotParseResult> ParseSnapshotAsync(
        CatalogSnapshot snapshot,
        int schemaVersion = 1,
        bool includeLastActivity = false)
    {
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(
            stream, snapshot, schemaVersion: schemaVersion, includeLastActivity: includeLastActivity);
        return CatalogSnapshotParser.Parse(stream.ToArray());
    }

    private Task PublishAsync(CatalogSnapshot snapshot) => SnapshotFilePublisher.PublishAsync(_path, snapshot);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
