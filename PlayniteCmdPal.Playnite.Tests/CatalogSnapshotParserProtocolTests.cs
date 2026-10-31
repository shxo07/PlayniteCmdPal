using System.Text;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

/// <summary>
/// Protocol and trust-boundary tests for the v1 snapshot parser. Every payload below is synthetic
/// and the parser never touches Playnite storage, so these run in normal CI.
/// </summary>
public sealed class CatalogSnapshotParserProtocolTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string ValidGame = "{\"id\":\"11111111-1111-4111-8111-111111111111\",\"name\":\"Halo\",\"isInstalled\":true}";
    private const string EnvelopePrefix = "{\"schemaVersion\":1,\"generatedAtUtc\":\"2026-10-30T00:00:00Z\",\"games\":";
    private const string ValidEnvelope = EnvelopePrefix + "[]}";

    [Fact]
    public void EnvelopePropertyOrderIsIrrelevant()
    {
        var reordered = $$"""
        {"games":[{{ValidGame}}],"generatedAtUtc":"2026-10-30T00:00:00.5Z","schemaVersion":1}
        """;

        var result = CatalogSnapshotParser.Parse(Json(reordered));

        Assert.Null(result.Issue);
        Assert.Equal("Halo", Assert.Single(result.Snapshot!.Games).Name);
    }

    [Fact]
    public void EmptyInputIsMalformed()
    {
        var result = CatalogSnapshotParser.Parse(ReadOnlyMemory<byte>.Empty);

        Assert.Null(result.Snapshot);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Issue.Message));
    }

    [Fact]
    public void TrailingContentAfterTheEnvelopeIsMalformed()
    {
        var result = CatalogSnapshotParser.Parse(Json(ValidEnvelope + "{}"));

        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public void FullyQualifiedPropertyNamesAreNotTreatedAsAllowlistedFields()
    {
        // Allowlisting is an exact name match; prefix/suffix variants must not slip through.
        var games = string.Join(",", new[]
        {
            $$"""{"id":"{{GameA}}","name":"Halo","isInstalled":true,"sourceName":"Steam"}""",
            $$"""{"id":"{{GameB}}","name":"Halo","isInstalled":true,"isInstalledValue":true}""",
        });

        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + games + "]}"));

        Assert.Null(result.Issue);
        Assert.Empty(result.Snapshot!.Games);
        Assert.Equal(2, result.SkippedRecordCount);
    }

    [Fact]
    public void UnicodeTitlesSurviveJsonEscapesAndNonAsciiInput()
    {
        var games = $$"""{"id":"{{GameA}}","name":"caf\u00e9 · 🎮","isInstalled":false}""";

        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + games + "]}"));

        Assert.Null(result.Issue);
        Assert.Equal("café · 🎮", Assert.Single(result.Snapshot!.Games).Name);
    }

    [Fact]
    public void NullOptionalFieldsAreTreatedAsAbsent()
    {
        var games = $$"""{"id":"{{GameA}}","name":" Halo ","isInstalled":false,"source":null,"platform":null}""";

        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + games + "]}"));

        var game = Assert.Single(result.Snapshot!.Games);
        Assert.Equal("Halo", game.Name);
        Assert.Null(game.Source);
        Assert.Null(game.Platform);
    }

    [Theory]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":true,"source":42}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":true,"platform":true}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":true,"source":{}}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":true,"platform":[]}""")]
    public void OptionalFieldsMustBeStringsOrNull(string gameJson)
    {
        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + gameJson + "]}"));

        Assert.Null(result.Issue);
        Assert.Empty(result.Snapshot!.Games);
        Assert.Equal(1, result.SkippedRecordCount);
    }

    [Theory]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo"}""")]
    [InlineData("""{"name":"Halo","isInstalled":true}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","isInstalled":true}""")]
    [InlineData("""{"id":"11111111-1111-4111-8111-111111111111","name":"Halo","isInstalled":null}""")]
    public void MissingOrNullRequiredFieldsSkipOnlyThatRecord(string gameJson)
    {
        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + gameJson + "," + ValidGame + "]}"));

        Assert.Null(result.Issue);
        Assert.Equal("Halo", Assert.Single(result.Snapshot!.Games).Name);
        Assert.Equal(1, result.SkippedRecordCount);
    }

    [Fact]
    public void ArrayRecordsThatAreNotObjectsAreSkipped()
    {
        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[[],\"Halo\",42,true,null," + ValidGame + "]}"));

        Assert.Null(result.Issue);
        Assert.Single(result.Snapshot!.Games);
        Assert.Equal(5, result.SkippedRecordCount);
    }

    [Fact]
    public void FirstValidRecordWinsForDuplicateIds()
    {
        var games = string.Join(",", new[]
        {
            $$"""{"id":"{{GameA}}","name":"First","isInstalled":true}""",
            $$"""{"id":"{{GameB}}","name":"Other","isInstalled":true}""",
            $$"""{"id":"{{GameA}}","name":"Second","isInstalled":true}""",
        });

        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + games + "]}"));

        Assert.Equal(new[] { "First", "Other" }, result.Snapshot!.Games.Select(game => game.Name));
        Assert.Equal(1, result.SkippedRecordCount);
    }

    [Fact]
    public void RecordsThatFailModelValidationAreSkippedRatherThanRejected()
    {
        var compactId = GameA.Replace("-", string.Empty, StringComparison.Ordinal);
        var games = string.Join(",", new[]
        {
            $$"""{"id":"{{GameA}}","name":"  ","isInstalled":true}""",
            $$"""{"id":"{{compactId}}","name":"Compact","isInstalled":true}""",
            $$"""{"id":"{{GameB}}","name":"Keep","isInstalled":false}""",
        });

        var result = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + games + "]}"));

        Assert.Null(result.Issue);
        Assert.Equal("Keep", Assert.Single(result.Snapshot!.Games).Name);
        Assert.Equal(2, result.SkippedRecordCount);
    }

    [Fact]
    public void SuccessfulParseHasNoIssueAndNoSkippedRecords()
    {
        var result = CatalogSnapshotParser.Parse(Json(ValidEnvelope));

        Assert.Null(result.Issue);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(0, result.SkippedRecordCount);
    }

    [Fact]
    public void FailureResultsCarryAUsableMessageAndNoSnapshot()
    {
        var result = CatalogSnapshotParser.Parse(Json("not json"));

        Assert.Null(result.Snapshot);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
        Assert.Equal(0, result.SkippedRecordCount);
        Assert.False(string.IsNullOrWhiteSpace(result.Issue.Message));
    }

    [Theory]
    [InlineData("""{"schemaVersion":0,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    [InlineData("""{"schemaVersion":-1,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    [InlineData("""{"schemaVersion":99,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    public void AnySchemaVersionOtherThanOneIsReportedAsUnsupported(string json)
    {
        Assert.Equal(CatalogErrorCode.UnsupportedSchema, CatalogSnapshotParser.Parse(Json(json)).Issue!.Code);
    }

    [Theory]
    [InlineData("""{"schemaVersion":true,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    [InlineData("""{"schemaVersion":null,"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    [InlineData("""{"schemaVersion":[1],"generatedAtUtc":"2026-10-30T00:00:00Z","games":[]}""")]
    public void NonNumericSchemaVersionIsMalformedRatherThanUnsupported(string json)
    {
        Assert.Equal(CatalogErrorCode.Malformed, CatalogSnapshotParser.Parse(Json(json)).Issue!.Code);
    }

    [Theory]
    [InlineData("2026-02-29T00:00:00Z")]
    [InlineData("2026-10-30T24:00:00Z")]
    [InlineData("2026-13-01T00:00:00Z")]
    [InlineData("2026-10-30 00:00:00Z")]
    [InlineData("2026-10-30T00:00:00.12345678Z")]
    [InlineData(" 2026-10-30T00:00:00Z")]
    [InlineData("2026-10-30T00:00:00z")]
    public void InvalidTimestampFormsAreRejected(string timestamp)
    {
        var result = CatalogSnapshotParser.Parse(Json($$"""{"schemaVersion":1,"generatedAtUtc":"{{timestamp}}","games":[]}"""));

        Assert.Null(result.Snapshot);
        Assert.Equal(CatalogErrorCode.Malformed, result.Issue!.Code);
    }

    [Fact]
    public void TimestampAtTheExactSizeLimitIsAccepted()
    {
        var json = new StringBuilder(EnvelopePrefix).Append("[]}").ToString();
        var bytes = Encoding.UTF8.GetBytes(json);
        var padded = new byte[SnapshotLimits.MaxBytes];
        bytes.CopyTo(padded, 0);
        for (var index = bytes.Length; index < padded.Length; index++)
        {
            padded[index] = (byte)' ';
        }

        var result = CatalogSnapshotParser.Parse(padded);

        Assert.Null(result.Issue);
        Assert.Empty(result.Snapshot!.Games);
    }

    [Fact]
    public void OneByteOverTheSizeLimitIsRejectedBeforeParsing()
    {
        var result = CatalogSnapshotParser.Parse(new byte[SnapshotLimits.MaxBytes + 1]);

        Assert.Null(result.Snapshot);
        Assert.Equal(CatalogErrorCode.TooLarge, result.Issue!.Code);
    }

    [Fact]
    public void RecordCountLimitBoundaryIsExact()
    {
        var atLimit = CatalogSnapshotParser.Parse(Json(BuildGames(SnapshotLimits.MaxGames)));
        Assert.Null(atLimit.Issue);
        Assert.Equal(SnapshotLimits.MaxGames, atLimit.Snapshot!.Games.Count);

        var overLimit = CatalogSnapshotParser.Parse(Json(BuildGames(SnapshotLimits.MaxGames + 1)));
        Assert.Equal(CatalogErrorCode.TooManyGames, overLimit.Issue!.Code);
    }

    [Fact]
    public void ParsedCollectionsAreIndependentOfTheInputBuffer()
    {
        var bytes = Json(EnvelopePrefix + "[" + ValidGame + "]}");
        var result = CatalogSnapshotParser.Parse(bytes);

        Array.Clear(bytes, 0, bytes.Length);

        Assert.Equal("Halo", Assert.Single(result.Snapshot!.Games).Name);
        Assert.Equal(SnapshotLimits.SchemaVersion, result.Snapshot.SchemaVersion);
    }

    [Fact]
    public async Task ParsedSnapshotCanBeRepublishedAndParsedAgain()
    {
        // Protocol round trip: parse an untrusted file, then use the model as the next input.
        var first = CatalogSnapshotParser.Parse(Json(EnvelopePrefix + "[" + ValidGame + "]}"));
        Assert.Null(first.Issue);

        using var stream = new MemoryStream();
        await CatalogSnapshotSerializer.SerializeAsync(stream, first.Snapshot!);
        var second = CatalogSnapshotParser.Parse(stream.ToArray());

        Assert.Null(second.Issue);
        Assert.Equal(first.Snapshot!.GeneratedAtUtc, second.Snapshot!.GeneratedAtUtc);
        Assert.Equal(first.Snapshot.Games[0].Name, second.Snapshot.Games[0].Name);
    }

    private static string BuildGames(int count)
    {
        var builder = new StringBuilder(EnvelopePrefix).Append('[');
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"id\":\"")
                .Append(GuidFromIndex(index))
                .Append("\",\"name\":\"Game ")
                .Append(index)
                .Append("\",\"isInstalled\":false}");
        }

        return builder.Append("]}").ToString();
    }

    private static string GuidFromIndex(int index)
    {
        // Deterministic, canonical, non-empty GUID for high-volume synthetic records.
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, index + 1);
        bytes[15] = 0x7F;
        return new Guid(bytes).ToString("D");
    }

    private static byte[] Json(string value) => Encoding.UTF8.GetBytes(value);
}
