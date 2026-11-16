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
    public void FormatsRelativeTimesAcrossUnitsAndUsesFutureWording()
    {
        var now = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("just now", RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddSeconds(-35).UtcDateTime, now));
        Assert.Equal("1 minute ago", RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddMinutes(-1).UtcDateTime, now));
        Assert.Equal("2 hours ago", RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddHours(-2).UtcDateTime, now));
        Assert.Equal("3 days ago", RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddDays(-3).UtcDateTime, now));
        Assert.Equal("in 5 minutes", RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddMinutes(5).UtcDateTime, now));
        Assert.Equal("in a moment", RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddSeconds(20).UtcDateTime, now));
    }

    [Theory]
    [InlineData(59, "59 minutes ago")]
    [InlineData(60, "1 hour ago")]
    [InlineData(1439, "23 hours ago")]
    [InlineData(1440, "1 day ago")]
    [InlineData(10079, "6 days ago")]
    [InlineData(10080, "1 week ago")]
    [InlineData(43200, "1 month ago")]
    [InlineData(525600, "1 year ago")]
    public void RelativeTimeFormattingUsesDocumentedUnitBoundaries(int minutesAgo, string expected)
    {
        var now = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(
            expected,
            RecentlyPlayedGames.FormatRelativeLastPlayed(now.AddMinutes(-minutesAgo).UtcDateTime, now));
    }

    [Fact]
    public void RelativeFormattingUsesInstantsForLocalAndUnspecifiedValues()
    {
        var now = DateTimeOffset.UtcNow;
        var utc = now.AddHours(-3).UtcDateTime;
        var local = utc.ToLocalTime();
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        Assert.Equal(
            RecentlyPlayedGames.FormatRelativeLastPlayed(utc, now),
            RecentlyPlayedGames.FormatRelativeLastPlayed(local, now));
        Assert.Equal(
            RecentlyPlayedGames.FormatRelativeLastPlayed(local, now),
            RecentlyPlayedGames.FormatRelativeLastPlayed(unspecified, now));
    }

    [Fact]
    public void SearchFiltersOnlyGamesAndAlwaysRetainsStatusAndRecoveryItems()
    {
        var status = new[] { "Refresh", "Catalog is stale", "Some records were skipped" };
        var games = new[] { "Halo", "Portal" };

        var visible = RecentlyPlayedGames.ComposeVisibleItems(status, games, "zelda", item => item);

        Assert.Equal(status, visible);
        Assert.DoesNotContain("Halo", visible);
        Assert.DoesNotContain("Portal", visible);
    }

    [Fact]
    public void TrimsSearchQueryBeforeFilteringGameItems()
    {
        var status = new[] { "Catalog is stale" };
        var games = new[] { "Halo Infinite", "Portal 2" };

        Assert.Equal(
            new[] { "Catalog is stale", "Halo Infinite" },
            RecentlyPlayedGames.ComposeVisibleItems(status, games, "  halo  ", item => item));
    }

    [Fact]
    public void BlankSearchIncludesAllStatusAndGameItems()
    {
        var status = new[] { "Refresh" };
        var games = new[] { "Halo", "Portal" };

        Assert.Equal(
            new[] { "Refresh", "Halo", "Portal" },
            RecentlyPlayedGames.ComposeVisibleItems(status, games, "  ", item => item));
    }


    [Fact]
    public void DistinguishesEmptyLibraryFromAllInvalidAndUnplayedRecords()
    {
        Assert.Equal(RecentlyPlayedEmptyState.EmptyLibrary, RecentlyPlayedGames.GetEmptyState(0, 0));
        Assert.Equal(RecentlyPlayedEmptyState.NoUsableRecords, RecentlyPlayedGames.GetEmptyState(0, 4));
        Assert.Equal(RecentlyPlayedEmptyState.NoPlayHistory, RecentlyPlayedGames.GetEmptyState(5, 2));
    }

    [Fact]
    public void RejectsNullCatalog() => Assert.Throws<ArgumentNullException>(() => RecentlyPlayedGames.Sort(null!));
}
