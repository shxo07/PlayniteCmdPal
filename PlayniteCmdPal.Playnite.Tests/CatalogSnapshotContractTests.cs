using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

/// <summary>Contract tests for the shared versioned snapshot model that run without Playnite.</summary>
public sealed class CatalogSnapshotContractTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyLibraryIsAValidSnapshot()
    {
        var snapshot = new CatalogSnapshot(Timestamp, Array.Empty<SnapshotGame>());

        Assert.Equal(1, snapshot.SchemaVersion);
        Assert.Equal(Timestamp, snapshot.GeneratedAtUtc);
        Assert.Empty(snapshot.Games);
    }

    [Fact]
    public void GamesAreCopiedSoLaterListMutationCannotChangeTheSnapshot()
    {
        var games = new List<SnapshotGame> { new(GameA, "Halo", true) };
        var snapshot = new CatalogSnapshot(Timestamp, games);

        games.Add(new SnapshotGame(GameB, "Halo 2", false));

        Assert.Single(snapshot.Games);
        Assert.Equal(GameA, snapshot.Games[0].Id);
    }

    [Fact]
    public void SnapshotGamesAreReadOnly()
    {
        var snapshot = new CatalogSnapshot(Timestamp, new[] { new SnapshotGame(GameA, "Halo", true) });

        Assert.Throws<NotSupportedException>(() => ((IList<SnapshotGame>)snapshot.Games).Add(new SnapshotGame(GameB, "Other", false)));
        Assert.Throws<NotSupportedException>(() => ((IList<SnapshotGame>)snapshot.Games)[0] = new SnapshotGame(GameB, "Other", false));
    }

    [Fact]
    public void NonUtcTimestampIsRejected()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new CatalogSnapshot(Timestamp.ToOffset(TimeSpan.FromHours(4)), Array.Empty<SnapshotGame>()));

        Assert.Equal("generatedAtUtc", error.ParamName);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void TimestampConvertedToZeroOffsetIsAccepted()
    {
        var utc = new DateTimeOffset(2026, 10, 30, 4, 0, 0, TimeSpan.FromHours(4));

        var snapshot = new CatalogSnapshot(utc.ToUniversalTime(), Array.Empty<SnapshotGame>());

        Assert.Equal(TimeSpan.Zero, snapshot.GeneratedAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), snapshot.GeneratedAtUtc);
    }

    [Fact]
    public void NullGameListIsRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new CatalogSnapshot(Timestamp, null!));

        Assert.Equal("games", error.ParamName);
    }

    [Fact]
    public void DuplicateGameIdsAreRejected()
    {
        var error = Assert.Throws<ArgumentException>(() => new CatalogSnapshot(Timestamp, new[]
        {
            new SnapshotGame(GameA, "Halo", true),
            new SnapshotGame(GameA, "Halo", false),
        }));

        Assert.Equal("games", error.ParamName);
    }

    [Fact]
    public void NullRecordInTheGameListIsRejected()
    {
        var error = Assert.Throws<ArgumentException>(() => new CatalogSnapshot(Timestamp, new[]
        {
            new SnapshotGame(GameA, "Halo", true),
            null!,
        }));

        Assert.Equal("games", error.ParamName);
    }

    [Fact]
    public void GameListBeyondTheRecordLimitIsRejectedBeforeEnumeration()
    {
        var games = new StubReadOnlyList<SnapshotGame>(SnapshotLimits.MaxGames + 1);

        // The limit is checked before any record is read.
        Assert.Throws<ArgumentException>(() => new CatalogSnapshot(Timestamp, games));
    }

    [Fact]
    public void GameListAtTheRecordLimitIsAccepted()
    {
        var games = Enumerable.Range(0, SnapshotLimits.MaxGames)
            .Select(index => new SnapshotGame(DeterministicGuid(index), "Game " + index, false))
            .ToArray();

        var snapshot = new CatalogSnapshot(Timestamp, games);

        Assert.Equal(SnapshotLimits.MaxGames, snapshot.Games.Count);
    }

    [Fact]
    public void SnapshotGameTrimsTextFieldsButNeverTheIdentity()
    {
        var game = new SnapshotGame(GameA, "  Halo  ", true, "  Steam ", "  PC  ");

        // Identity is preserved exactly; padding around a GUID is rejected rather than trimmed.
        Assert.Equal(GameA, game.Id);
        Assert.Throws<ArgumentException>(() => new SnapshotGame(" " + GameA + " ", "Halo", true));

        Assert.Equal("Halo", game.Name);
        Assert.Equal("Steam", game.Source);
        Assert.Equal("PC", game.Platform);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" " + GameA)]
    [InlineData(GameA + " ")]
    public void GameIdMustAlreadyBeCanonicalBecauseTrimmingCouldChangeIdentity(string id)
    {
        Assert.Throws<ArgumentException>(() => new SnapshotGame(id, "Halo", true));
    }

    [Theory]
    [InlineData("11111111111141118111111111111111")]
    [InlineData("{11111111-1111-4111-8111-111111111111}")]
    [InlineData("11111111-1111-4111-8111-11111111111")]
    [InlineData("ABCDEF01-2345-4678-9ABC-DEF012345678")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    public void NonCanonicalOrEmptyGuidsAreRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => new SnapshotGame(id, "Halo", true));
        Assert.False(SnapshotValidation.IsCanonicalGameId(id));
    }

    [Fact]
    public void LowercaseGuidIsTheIdentityMemberOfTheContract()
    {
        var game = new SnapshotGame(GameA, "Halo", true);

        Assert.True(SnapshotValidation.IsCanonicalGameId(game.Id));
        Assert.Equal(GameA, game.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t\n")]
    public void GameNameIsRequired(string name)
    {
        Assert.Throws<ArgumentException>(() => new SnapshotGame(GameA, name, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void BlankOptionalFieldsBecomeNullRatherThanEmptyStrings(string? value)
    {
        var game = new SnapshotGame(GameA, "Halo", true, value, value);

        Assert.Null(game.Source);
        Assert.Null(game.Platform);
    }

    [Fact]
    public void InstallStateIsPreservedExactly()
    {
        Assert.True(new SnapshotGame(GameA, "Halo", true).IsInstalled);
        Assert.False(new SnapshotGame(GameA, "Halo", false).IsInstalled);
    }

    [Fact]
    public void SiblingOptionalFieldsAreIndependent()
    {
        var game = new SnapshotGame(GameA, "Halo", true, " Steam ", null);

        Assert.Equal("Steam", game.Source);
        Assert.Null(game.Platform);
    }

    [Fact]
    public void SchemaConstantsAreStable()
    {
        // Changing any of these values changes the v1 transport contract and needs a new schema.
        Assert.Equal(1, SnapshotLimits.LegacySchemaVersion);
        Assert.Equal(2, SnapshotLimits.SchemaVersion);
        Assert.Equal(32 * 1024 * 1024, SnapshotLimits.MaxBytes);
        Assert.Equal(100_000, SnapshotLimits.MaxGames);
        Assert.Equal(16, SnapshotLimits.MaxJsonDepth);
    }


    [Theory]
    [InlineData("""11111111-1111-4111-8111-111111111111\22222222-2222-4222-8222-222222222222.png""", true)]
    [InlineData("11111111-1111-4111-8111-111111111111/22222222-2222-4222-8222-222222222222.webp", true)]
    [InlineData("C:\\\\games\\\\cover.png", false)]
    [InlineData("https://example.invalid/cover.png", false)]
    [InlineData("..\\\\22222222-2222-4222-8222-222222222222.png", false)]
    [InlineData("33333333-3333-4333-8333-333333333333\\\\22222222-2222-4222-8222-222222222222.png", false)]
    [InlineData("11111111-1111-4111-8111-111111111111\\\\22222222-2222-4222-8222-222222222222.bmp", false)]
    public void PlayniteArtworkReferencesMustBeCanonicalGameOwnedDatabaseRefs(string reference, bool expected)
    {
        Assert.Equal(expected, ArtworkReferenceValidator.TryValidate(GameA, reference, out _));
    }

    [Fact]
    public void ArtworkSignatureValidationRejectsTruncatedAndUnknownFiles()
    {
        Assert.False(ArtworkFileSignature.TryGetExtension(new byte[] { 0xFF, 0xD8 }, out _));
        Assert.False(ArtworkFileSignature.TryGetExtension(new byte[] { 1, 2, 3, 4 }, out _));
        Assert.False(ArtworkFileSignature.TryGetExtension(new byte[32], out _));
    }

    [Fact]
    public void ArtworkSignatureValidationRecognizesContainerSignaturesWithoutClaimingDecodability()
    {
        // A real 1x1 PNG fixture complements the minimal marker-only format probes below.
        var realPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lYQAAAAASUVORK5CYII=");
        Assert.True(ArtworkFileSignature.TryGetExtension(realPng, out var realPngExtension));
        Assert.Equal(".png", realPngExtension);

        var png = new byte[24];
        byte[] pngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Array.Copy(pngHeader, png, pngHeader.Length);
        png[16] = (byte)'I'; png[17] = (byte)'E'; png[18] = (byte)'N'; png[19] = (byte)'D';
        Assert.True(ArtworkFileSignature.TryGetExtension(png, out var pngExtension));
        Assert.Equal(".png", pngExtension);

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        Assert.True(ArtworkFileSignature.TryGetExtension(jpeg, out var jpegExtension));
        Assert.Equal(".jpg", jpegExtension);

        var gif = new byte[14];
        gif[0] = (byte)'G'; gif[1] = (byte)'I'; gif[2] = (byte)'F'; gif[13] = 0x3B;
        Assert.True(ArtworkFileSignature.TryGetExtension(gif, out var gifExtension));
        Assert.Equal(".gif", gifExtension);

        var webp = new byte[16];
        webp[0] = (byte)'R'; webp[1] = (byte)'I'; webp[2] = (byte)'F'; webp[3] = (byte)'F';
        webp[4] = 8; webp[8] = (byte)'W'; webp[9] = (byte)'E'; webp[10] = (byte)'B'; webp[11] = (byte)'P';
        Assert.True(ArtworkFileSignature.TryGetExtension(webp, out var webpExtension));
        Assert.Equal(".webp", webpExtension);
    }

    private static string DeterministicGuid(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index + 1).CopyTo(bytes, 0);
        bytes[15] = 0x2A;
        return new Guid(bytes).ToString("D");
    }

    /// <summary>A read-only list that reports a large count and fails if any record is read.</summary>
    private sealed class StubReadOnlyList<T>(int count) : IReadOnlyList<T>
    {
        public int Count => count;

        public T this[int index] => throw new InvalidOperationException($"Unexpected indexer read at {index}.");

        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Unexpected enumeration.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
