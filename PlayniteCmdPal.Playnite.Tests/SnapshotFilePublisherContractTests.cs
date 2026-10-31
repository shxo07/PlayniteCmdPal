using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

/// <summary>
/// Failure-path tests for atomic snapshot publication. Everything is synthetic and uses a temporary
/// directory, so these run without Playnite or a personal game library.
/// </summary>
public sealed class SnapshotFilePublisherContractTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Tests", Guid.NewGuid().ToString("N"));

    public SnapshotFilePublisherContractTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task ArgumentValidationHappensBeforeTouchingTheFileSystem()
    {
        var snapshot = Snapshot("Halo");

        Assert.Equal("path", (await Assert.ThrowsAsync<ArgumentException>(() => SnapshotFilePublisher.PublishAsync(" ", snapshot))).ParamName);
        Assert.Equal("path", (await Assert.ThrowsAsync<ArgumentException>(() => SnapshotFilePublisher.PublishAsync(string.Empty, snapshot))).ParamName);
        Assert.Equal("snapshot", (await Assert.ThrowsAsync<ArgumentNullException>(() => SnapshotFilePublisher.PublishAsync(Path.Combine(_directory, "catalog-v1.json"), null!))).ParamName);
        // The null-snapshot call above passes a real path; validation must reject it before any
        // directory or temporary file is created.
        Assert.Empty(Directory.GetFileSystemEntries(_directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PreCanceledPublicationCreatesNothing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SnapshotFilePublisher.PublishAsync(Path.Combine(_directory, "nested", "catalog-v1.json"), Snapshot("Halo"), cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(_directory, "nested")));
    }

    [Fact]
    public async Task RepeatedPublicationsReplaceTheFileWithoutGrowingTheDirectory()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");

        for (var index = 0; index < 10; index++)
        {
            await SnapshotFilePublisher.PublishAsync(path, Snapshot("Game " + index));
        }

        Assert.Equal(new[] { "catalog-v1.json" }, Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName));
        Assert.Equal("Game 9", CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(path)).Snapshot!.Games[0].Name);
    }

    [Fact]
    public async Task PublicationOverwritesAStaleFileThatIsNotAValidSnapshot()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        await File.WriteAllTextAsync(path, "stale or truncated content");

        await SnapshotFilePublisher.PublishAsync(path, Snapshot("Halo"));

        var parsed = CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(path));
        Assert.Null(parsed.Issue);
        Assert.Equal("Halo", parsed.Snapshot!.Games[0].Name);
    }

    [Fact]
    public async Task ConcurrentFirstPublicationsLeaveExactlyOneCompleteSnapshot()
    {
        var path = Path.Combine(_directory, "nested", "catalog-v1.json");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            SnapshotFilePublisher.PublishAsync(path, Snapshot("Contender " + index))));

        var parsed = CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(path));
        Assert.Null(parsed.Issue);
        Assert.Single(parsed.Snapshot!.Games);
        Assert.StartsWith("Contender ", parsed.Snapshot.Games[0].Name, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "catalog-v1.json" },
            Directory.GetFileSystemEntries(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }

    [Fact]
    public async Task PublicationIsValidAtTheDocumentLevelAndKeepsOptionalFieldsOmitted()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        var snapshot = new CatalogSnapshot(Timestamp, new[]
        {
            new SnapshotGame(GameA, "Halo", true, "Steam", " PC "),
        });

        await SnapshotFilePublisher.PublishAsync(path, snapshot);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal((byte)'{', bytes[0]);
        var parsed = CatalogSnapshotParser.Parse(bytes);
        Assert.Null(parsed.Issue);
        Assert.Equal("Steam", parsed.Snapshot!.Games[0].Source);
        Assert.Equal("PC", parsed.Snapshot.Games[0].Platform);
        Assert.Equal(Timestamp, parsed.Snapshot.GeneratedAtUtc);
    }

    [Fact]
    public async Task TemporaryFilesNeverRemainAfterAFailedPublication()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        await SnapshotFilePublisher.PublishAsync(path, Snapshot("First"));

        // A directory occupying the destination makes replacement fail deterministically.
        File.Delete(path);
        Directory.CreateDirectory(path);

        await Assert.ThrowsAnyAsync<Exception>(() => SnapshotFilePublisher.PublishAsync(path, Snapshot("Second")));

        // The failed publication cleaned up its own temporary file and left the destination alone.
        Assert.Equal(new[] { "catalog-v1.json" }, Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName));
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task PublicationDoesNotDeleteAnUnrelatedSiblingFile()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        var sibling = Path.Combine(_directory, "notes.txt");
        await File.WriteAllTextAsync(sibling, "keep me");

        await SnapshotFilePublisher.PublishAsync(path, Snapshot("Halo"));

        Assert.Equal("keep me", await File.ReadAllTextAsync(sibling));
        Assert.True(File.Exists(path));
    }

    private static CatalogSnapshot Snapshot(string name) =>
        new(Timestamp, new[] { new SnapshotGame(GameA, name, false) });
}
