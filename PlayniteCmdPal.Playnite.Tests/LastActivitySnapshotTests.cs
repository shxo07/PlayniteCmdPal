using System.Text;
using System.Text.Json;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class LastActivitySnapshotTests
{
    private const string GameId = "11111111-1111-4111-8111-111111111111";
    private static readonly DateTimeOffset GeneratedAt = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LastActivityRoundTripsWithoutForcingUtc()
    {
        var activity = new DateTime(2026, 10, 30, 14, 25, 0, DateTimeKind.Unspecified);
        var snapshot = new CatalogSnapshot(GeneratedAt, new[]
        {
            new SnapshotGame(GameId, "Synthetic game", true, lastActivity: activity),
        });

        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, snapshot, schemaVersion: 2, includeLastActivity: true);
        using var document = JsonDocument.Parse(stream.ToArray());
        var written = document.RootElement.GetProperty("games")[0].GetProperty("lastActivity").GetString();

        Assert.Equal(activity.ToString("O", System.Globalization.CultureInfo.InvariantCulture), written);
        var parsed = CatalogSnapshotParser.Parse(stream.ToArray());
        Assert.Null(parsed.Issue);
        var game = Assert.Single(parsed.Snapshot!.Games);
        Assert.Equal(activity, game.LastActivity);
        Assert.Equal(DateTimeKind.Unspecified, game.LastActivity!.Value.Kind);
    }

    [Fact]
    public async Task LocalDateTimeRoundTripsWithItsLocalKindAndOffset()
    {
        var activity = DateTime.SpecifyKind(new DateTime(2026, 10, 30, 14, 25, 0), DateTimeKind.Local);
        var bytes = await SerializeAsync(activity);
        var parsed = CatalogSnapshotParser.Parse(bytes);
        Assert.Null(parsed.Issue);
        var actual = Assert.Single(parsed.Snapshot!.Games).LastActivity!.Value;
        Assert.Equal(DateTimeKind.Local, actual.Kind);
        Assert.Equal(activity, actual);
    }

    [Fact]
    public async Task UtcDateTimeRoundTripsAsUtc()
    {
        var activity = new DateTime(2026, 10, 30, 14, 25, 0, DateTimeKind.Utc);
        var bytes = await SerializeAsync(activity);
        var parsed = CatalogSnapshotParser.Parse(bytes);
        Assert.Null(parsed.Issue);
        var actual = Assert.Single(parsed.Snapshot!.Games).LastActivity!.Value;
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.Equal(activity, actual);
    }

    private static async Task<byte[]> SerializeAsync(DateTime activity)
    {
        var snapshot = new CatalogSnapshot(GeneratedAt, new[] { new SnapshotGame(GameId, "Synthetic game", true, lastActivity: activity) });
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, snapshot, schemaVersion: 2, includeLastActivity: true);
        return stream.ToArray();
    }

    [Fact]
    public void MissingLastActivityRemainsNullAndMalformedValuesSkipOnlyTheirRecord()
    {
        var json = $$"""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[{"id":"{{GameId}}","name":"No history","isInstalled":false},{"id":"22222222-2222-4222-8222-222222222222","name":"Bad date","isInstalled":false,"lastActivity":"yesterday"}]}""";
        var parsed = CatalogSnapshotParser.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Null(parsed.Issue);
        Assert.Equal(1, parsed.SkippedRecordCount);
        Assert.Null(Assert.Single(parsed.Snapshot!.Games).LastActivity);
    }
}
