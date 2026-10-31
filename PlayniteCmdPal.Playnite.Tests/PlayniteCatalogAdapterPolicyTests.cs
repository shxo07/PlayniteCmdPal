using PlayniteCmdPal.Core;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

/// <summary>
/// Adapter cache-policy contract tests. They use synthetic snapshots on disk only; no Playnite
/// installation, personal library, PowerToys, or secrets are required.
/// </summary>
public sealed class PlayniteCatalogAdapterPolicyTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string GameC = "33333333-3333-4333-8333-333333333333";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    public PlayniteCatalogAdapterPolicyTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "catalog-v1.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void DefaultSnapshotPathIsUnderLocalAppDataAndNamedForSchemaOne()
    {
        var path = PlayniteCatalogAdapter.GetDefaultSnapshotPath();

        Assert.Equal("catalog-v1.json", Path.GetFileName(path));
        Assert.Equal("PlayniteCmdPal", Path.GetFileName(Path.GetDirectoryName(path)));
        Assert.EndsWith(
            Path.Combine("PlayniteCmdPal", "catalog-v1.json"),
            path,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyWindowsAreTheDocumentedConstants()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), PlayniteCatalogAdapter.RefreshInterval);
        Assert.Equal(TimeSpan.FromHours(24), PlayniteCatalogAdapter.StaleAfter);
    }

    [Fact]
    public async Task AdapterNeverCreatesOrDeletesTheSnapshotFile()
    {
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        Assert.False((await adapter.GetAsync()).IsAvailable);
        Assert.False(File.Exists(_path));

        await PublishAsync(NewSnapshot("Halo"));
        await adapter.RefreshAsync();
        var before = await File.ReadAllBytesAsync(_path);

        await adapter.RefreshAsync();

        Assert.True(File.Exists(_path));
        Assert.Equal(before, await File.ReadAllBytesAsync(_path));
    }

    [Fact]
    public async Task SnapshotIsReadableWhileAWriterHoldsTheFileOpen()
    {
        // The adapter opens with FileShare.ReadWrite | FileShare.Delete; a bridge publication in
        // flight must not make the last known state unreadable.
        await PublishAsync(NewSnapshot("Halo"));

        using (new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

            Assert.True(result.IsAvailable);
            Assert.Null(result.Issue);
            Assert.Equal("Halo", Assert.Single(result.Games).Title);
        }
    }

    [Fact]
    public async Task DirectoryPathInsteadOfFileIsReportedAsUnavailableRatherThanMissing()
    {
        Directory.CreateDirectory(_path);

        // Windows reports opening a directory as an unauthorized access; Linux as an I/O error.
        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        Assert.False(result.IsAvailable);
        Assert.NotNull(result.Issue);
        Assert.NotEqual(CatalogErrorCode.Missing, result.Issue.Code);
    }

    [Fact]
    public async Task InvalidRecordsInAnOtherwiseValidFileKeepTheLibraryAvailable()
    {
        await File.WriteAllTextAsync(_path, $$"""
        {"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[
          {"id":"{{GameA}}","name":"Halo","isInstalled":true},
          {"id":"{{GameA}}","name":"Duplicate","isInstalled":true},
          {"id":"{{GameB}}","name":" ","isInstalled":false}
        ]}
        """);

        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        Assert.True(result.IsAvailable);
        Assert.Null(result.Issue);
        Assert.Equal("Halo", Assert.Single(result.Games).Title);
        Assert.Equal(2, result.SkippedRecordCount);
    }

    [Fact]
    public async Task UnsupportedSchemaOnFirstReadIsUnavailableAndNotAnEmptyLibrary()
    {
        await File.WriteAllTextAsync(_path, """{"schemaVersion":2,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""");

        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        Assert.False(result.IsAvailable);
        Assert.Empty(result.Games);
        Assert.False(result.IsFromCache);
        Assert.False(result.IsStale);
        Assert.Equal(CatalogErrorCode.UnsupportedSchema, result.Issue!.Code);
    }

    [Fact]
    public async Task EmptyFileIsUnavailable()
    {
        await File.WriteAllBytesAsync(_path, Array.Empty<byte>());

        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public async Task CatalogMapsToCoreEntriesThatSearchCanUse()
    {
        await PublishAsync(new CatalogSnapshot(_time.GetUtcNow(), new[]
        {
            new SnapshotGame(GameA, "Halo", true, "Steam", "PC"),
            new SnapshotGame(GameB, "Halo 2", false, null, null),
            new SnapshotGame(GameC, "Superhalo", false, "GOG", "PC"),
        }));

        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        var ranked = GameSearch.Search(result.Games, "halo");

        Assert.Equal(new[] { "Halo", "Halo 2", "Superhalo" }, ranked.Select(game => game.Title));
        Assert.Equal(GameA, ranked[0].GameId);
        Assert.True(ranked[0].IsInstalled);
        Assert.Equal("PC", ranked[0].Platform);
        Assert.False(ranked[1].IsInstalled);
    }

    [Fact]
    public async Task UninstalledStateIsPreservedAsFalseRatherThanUnknown()
    {
        await PublishAsync(new CatalogSnapshot(_time.GetUtcNow(), new[]
        {
            new SnapshotGame(GameA, "Halo", false),
        }));

        var game = Assert.Single((await new PlayniteCatalogAdapter(_path, _time).GetAsync()).Games);

        Assert.False(game.IsInstalled);
        Assert.Null(game.Source);
        Assert.Null(game.IconReference);
    }

    [Fact]
    public async Task CachedReadsWithinTheIntervalDoNotReopenTheFile()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        Assert.False((await adapter.GetAsync()).IsFromCache);

        // Replacing the file behind the adapter proves the interval returned memory, not disk.
        await PublishAsync(NewSnapshot("Halo 2"));
        _time.Advance(PlayniteCatalogAdapter.RefreshInterval - TimeSpan.FromMilliseconds(1));

        var cached = await adapter.GetAsync();

        Assert.True(cached.IsFromCache);
        Assert.Equal("Halo", Assert.Single(cached.Games).Title);
    }

    [Fact]
    public async Task RefreshIntervalBoundaryTriggersARevalidation()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        await adapter.GetAsync();

        await PublishAsync(NewSnapshot("Halo 2"));
        _time.Advance(PlayniteCatalogAdapter.RefreshInterval);

        var revalidated = await adapter.GetAsync();

        Assert.False(revalidated.IsFromCache);
        Assert.Equal("Halo 2", Assert.Single(revalidated.Games).Title);
    }

    [Fact]
    public async Task ExpiredIntervalStillReportsTheCurrentIssueAndRetainedLibrary()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        await adapter.GetAsync();

        await File.WriteAllTextAsync(_path, "not json");
        _time.Advance(PlayniteCatalogAdapter.RefreshInterval);

        var result = await adapter.GetAsync();

        Assert.True(result.IsAvailable);
        Assert.True(result.IsFromCache);
        Assert.Equal("Halo", Assert.Single(result.Games).Title);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public async Task RepeatedFailuresKeepReportingTheOriginalLastKnownGood()
    {
        await PublishAsync(new CatalogSnapshot(
            new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero),
            new[] { new SnapshotGame(GameA, "Halo", true), new SnapshotGame(GameB, "Halo 2", false) }));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        await adapter.GetAsync();
        File.Delete(_path);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await adapter.RefreshAsync();
            Assert.True(result.IsAvailable);
            Assert.Equal(new[] { "Halo", "Halo 2" }, result.Games.Select(game => game.Title));
            Assert.Equal(CatalogErrorCode.Missing, result.Issue!.Code);
            Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), result.GeneratedAtUtc);
        }
    }

    [Fact]
    public async Task ConcurrentRefreshCallersShareOneReadAndOneIssue()
    {
        await File.WriteAllTextAsync(_path, "not json");
        var adapter = new PlayniteCatalogAdapter(_path, _time);

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => adapter.RefreshAsync()));

        Assert.All(results, result => Assert.False(result.IsAvailable));
        Assert.All(results, result => Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code));
    }

    [Fact]
    public async Task GetThatOverlapsARefreshReturnsThatRefreshResultOrACachedOne()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);

        var refresh = adapter.RefreshAsync();
        var get = adapter.GetAsync();
        await Task.WhenAll(refresh, get).WaitAsync(TimeSpan.FromSeconds(10));

        // Both outcomes are supported: a caller that arrives mid-read shares it, and a caller
        // that arrives after it finished is served from memory without touching the file.
        Assert.True(refresh.Result.IsAvailable);
        Assert.False(refresh.Result.IsFromCache);
        Assert.Equal(refresh.Result.Games, get.Result.Games);
        if (ReferenceEquals(refresh.Result, get.Result))
        {
            Assert.False(get.Result.IsFromCache);
        }
        else
        {
            Assert.True(get.Result.IsFromCache);
        }
    }

    [Fact]
    public async Task PreCanceledCallerIsRejectedWithoutDisturbingInFlightWork()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var refresh = adapter.RefreshAsync();
        var canceled = adapter.GetAsync(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        var result = await refresh.WaitAsync(TimeSpan.FromSeconds(10));

        // The refresh still performed the real read that the canceled caller never joined.
        Assert.False(result.IsFromCache);
        Assert.Null(result.Issue);
        Assert.Equal("Halo", Assert.Single(result.Games).Title);
    }

    [Fact]
    public async Task ResultsAreImmutableSnapshotsOfTheLibrary()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);
        var first = await adapter.GetAsync();

        await PublishAsync(NewSnapshot("Halo 2"));
        var second = await adapter.RefreshAsync();

        Assert.Equal("Halo", Assert.Single(first.Games).Title);
        Assert.Equal("Halo 2", Assert.Single(second.Games).Title);
        Assert.NotSame(first.Games, second.Games);
    }

    [Fact]
    public async Task LargeSyntheticLibraryRoundTripsThroughTheRealPublisherAndAdapter()
    {
        var games = Enumerable.Range(0, 2_000)
            .Select(index => new SnapshotGame(
                DeterministicGuid(index),
                "Synthetic Game " + index,
                index % 2 == 0,
                index % 3 == 0 ? "Steam" : null,
                index % 5 == 0 ? "PC" : null))
            .ToArray();

        await PublishAsync(new CatalogSnapshot(_time.GetUtcNow(), games));
        var result = await new PlayniteCatalogAdapter(_path, _time).GetAsync();

        Assert.True(result.IsAvailable);
        Assert.Null(result.Issue);
        Assert.Equal(0, result.SkippedRecordCount);
        Assert.Equal(games.Length, result.Games.Count);
        Assert.Equal(games.Select(game => game.Id), result.Games.Select(game => game.GameId));
        Assert.Equal(games.Select(game => game.Name), result.Games.Select(game => game.Title));
        Assert.Equal(games.Select(game => game.Source), result.Games.Select(game => game.Source));
        Assert.Equal(games.Select(game => game.Platform), result.Games.Select(game => game.Platform));
        Assert.Equal(
            games.Select(game => (bool?)game.IsInstalled),
            result.Games.Select(game => game.IsInstalled));
    }

    [Fact]
    public async Task RepeatedReadsOfUnchangedContentProduceEqualCatalogs()
    {
        await PublishAsync(NewSnapshot("Halo"));
        var adapter = new PlayniteCatalogAdapter(_path, _time);

        var first = await adapter.GetAsync();
        var second = await adapter.RefreshAsync();

        Assert.Equal(first.Games, second.Games);
        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc);
        Assert.Equal(first.SkippedRecordCount, second.SkippedRecordCount);
    }

    private CatalogSnapshot NewSnapshot(string title) =>
        new(_time.GetUtcNow(), new[] { new SnapshotGame(GameA, title, true) });

    private Task PublishAsync(CatalogSnapshot snapshot) => SnapshotFilePublisher.PublishAsync(_path, snapshot);

    private static string DeterministicGuid(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index + 1).CopyTo(bytes, 0);
        bytes[15] = 0x3F;
        return new Guid(bytes).ToString("D");
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
