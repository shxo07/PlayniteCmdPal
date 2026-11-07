using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class RecentlyPlayedGamesTests
{
    [Fact]
    public void SortsNewestFirstAndExcludesGamesWithoutHistory()
    {
        var games = new[]
        {
            new GameEntry("old", "Older", LastActivity: new DateTime(2026, 10, 28, 9, 0, 0)),
            new GameEntry("never", "Never played"),
            new GameEntry("new", "Newest", LastActivity: new DateTime(2026, 10, 30, 12, 0, 0)),
        };
        Assert.Equal(new[] { "new", "old" }, RecentlyPlayedGames.Sort(games).Select(game => game.GameId));
    }

    [Fact]
    public void ComparesUtcAndLocalValuesAsInstantsRatherThanWallClockTicks()
    {
        var utcEleven = new DateTime(2026, 10, 30, 11, 0, 0, DateTimeKind.Utc);
        var localTen = DateTime.SpecifyKind(utcEleven.AddHours(-1).ToLocalTime(), DateTimeKind.Local);
        var games = new[]
        {
            new GameEntry("newer", "UTC eleven", LastActivity: utcEleven),
            new GameEntry("older", "Local ten", LastActivity: localTen),
        };

        Assert.Equal(new[] { "newer", "older" }, RecentlyPlayedGames.Sort(games).Select(game => game.GameId));
    }

    [Fact]
    public void TreatsUnspecifiedValuesAsLocalMachineTime()
    {
        var unspecified = DateTime.SpecifyKind(new DateTime(2026, 10, 30, 10, 0, 0), DateTimeKind.Unspecified);
        var expectedUtc = unspecified.ToUniversalTime();
        var equivalentUtc = new DateTime(expectedUtc.Ticks, DateTimeKind.Utc);
        var games = new[]
        {
            new GameEntry("unspecified", "Unspecified", LastActivity: unspecified),
            new GameEntry("utc", "Equivalent UTC", LastActivity: equivalentUtc),
        };

        Assert.Equal(new[] { "utc", "unspecified" }, RecentlyPlayedGames.Sort(games).Select(game => game.GameId));
    }

    [Fact]
    public void UsesTitleThenOrdinalTitleThenGameIdAsStableTieBreakers()
    {
        var timestamp = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var games = new[]
        {
            new GameEntry("z", "alpha", LastActivity: timestamp),
            new GameEntry("b", "Alpha", LastActivity: timestamp),
            new GameEntry("a", "Alpha", LastActivity: timestamp),
            new GameEntry("c", "Beta", LastActivity: timestamp),
        };
        Assert.Equal(new[] { "a", "b", "z", "c" }, RecentlyPlayedGames.Sort(games).Select(game => game.GameId));
    }

    [Fact]
    public void OrderingIsDeterministicAcrossRepeatedRuns()
    {
        var timestamp = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var games = new[] { new GameEntry("2", "Same", LastActivity: timestamp), new GameEntry("1", "Same", LastActivity: timestamp) };
        for (var run = 0; run < 20; run++)
            Assert.Equal(new[] { "1", "2" }, RecentlyPlayedGames.Sort(games).Select(game => game.GameId));
    }

    [Fact]
    public void EmptyOrMissingHistoryProducesNoResults()
    {
        Assert.Empty(RecentlyPlayedGames.Sort(Array.Empty<GameEntry>()));
        Assert.Empty(RecentlyPlayedGames.Sort(new[] { new GameEntry("never", "Never played") }));
    }

    [Fact]
    public void RejectsNullCatalog() => Assert.Throws<ArgumentNullException>(() => RecentlyPlayedGames.Sort(null!));
}
