using System.Text.Json;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class CatalogSnapshotSerializerTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2026-10-30T00:00:00Z");

    [Fact]
    public async Task EscapesUnicodeAndControlCharactersAndRoundTripsTheAllowlist()
    {
        var name = "Example \"quoted\" \\ folder/part\n\t\0 café 🎮 " + '\ud800' + " end " + '\udc00' + "!";
        var expectedName = "Example \"quoted\" \\ folder/part\n\t\0 café 🎮 \uFFFD end \uFFFD!";
        var snapshot = new CatalogSnapshot(Timestamp, new[]
        {
            new SnapshotGame(GameA, name, true, "Source \"A\"", "PC\\Windows"),
            new SnapshotGame(GameB, "Second", false),
        });

        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, snapshot);
        Assert.True(stream.CanWrite);
        var bytes = stream.ToArray();
        Assert.Equal((byte)'{', bytes[0]); // No UTF-8 BOM.

        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        Assert.Equal(3, root.EnumerateObject().Count());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        var games = root.GetProperty("games");
        Assert.Equal(2, games.GetArrayLength());
        Assert.Equal(5, games[0].EnumerateObject().Count());
        Assert.True(games[0].GetProperty("isInstalled").GetBoolean());
        Assert.Equal(expectedName, games[0].GetProperty("name").GetString());
        Assert.Equal("Source \"A\"", games[0].GetProperty("source").GetString());
        Assert.Equal("PC\\Windows", games[0].GetProperty("platform").GetString());
        Assert.Equal(3, games[1].EnumerateObject().Count());
        Assert.False(games[1].GetProperty("isInstalled").GetBoolean());
        Assert.False(games[1].TryGetProperty("source", out _));
        Assert.False(games[1].TryGetProperty("platform", out _));

        var parsed = CatalogSnapshotParser.Parse(bytes);
        Assert.Null(parsed.Issue);
        Assert.Equal(Timestamp, parsed.Snapshot!.GeneratedAtUtc);
        Assert.Equal(GameA, parsed.Snapshot.Games[0].Id);
        Assert.Equal(expectedName, parsed.Snapshot.Games[0].Name);
        Assert.Equal(GameB, parsed.Snapshot.Games[1].Id);
    }

    [Fact]
    public async Task WritesAnEmptyLibrary()
    {
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, new CatalogSnapshot(Timestamp, Array.Empty<SnapshotGame>()));
        var result = CatalogSnapshotParser.Parse(stream.ToArray());
        Assert.Null(result.Issue);
        Assert.Empty(result.Snapshot!.Games);
    }

    [Fact]
    public async Task PreCanceledSerializationLeavesTheStreamOpenAndUnchanged()
    {
        using var stream = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var snapshot = new CatalogSnapshot(Timestamp, Array.Empty<SnapshotGame>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CatalogSnapshotSerializer.SerializeAsync(stream, snapshot, cancellation.Token));
        Assert.True(stream.CanWrite);
        Assert.Equal(0L, stream.Length);
    }

    [Fact]
    public async Task CancellationDuringWritingStopsSerializationAndLeavesDestinationOpen()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelOnWriteStream(cancellation);
        var snapshot = new CatalogSnapshot(Timestamp, new[]
        {
            new SnapshotGame(GameA, new string('x', 16 * 1024), false),
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CatalogSnapshotSerializer.SerializeAsync(stream, snapshot, cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public async Task WriteFailureIsNotMaskedByWriterCleanup()
    {
        using var stream = new FailingWriteStream();
        var snapshot = new CatalogSnapshot(Timestamp, new[]
        {
            new SnapshotGame(GameA, new string('x', 16 * 1024), false),
        });

        var error = await Assert.ThrowsAsync<IOException>(() => CatalogSnapshotSerializer.SerializeAsync(stream, snapshot));
        Assert.Equal("Original write failed.", error.Message);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void SharedSnapshotAssemblyDoesNotReferenceSystemTextJson()
    {
        Assert.DoesNotContain(typeof(CatalogSnapshot).Assembly.GetReferencedAssemblies(), reference =>
            string.Equals(reference.Name, "System.Text.Json", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CancelOnWriteStream(CancellationTokenSource cancellation) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            cancellation.Cancel();
        }
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        private int _attempts;

        public override void Write(byte[] buffer, int offset, int count)
        {
            _attempts++;
            throw new IOException(_attempts == 1 ? "Original write failed." : "Cleanup write failed.");
        }
    }
}
