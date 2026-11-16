using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class InstalledGameMatchTests
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string GameC = "33333333-3333-4333-8333-333333333333";
    private const string Title = "Orbit Rally 16";
    private static readonly GameEntry Installed = new(GameA, Title, "Store A", true);

    [Fact]
    public void FindsOneInstalledExactTitleIgnoringCaseAndOuterQueryWhitespace()
    {
        Assert.Equal(Installed, InstalledGameMatch.Find(
            [new GameEntry(GameB, "Moon Orchard", IsInstalled: true), Installed],
            "  ORBIT RALLY 16  "));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void UninstalledOrUnknownInstallationStateNeverProducesAMatch(bool? isInstalled)
    {
        Assert.Null(InstalledGameMatch.Find(
            [Installed with { IsInstalled = isInstalled }], Title));
    }

    [Fact]
    public void UninstalledDuplicateTitleDoesNotHideTheUniqueInstalledChoice()
    {
        var uninstalledDuplicate = Installed with { GameId = GameB, IsInstalled = false };
        Assert.Equal(Installed, InstalledGameMatch.Find([uninstalledDuplicate, Installed], Title));
    }

    [Fact]
    public void MultipleInstalledGamesWithTheSameTitleAreAmbiguous()
    {
        var otherInstalled = Installed with { GameId = GameB, Source = "Store B" };
        Assert.Null(InstalledGameMatch.Find([Installed, otherInstalled], Title));
    }

    [Fact]
    public void IdenticalDuplicateRecordsCountOnceButConflictingDuplicateIdsFailClosed()
    {
        Assert.Equal(Installed, InstalledGameMatch.Find([Installed, Installed with { }], Title));
        Assert.Null(InstalledGameMatch.Find(
            [Installed, Installed with { Title = "Orbit Rally 17" }], Title));
    }

    [Theory]
    [InlineData("Orbit")]
    [InlineData("Unknown Game")]
    [InlineData("")]
    [InlineData("   ")]
    public void PartialUnknownAndBlankQueriesDoNotSelectAGame(string query)
    {
        Assert.Null(InstalledGameMatch.Find([Installed], query));
    }

    [Fact]
    public void InvalidCatalogRecordsFailClosedEvenWhenTheyAreNotInstalledMatches()
    {
        Assert.Null(InstalledGameMatch.Find(
            [Installed, new GameEntry("invalid-id", "Unrelated", IsInstalled: false)], Title));
        Assert.Null(InstalledGameMatch.Find([Installed, null!], Title));
        Assert.Null(InstalledGameMatch.Find([Installed with { Title = " " }], Title));
    }

    [Fact]
    public void RepeatedTitlesAreComparedOnlyAmongInstalledEntries()
    {
        var games = new[]
        {
            Installed,
            Installed with { GameId = GameB, IsInstalled = false },
            Installed with { GameId = GameC, Title = "Moon Orchard", IsInstalled = true },
        };

        Assert.Equal(Installed, InstalledGameMatch.Find(games, Title));
        Assert.Equal(games[2], InstalledGameMatch.Find(games, "Moon Orchard"));
    }
}
