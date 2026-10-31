using System.Text;
using System.Text.Json;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class CatalogSnapshotParserTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";

    [Fact]
    public void ParsesValidSnapshotAndPreservesStableIdentity()
    {
        var result = CatalogSnapshotParser.Parse(Json($$"""
        {
          "schemaVersion": 1,
          "generatedAtUtc": "2026-10-30T00:00:00.0000000Z",
          "games": [
            { "id": "{{GameA}}", "name": "Same Title", "isInstalled": true, "source": "Steam", "platform": "PC" },
            { "id": "{{GameB}}", "name": "Same Title", "isInstalled": false }
          ]
        }
        """));

        Assert.Null(result.Issue);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(0, result.SkippedRecordCount);
        Assert.Equal(2, result.Snapshot!.Games.Count);
        Assert.Equal(GameA, result.Snapshot.Games[0].Id);
        Assert.Equal(GameB, result.Snapshot.Games[1].Id);
        Assert.True(result.Snapshot.Games[0].IsInstalled);
        Assert.Equal("Steam", result.Snapshot.Games[0].Source);
        Assert.Equal("PC", result.Snapshot.Games[0].Platform);
        Assert.Null(result.Snapshot.Games[1].Source);
    }

    [Theory]
    [InlineData("2026-10-30T00:00:00Z")]
    [InlineData("2026-10-30T00:00:00.1Z")]
    [InlineData("2026-10-30T00:00:00.1234567Z")]
    [InlineData("2026-10-30T00:00:00+00:00")]
    [InlineData("2026-10-30T00:00:00.1234567+00:00")]
    public void AcceptsUtcTimestampsWithAndWithoutFractionalSeconds(string timestamp)
    {
        var result = CatalogSnapshotParser.Parse(Json($$"""{"schemaVersion":1,"generatedAtUtc":"{{timestamp}}","games":[]}"""));
        Assert.Null(result.Issue);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(TimeSpan.Zero, result.Snapshot!.GeneratedAtUtc.Offset);
    }

    [Fact]
    public void EmptyGamesArrayIsValid()
    {
        var result = CatalogSnapshotParser.Parse(Json("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}"""));
        Assert.Null(result.Issue);
        Assert.Empty(result.Snapshot!.Games);
    }

    [Fact]
    public void SkipsInvalidRecordsAndDuplicateIdsWithoutLosingValidRecords()
    {
        var result = CatalogSnapshotParser.Parse(Json($$"""
        {
          "schemaVersion": 1,
          "generatedAtUtc": "2026-10-30T00:00:00Z",
          "games": [
            { "id": "{{GameA}}", "name": "Keep", "isInstalled": false },
            { "id": "not-a-guid", "name": "Bad", "isInstalled": false },
            { "id": "{{GameA}}", "name": "Duplicate", "isInstalled": true },
            { "id": "{{GameB}}", "name": " ", "isInstalled": true },
            { "id": "{{GameB}}", "name": "Missing installed" },
            null
          ]
        }
        """));

        Assert.Null(result.Issue);
        Assert.Equal(5, result.SkippedRecordCount);
        var game = Assert.Single(result.Snapshot!.Games);
        Assert.Equal("Keep", game.Name);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[],}""")]
    [InlineData("""{"schemaVersion":"1","generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    [InlineData("""{"schemaVersion":1.5,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"yesterday","games":[]}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00+05:30","games":[]}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00","games":[]}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":null}""")]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":{},"games":[]}""")]
    public void RejectsMalformedEnvelope(string json)
    {
        var result = CatalogSnapshotParser.Parse(Json(json));
        Assert.Null(result.Snapshot);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[],"extra":true}""")]
    [InlineData("""{"schemaVersion":1,"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    public void RejectsUnknownAndDuplicateEnvelopeProperties(string json)
    {
        var result = CatalogSnapshotParser.Parse(Json(json));
        Assert.Null(result.Snapshot);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Theory]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":false,"path":"not-allowlisted"}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","name":"Duplicate","isInstalled":false}""")]
    [InlineData("""{"id":"00000000-0000-0000-0000-000000000000","name":"Halo","isInstalled":false}""")]
    [InlineData("""{"id":"11111111111141118111111111111111","name":"Halo","isInstalled":false}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":"false"}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":false,"source":42}""")]
    public void SkipsInvalidOrNonAllowlistedGameRecords(string gameJson)
    {
        var result = CatalogSnapshotParser.Parse(Json($$"""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[{{gameJson}}]}"""));
        Assert.Null(result.Issue);
        Assert.Empty(result.Snapshot!.Games);
        Assert.Equal(1, result.SkippedRecordCount);
    }

    [Fact]
    public void RejectsUnsupportedSchema()
    {
        var result = CatalogSnapshotParser.Parse(Json("""{"schemaVersion":2,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}"""));
        Assert.Equal(CatalogErrorCode.UnsupportedSchema, result.Issue!.Code);
    }

    [Fact]
    public void RejectsTooManyRecords()
    {
        var games = string.Join(",", Enumerable.Range(0, SnapshotLimits.MaxGames + 1).Select(_ => "{}"));
        var result = CatalogSnapshotParser.Parse(Json($$"""{"schemaVersion":1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[{{games}}]}"""));
        Assert.Equal(CatalogErrorCode.TooManyGames, result.Issue!.Code);
    }

    [Fact]
    public void RejectsOversizedInput()
    {
        var result = CatalogSnapshotParser.Parse(new byte[SnapshotLimits.MaxBytes + 1]);
        Assert.Equal(CatalogErrorCode.TooLarge, result.Issue!.Code);
    }

    [Fact]
    public void RejectsExcessiveJsonDepth()
    {
        var json = "{\"schemaVersion\":1,\"generatedAtUtc\":\"2026-10-30T00:00:00Z\",\"games\":[],\"extra\":" + new string('[', SnapshotLimits.MaxJsonDepth + 2) + "0" + new string(']', SnapshotLimits.MaxJsonDepth + 2) + "}";
        var result = CatalogSnapshotParser.Parse(Json(json));
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public async Task SerializerRoundTripsOnlyAllowlistedFields()
    {
        var snapshot = new CatalogSnapshot(DateTimeOffset.Parse("2026-10-30T00:00:00Z"), new[]
        {
            new SnapshotGame(GameA, "Halo", true, "Steam", "PC"),
        });
        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, snapshot);
        using var document = JsonDocument.Parse(stream.ToArray());
        var game = document.RootElement.GetProperty("games")[0];
        Assert.Equal(5, game.EnumerateObject().Count());
        var result = CatalogSnapshotParser.Parse(stream.ToArray());
        Assert.Null(result.Issue);
        Assert.Equal("Halo", result.Snapshot!.Games[0].Name);
    }

    private static byte[] Json(string value) => Encoding.UTF8.GetBytes(value);
}
