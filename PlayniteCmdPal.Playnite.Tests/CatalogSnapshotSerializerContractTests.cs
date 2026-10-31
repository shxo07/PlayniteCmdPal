using System.Text.Json;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

/// <summary>
/// Output-shape and failure-path tests for the allowlisted v1 writer, using synthetic data only.
/// </summary>
public sealed class CatalogSnapshotSerializerContractTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 30, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task ArgumentValidationHappensBeforeWriting()
    {
        using var stream = new MemoryStream();

        Assert.Equal("destination", (await Assert.ThrowsAsync<ArgumentNullException>(() => CatalogSnapshotSerializer.SerializeAsync(null!, EmptySnapshot()))).ParamName);
        Assert.Equal("snapshot", (await Assert.ThrowsAsync<ArgumentNullException>(() => CatalogSnapshotSerializer.SerializeAsync(stream, null!))).ParamName);
        Assert.Equal(0L, stream.Length);
    }

    [Fact]
    public async Task EnvelopeFieldSetAndOrderAreStable()
    {
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, EmptySnapshot());

        using var document = JsonDocument.Parse(stream.ToArray());
        Assert.Equal(
            new[] { "schemaVersion", "generatedAtUtc", "games" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(JsonValueKind.Number, document.RootElement.GetProperty("schemaVersion").ValueKind);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("generatedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("games").ValueKind);
    }

    [Fact]
    public async Task TimestampIsWrittenAsRoundTrippableUtcRegardlessOfTheCallerOffset()
    {
        // The model only accepts zero-offset timestamps, so this asserts the exact wire format.
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, EmptySnapshot());

        using var document = JsonDocument.Parse(stream.ToArray());
        var written = document.RootElement.GetProperty("generatedAtUtc").GetString();

        Assert.NotNull(written);
        Assert.EndsWith("Z", written, StringComparison.Ordinal);
        Assert.Equal(Timestamp.UtcDateTime, DateTime.Parse(written, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public async Task OutputContainsNoByteOrderMarkAndIsValidUtf8Json()
    {
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, SnapshotWith(("  Halo: Combat Evolved  ", " Steam ")));

        var bytes = stream.ToArray();

        Assert.NotEqual((byte)0xEF, bytes[0]);
        Assert.Equal((byte)'{', bytes[0]);
        using var document = JsonDocument.Parse(bytes);
        var game = document.RootElement.GetProperty("games")[0];
        Assert.Equal("Halo: Combat Evolved", game.GetProperty("name").GetString());
        Assert.Equal("Steam", game.GetProperty("source").GetString());
    }

    [Fact]
    public async Task EveryRecordCarriesIdentityNameAndInstallState()
    {
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, SnapshotWith(("Halo", null), ("Halo 2", null)));

        using var document = JsonDocument.Parse(stream.ToArray());
        var games = document.RootElement.GetProperty("games");
        Assert.Equal(2, games.GetArrayLength());
        foreach (var game in games.EnumerateArray())
        {
            Assert.Equal(
                new[] { "id", "name", "isInstalled" },
                game.EnumerateObject().Select(property => property.Name));
        }
    }

    [Fact]
    public async Task SerializationIsDeterministicForIdenticalInput()
    {
        var first = await SerializeAsync(SnapshotWith(("Halo", "Steam")));
        var second = await SerializeAsync(SnapshotWith(("Halo", "Steam")));

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task DestinationRemainsOpenAndFlushedAfterSuccess()
    {
        using var stream = new MemoryStream();

        await CatalogSnapshotSerializer.SerializeAsync(stream, EmptySnapshot());

        Assert.True(stream.CanWrite);
        Assert.True(stream.Length > 0);
        stream.Position = 0;
        Assert.Equal((byte)'{', stream.ReadByte());
    }

    [Fact]
    public async Task NonWritableDestinationFailsWithoutSwallowingTheCause()
    {
        using var stream = new ReadOnlyStream();

        await Assert.ThrowsAnyAsync<Exception>(() => CatalogSnapshotSerializer.SerializeAsync(stream, SnapshotWith(("Halo", null))));
    }

    private static CatalogSnapshot EmptySnapshot() => new(Timestamp, Array.Empty<SnapshotGame>());

    private static CatalogSnapshot SnapshotWith(params (string Name, string? Source)[] games) =>
        new(Timestamp, games.Select((game, index) =>
            new SnapshotGame(DeterministicGuid(index), game.Name, true, game.Source)).ToArray());

    private static string DeterministicGuid(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index + 1).CopyTo(bytes, 0);
        bytes[15] = 0x4D;
        return new Guid(bytes).ToString("D");
    }

    private static async Task<byte[]> SerializeAsync(CatalogSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, snapshot);
        return stream.ToArray();
    }

    private sealed class ReadOnlyStream : MemoryStream
    {
        public override bool CanWrite => false;

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Synthetic read-only destination.");
    }
}
