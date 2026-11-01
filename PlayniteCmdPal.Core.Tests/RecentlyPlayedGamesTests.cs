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
    public void UsesTitleThenOrdinalTitleThenGameIdAsStableTieBreakers()
    {
        var timestamp = new DateTime(2026, 10, 30, 12, 0, 0);
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
        var timestamp = new DateTime(2026, 10, 30, 12, 0, 0);
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
