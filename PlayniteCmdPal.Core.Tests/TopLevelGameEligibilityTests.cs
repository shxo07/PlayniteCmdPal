using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class TopLevelGameEligibilityTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string GameC = "33333333-3333-4333-8333-333333333333";
    private static readonly GameEntry Rally = new(GameA, "Orbit Rally 16", "Store A", true);
    private static readonly GameEntry Orchard = new(GameB, "Moon Orchard", "Store B", false);

    [Fact]
    public void RootCatalogContainsAllUniqueIdsInDeterministicOrderRegardlessOfInstalledState()
    {
        var games = new[] { Rally, Orchard };

        var expected = new[] { Orchard, Rally };
        Assert.Equal(expected, DirectLaunchGames.GetCanonicalGames(games));
        Assert.Equal(expected, DirectLaunchGames.GetCanonicalGames(games.Reverse()));
    }

    [Fact]
    public void EqualTitlesKeepEveryStableIdWithoutChoosingBySourceInstallationStateOrInputOrder()
    {
        var other = Rally with { GameId = GameC, Source = "Store C", IsInstalled = false };
        var games = new[] { Rally, Orchard, other };
        var expected = new[] { Orchard, Rally, other };

        Assert.Equal(expected, DirectLaunchGames.GetCanonicalGames(games));
        Assert.Equal(expected, DirectLaunchGames.GetCanonicalGames(games.Reverse()));
        Assert.Equal(Rally, DirectLaunchGames.FindById(games, GameA));
        Assert.Equal(other, DirectLaunchGames.FindById(games, GameC));
    }

    [Fact]
    public void CaseOnlyTitleVariantsRemainDistinctAndAreOrderedDeterministically()
    {
        var other = Rally with { GameId = GameC, Title = "ORBIT RALLY 16" };
        var games = new[] { Rally, other };
        Assert.Equal(new[] { other, Rally }, DirectLaunchGames.GetCanonicalGames(games));
        Assert.Equal(new[] { other, Rally }, DirectLaunchGames.GetCanonicalGames(games.Reverse()));
    }

    [Fact]
    public void IdenticalRecordsRegisterOnlyOneCommandPerStableId()
    {
        var games = new[] { Rally, Rally with { } };
        Assert.Equal(new[] { Rally }, DirectLaunchGames.GetCanonicalGames(games));
        Assert.Equal(Rally, DirectLaunchGames.FindById(games, GameA));
    }

    [Fact]
    public void ConflictingRecordsForOneIdInvalidateTheCommandCatalogAndIdLookup()
    {
        var conflicting = Rally with { Title = "Orbit Rally 17" };
        var games = new[] { Rally, conflicting, Orchard };
        Assert.Empty(DirectLaunchGames.GetCanonicalGames(games));
        Assert.Empty(DirectLaunchGames.GetCanonicalGames(games.Reverse()));
        Assert.Null(DirectLaunchGames.FindById(games, GameA));
        Assert.Null(DirectLaunchGames.FindById(games.Reverse(), GameA));
        Assert.Null(DirectLaunchGames.FindById(games, GameB));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")]
    [InlineData("11111111111141118111111111111111")]
    public void InvalidOrNoncanonicalIdsFailClosed(string? id)
    {
        Assert.Empty(DirectLaunchGames.GetCanonicalGames(new[] { Orchard, Rally with { GameId = id! } }));
        Assert.Null(DirectLaunchGames.FindById(new[] { Rally }, id));
    }

    [Fact]
    public void NullAndBlankRecordsFailClosedAndEmptyCatalogHasNoCommands()
    {
        Assert.Empty(DirectLaunchGames.GetCanonicalGames(new[] { Rally, null! }));
        Assert.Empty(DirectLaunchGames.GetCanonicalGames(new[] { Rally, Orchard with { Title = " " } }));
        Assert.Empty(DirectLaunchGames.GetCanonicalGames(Array.Empty<GameEntry>()));
        Assert.Null(DirectLaunchGames.FindById(new[] { Rally, null! }, GameA));
        Assert.Null(DirectLaunchGames.FindById(new[] { Rally, Orchard with { Title = " " } }, GameA));
        Assert.Null(DirectLaunchGames.FindById(Array.Empty<GameEntry>(), GameA));
    }

    [Fact]
    public void IdLookupDoesNotResolveAnotherGameByTitleOrNormalizeDisplayText()
    {
        var padded = Rally with { Title = " Orbit Rally 16 " };
        Assert.Equal(padded, Assert.Single(DirectLaunchGames.GetCanonicalGames(new[] { padded })));
        Assert.Equal(padded, DirectLaunchGames.FindById(new[] { padded }, GameA));
        Assert.Null(DirectLaunchGames.FindById(new[] { Rally }, GameB));
        Assert.Null(DirectLaunchGames.FindById(new[] { Rally }, Rally.Title));
    }

    [Fact]
    public void InputIsEnumeratedOnceAndPublishedCandidateCollectionIsReadOnly()
    {
        var enumerations = 0;
        IEnumerable<GameEntry> Catalog()
        {
            enumerations++;
            yield return Rally;
            yield return Orchard;
        }

        var candidates = DirectLaunchGames.GetCanonicalGames(Catalog());

        Assert.Equal(1, enumerations);
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<GameEntry>>(candidates).Add(Rally));
        Assert.Equal(Rally, DirectLaunchGames.FindById(Catalog(), GameA));
        Assert.Equal(2, enumerations);
    }

    [Fact]
    public void LargeCommandCatalogIsDeterministicWithoutPerGameRescanning()
    {
        var games = Enumerable.Range(1, 5_000).Select(index =>
            new GameEntry(new Guid(index, 0, 0, new byte[8]).ToString("D"), $"Synthetic game {index:D5}")).ToArray();

        Assert.Equal(games, DirectLaunchGames.GetCanonicalGames(games.Reverse()));
    }
}
