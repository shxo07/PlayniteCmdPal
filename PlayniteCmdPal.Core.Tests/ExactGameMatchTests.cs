using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class ExactGameMatchTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private static readonly GameEntry Rally = new(GameA, "Orbit Rally 16", "Store A", true);

    [Theory]
    [InlineData("Orbit Rally 16")]
    [InlineData("orbit rally 16")]
    [InlineData("  ORBIT RALLY 16  ")]
    public void UniqueExactTitlePreservesTheCatalogTitleAndStableId(string query)
    {
        var catalog = new[] { new GameEntry(GameB, "Orbit Rally 160"), Rally };

        Assert.Equal(Rally, ExactGameMatch.Find(catalog, query));
        Assert.Equal(Rally, ExactGameMatch.Find(catalog.Reverse(), query));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Orbit")]
    [InlineData("Rally 16")]
    [InlineData("Orbit Rally")]
    [InlineData("OrbitRally16")]
    [InlineData("Orbit  Rally 16")]
    [InlineData("Orbit-Rally 16")]
    [InlineData("Unknown")]
    public void PartialFuzzyPunctuationAndNoMatchQueriesDoNotSelectAGame(string? query)
    {
        Assert.Null(ExactGameMatch.Find(new[] { Rally }, query));
    }

    [Fact]
    public void DuplicateTitlesNeverChooseBySourceInstallStateOrInputOrder()
    {
        var other = new GameEntry(GameB, "ORBIT RALLY 16", "Store B", false);

        Assert.Null(ExactGameMatch.Find(new[] { Rally, other }, Rally.Title));
        Assert.Null(ExactGameMatch.Find(new[] { other, Rally }, Rally.Title));
    }

    [Fact]
    public void IdenticalRepeatedRecordsCountAsOneGame()
    {
        Assert.Equal(Rally, ExactGameMatch.Find(new[] { Rally, Rally with { } }, Rally.Title));
    }

    [Fact]
    public void ConflictingRecordsForOneIdFailClosedRegardlessOfOrder()
    {
        var conflicting = Rally with { Title = "Orbit Rally 17" };

        Assert.Null(ExactGameMatch.Find(new[] { Rally, conflicting }, Rally.Title));
        Assert.Null(ExactGameMatch.Find(new[] { conflicting, Rally }, Rally.Title));
    }

    [Theory]
    [InlineData("not-a-game-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")]
    [InlineData("11111111111141118111111111111111")]
    public void InvalidOrNoncanonicalIdentityCannotBeALaunchCandidate(string id)
    {
        Assert.Null(ExactGameMatch.Find(new[] { Rally with { GameId = id } }, Rally.Title));
    }

    [Fact]
    public void InvalidRecordsCannotConcealAnAmbiguousMatch()
    {
        Assert.Null(ExactGameMatch.Find(new[] { Rally, null! }, Rally.Title));
        Assert.Null(ExactGameMatch.Find(new[] { Rally, new GameEntry(GameB, " ") }, Rally.Title));
        Assert.Null(ExactGameMatch.Find(new[] { Rally, Rally with { GameId = "invalid" } }, Rally.Title));
    }

    [Fact]
    public void StoredTitlesAreNotAggressivelyNormalized()
    {
        Assert.Null(ExactGameMatch.Find(new[] { Rally with { Title = " Orbit Rally 16 " } }, Rally.Title));
    }

    [Fact]
    public void EmptyCatalogHasNoMatch()
    {
        Assert.Null(ExactGameMatch.Find(Array.Empty<GameEntry>(), Rally.Title));
    }
}
