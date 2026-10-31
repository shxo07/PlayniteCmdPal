using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class SnapshotFilePublisherTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Tests", Guid.NewGuid().ToString("N"));

    public SnapshotFilePublisherTests() => Directory.CreateDirectory(_directory);
    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task PublishesCreatesAndAtomicallyReplacesWithoutLeavingTempFiles()
    {
        var path = Path.Combine(_directory, "nested", "catalog-v1.json");
        await SnapshotFilePublisher.PublishAsync(path, Snapshot("First"));
        await SnapshotFilePublisher.PublishAsync(path, Snapshot("Second"));

        var result = CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(path));
        Assert.Null(result.Issue);
        Assert.Equal("Second", result.Snapshot!.Games[0].Name);
        Assert.Equal(new[] { "catalog-v1.json" }, Directory.GetFiles(Path.GetDirectoryName(path)!, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
    }

    [Fact]
    public async Task CanceledPublicationPreservesPreviousSnapshot()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        await SnapshotFilePublisher.PublishAsync(path, Snapshot("First"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotFilePublisher.PublishAsync(path, Snapshot("Second"), cancellation.Token));
        var result = CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(path));
        Assert.Equal("First", result.Snapshot!.Games[0].Name);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task ConcurrentPublicationsLeaveOneCompleteValidSnapshot()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => SnapshotFilePublisher.PublishAsync(path, Snapshot("Game " + i))));
        var result = CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(path));
        Assert.Null(result.Issue);
        Assert.Single(result.Snapshot!.Games);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task OversizedPublicationPreservesPreviousSnapshotAndCleansTempFile()
    {
        var path = Path.Combine(_directory, "catalog-v1.json");
        await SnapshotFilePublisher.PublishAsync(path, Snapshot("First"));
        var previous = await File.ReadAllBytesAsync(path);
        var largeName = new string('x', 1024 * 1024);
        var games = Enumerable.Range(0, 33)
            .Select(_ => new SnapshotGame(Guid.NewGuid().ToString("D"), largeName, false))
            .ToArray();
        var oversized = new CatalogSnapshot(DateTimeOffset.Parse("2026-10-30T00:00:00Z"), games);

        await Assert.ThrowsAsync<InvalidDataException>(() => SnapshotFilePublisher.PublishAsync(path, oversized));
        Assert.Equal(previous, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }

    private static CatalogSnapshot Snapshot(string name) =>
        new(DateTimeOffset.Parse("2026-10-30T00:00:00Z"), new[] { new SnapshotGame(GameA, name, false) });
}
