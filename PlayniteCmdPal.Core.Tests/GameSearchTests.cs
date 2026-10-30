using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class GameSearchTests
{
    [Fact]
    public void RanksExactBeforePrefixWordPrefixAndSubstring()
    {
        var catalog = new[]
        {
            new GameEntry("substring", "Superhalo"),
            new GameEntry("word", "The Halo Effect"),
            new GameEntry("prefix", "Halo Infinite"),
            new GameEntry("exact", "Halo"),
        };

        var results = GameSearch.Search(catalog, "halo");

        Assert.Equal(new[] { "exact", "prefix", "word", "substring" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void MatchesWithoutCaseSensitivity()
    {
        var results = GameSearch.Search(
            new[] { new GameEntry("1", "HALO"), new GameEntry("2", "Halo Infinite") },
            "hAlO");

        Assert.Equal(new[] { "1", "2" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void RanksEarlierWordPrefixBeforeLaterWordPrefix()
    {
        var results = GameSearch.Search(
            new[] { new GameEntry("later", "Super Halo"), new GameEntry("earlier", "The Halo Effect") },
            "halo");

        Assert.Equal(new[] { "earlier", "later" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void RanksWordPrefixAheadOfSubstringWhenPunctuationDefinesTheWordBoundary()
    {
        var results = GameSearch.Search(
            new[] { new GameEntry("substring", "TheHaloEffect"), new GameEntry("word", "The-Halo-Effect") },
            "halo");

        Assert.Equal(new[] { "word", "substring" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void PreservesDistinctGamesWithDuplicateDisplayTitles()
    {
        var results = GameSearch.Search(
            new[] { new GameEntry("game-b", "Same Title"), new GameEntry("game-a", "Same Title") },
            "same title");

        Assert.Equal(new[] { "game-a", "game-b" }, results.Select(game => game.GameId));
        Assert.All(results, game => Assert.Equal("Same Title", game.Title));
    }

    [Fact]
    public void UsesStableOrdinalTieBreaksForEquivalentTitles()
    {
        var catalog = new[]
        {
            new GameEntry("z", "alpha"),
            new GameEntry("b", "Alpha"),
            new GameEntry("a", "Alpha"),
        };

        var results = GameSearch.Search(catalog, "a");

        Assert.Equal(new[] { "a", "b", "z" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void ChoosesOneDeterministicCanonicalEntryForDuplicateGameIds()
    {
        var entries = new[]
        {
            new GameEntry("same-id", "Zelda", "Store Z"),
            new GameEntry("same-id", "Alpha", "Store A"),
        };

        Assert.Equal("Alpha", Assert.Single(GameSearch.Search(entries, "alpha")).Title);
        Assert.Equal("Alpha", Assert.Single(GameSearch.Search(entries.Reverse(), "alpha")).Title);
    }

    [Fact]
    public void UsesPlatformAsAnOrdinalTieBreakWithoutChangingIdentity()
    {
        var catalog = new[]
        {
            new GameEntry("switch", "Same Title", Platform: "Switch"),
            new GameEntry("pc", "Same Title", Platform: "PC"),
        };

        var results = GameSearch.Search(catalog, "same title");

        Assert.Equal(2, results.Count);
        Assert.Equal("pc", results[0].GameId);
        Assert.Equal("switch", results[1].GameId);
        Assert.Equal("PC", results[0].Platform);
        Assert.Equal("Switch", results[1].Platform);
    }

    [Fact]
    public void ChoosesDeterministicPlatformForDuplicateGameIds()
    {
        var entries = new[]
        {
            new GameEntry("same-id", "Same Title", Platform: "Switch"),
            new GameEntry("same-id", "Same Title", Platform: "PC"),
        };

        Assert.Equal("PC", Assert.Single(GameSearch.Search(entries, "same title")).Platform);
        Assert.Equal("PC", Assert.Single(GameSearch.Search(entries.Reverse(), "same title")).Platform);
    }

    [Fact]
    public void RepeatedSearchesReturnTheSameOrder()
    {
        var catalog = new[]
        {
            new GameEntry("3", "The Halo Effect"),
            new GameEntry("2", "Halo Infinite"),
            new GameEntry("1", "Halo"),
            new GameEntry("4", "Superhalo"),
            new GameEntry("5", "Halo Infinite"),
        };

        var first = GameSearch.Search(catalog, "halo").Select(game => game.GameId).ToArray();

        for (var run = 0; run < 20; run++)
        {
            Assert.Equal(first, GameSearch.Search(catalog, "halo").Select(game => game.GameId));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceQueryReturnsNoResults(string query)
    {
        var results = GameSearch.Search(new[] { new GameEntry("1", "Halo") }, query);

        Assert.Empty(results);
    }

    [Fact]
    public void NullQueryReturnsNoResults()
    {
        Assert.Empty(GameSearch.Search(new[] { new GameEntry("1", "Halo") }, null));
    }

    [Fact]
    public void EmptyCatalogReturnsNoResults()
    {
        Assert.Empty(GameSearch.Search(Array.Empty<GameEntry>(), "halo"));
    }

    [Fact]
    public void RejectsMissingStableIdOrDisplayTitle()
    {
        Assert.Throws<ArgumentException>(() => new GameEntry(" ", "Halo").Validate());
        Assert.Throws<ArgumentException>(() => new GameEntry("id", " ").Validate());
    }

    [Fact]
    public void RejectsNullCatalog()
    {
        Assert.Throws<ArgumentNullException>(() => GameSearch.Search(null!, "halo"));
    }
}
