using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

/// <summary>
/// Additional ranking, matching, and stability contract tests. The baseline ranking order is
/// covered by <see cref="GameSearchTests"/>; these cases pin the documented edges.
/// </summary>
public sealed class GameSearchRankingTests
{
    [Fact]
    public void QueryIsTrimmedBeforeMatching()
    {
        var catalog = new[]
        {
            new GameEntry("substring", "Superhalo"),
            new GameEntry("word", "The Halo Effect"),
            new GameEntry("prefix", "Halo Infinite"),
            new GameEntry("exact", "Halo"),
        };

        var padded = GameSearch.Search(catalog, "  halo  ");

        Assert.Equal(GameSearch.Search(catalog, "halo"), padded);
        Assert.Equal(new[] { "exact", "prefix", "word", "substring" }, padded.Select(game => game.GameId));
    }

    [Fact]
    public void TitleWhitespaceIsNotTrimmedDuringSearch()
    {
        // The catalog contract supplies trimmed names, so search does not invent a trimmed copy.
        // A padded stored title therefore ranks as a prefix match, not an exact match.
        var padded = new GameEntry("padded", " Halo");
        var exact = new GameEntry("exact", "Halo");

        var results = GameSearch.Search(new[] { padded, exact }, "halo");

        Assert.Equal(new[] { "exact", "padded" }, results.Select(game => game.GameId));
    }

    [Theory]
    [InlineData("halo")]
    [InlineData("HALO")]
    [InlineData("hAlO")]
    [InlineData("HaLO")]
    public void CaseVariantsProduceTheSameRanking(string query)
    {
        var catalog = new[]
        {
            new GameEntry("substring", "Superhalo"),
            new GameEntry("word", "the halo effect"),
            new GameEntry("prefix", "HALO INFINITE"),
            new GameEntry("exact", "halo"),
        };

        var results = GameSearch.Search(catalog, query);

        Assert.Equal(new[] { "exact", "prefix", "word", "substring" }, results.Select(game => game.GameId));
    }

    [Theory]
    [InlineData("Halo Infinite", "halo")]
    [InlineData("Halo Infinite", "infinite")]
    [InlineData("Halo Infinite", "halo inf")]
    [InlineData("Half-Life 2", "half-life")]
    [InlineData("Half-Life 2", "life")]
    [InlineData("Ori and the Blind Forest", "the blind")]
    public void MultiWordAndPrefixQueriesMatchAsDocumented(string title, string query)
    {
        var results = GameSearch.Search(new[] { new GameEntry("game", title) }, query);

        Assert.Single(results);
    }

    [Fact]
    public void QueryEqualToAnyCatalogTitleRanksThatTitleFirst()
    {
        var catalog = new[]
        {
            new GameEntry("substring", "Superhalo"),
            new GameEntry("word", "The Halo Effect"),
            new GameEntry("prefix", "Halo Infinite"),
            new GameEntry("exact", "Halo"),
        };

        // Any entry whose title equals the query is an exact match, whatever its other fields.
        Assert.Equal("exact", GameSearch.Search(catalog, "Halo")[0].GameId);
        Assert.Equal("prefix", GameSearch.Search(catalog, "Halo Infinite")[0].GameId);
        Assert.Equal("word", GameSearch.Search(catalog, "The Halo Effect")[0].GameId);
        Assert.Equal("substring", GameSearch.Search(catalog, "Superhalo")[0].GameId);

        // Only titles that actually contain the query are returned, so corpus-wide counts vary.
        Assert.Equal(4, GameSearch.Search(catalog, "Halo").Count);
        Assert.Single(GameSearch.Search(catalog, "Superhalo"));
    }

    [Fact]
    public void MatchesEveryWordPrefixAndReportsTheEarliestOne()
    {
        var results = GameSearch.Search(
            new[]
            {
                new GameEntry("later", "Super Mega Halo"),
                new GameEntry("earliest", "Halo Wars"),
            },
            "halo");

        // "earliest" is a prefix (rank 1); "later" is a word prefix at index 11 (rank 2).
        Assert.Equal(new[] { "earliest", "later" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void WordPrefixIndexBreaksTiesBetweenOtherwiseEquivalentEntries()
    {
        var results = GameSearch.Search(
            new[]
            {
                new GameEntry("second", "Star Control Halo"),
                new GameEntry("first", "Star Halo"),
            },
            "halo");

        Assert.Equal(new[] { "first", "second" }, results.Select(game => game.GameId));
    }

    [Theory]
    [InlineData("Halo", "ha")]
    [InlineData("Halo", "halo")]
    [InlineData("Halo", "Hal")]
    public void PrefixQueriesRankBeforeWordPrefixAndSubstringMatches(string title, string query)
    {
        var results = GameSearch.Search(
            new[]
            {
                new GameEntry("substring", "The" + title),
                new GameEntry("word", "The " + title),
                new GameEntry("prefix", title + " Infinite"),
            },
            query);

        Assert.Equal(new[] { "prefix", "word", "substring" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void OnlyNonAlphanumericCharactersCreateAWordBoundary()
    {
        // A word prefix needs an alphanumeric match start preceded by a non-alphanumeric
        // character. A digit before the match is alphanumeric, so it is not a boundary.
        var results = GameSearch.Search(
            new[]
            {
                new GameEntry("joined", "PreyHalo"),
                new GameEntry("digit-before", "Prey2Halo"),
                new GameEntry("punctuation-before", "Prey:Halo"),
            },
            "halo");

        Assert.Equal(
            new[] { "punctuation-before", "joined", "digit-before" },
            results.Select(game => game.GameId));
    }

    [Fact]
    public void UnderscoreCreatesAWordBoundaryBecauseItIsNotAlphanumeric()
    {
        var results = GameSearch.Search(
            new[]
            {
                new GameEntry("joined", "PreyHalo"),
                new GameEntry("separated", "Prey_Halo"),
            },
            "halo");

        Assert.Equal(new[] { "separated", "joined" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void QueryLongerThanAnyTitleReturnsNoResults()
    {
        var results = GameSearch.Search(
            new[] { new GameEntry("1", "Halo"), new GameEntry("2", "Halo 2") },
            "Halo Infinite");

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("zzz")]
    [InlineData("Halo 3")]
    public void UnmatchedQueryReturnsNoResults(string query)
    {
        Assert.Empty(GameSearch.Search(
            new[] { new GameEntry("1", "Halo"), new GameEntry("2", "Halo 2") },
            query));
    }

    [Fact]
    public void TitlesThatContainNoneOfTheQueryCharactersNeverMatch()
    {
        Assert.Empty(GameSearch.Search(new[] { new GameEntry("1", "!!!") }, "halo"));
    }

    [Theory]
    [InlineData("HALO")]
    [InlineData("halo")]
    [InlineData("HaLo")]
    public void ExactMatchIsCaseInsensitive(string title)
    {
        var results = GameSearch.Search(new[] { new GameEntry("1", title) }, "hAlO");

        Assert.Equal(title, Assert.Single(results).Title);
    }

    [Fact]
    public void DistinctGameIdsWithTheSameTitleAreOrderedByGameId()
    {
        var catalog = new[]
        {
            new GameEntry("id-9", "Doom"),
            new GameEntry("id-1", "Doom"),
            new GameEntry("ID-5", "Doom"),
        };

        var results = GameSearch.Search(catalog, "doom");

        Assert.Equal(new[] { "ID-5", "id-1", "id-9" }, results.Select(game => game.GameId));
    }

    [Fact]
    public void DuplicateGameIdsCollapseToTheCanonicalEntryRegardlessOfInputOrder()
    {
        var entries = new[]
        {
            new GameEntry("dup", "Zeta", null, null, null, "Switch"),
            new GameEntry("dup", "Zeta", "GOG", false, null, null),
            new GameEntry("dup", "Zeta", "Steam", false, null, "PC"),
        };

        var forward = Assert.Single(GameSearch.Search(entries, "zeta"));
        var reversed = Assert.Single(GameSearch.Search(entries.Reverse(), "zeta"));

        // Ordering inside the duplicate group is ordinal: null source sorts first.
        Assert.Null(forward.Source);
        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void CanonicalEntryIsStableWhenDuplicateIdsDifferOnlyByInstalledState()
    {
        var entries = new[]
        {
            new GameEntry("dup", "Zeta", "Steam", true),
            new GameEntry("dup", "Zeta", "Steam", false),
        };

        Assert.False(Assert.Single(GameSearch.Search(entries, "zeta")).IsInstalled);
        Assert.False(Assert.Single(GameSearch.Search(entries.Reverse(), "zeta")).IsInstalled);
    }

    [Fact]
    public void PlatformAndSourceTieBreaksDoNotRemoveDistinctDatabaseEntries()
    {
        var catalog = new[]
        {
            new GameEntry("switch", "Same Title", "Nintendo", true, null, "Switch"),
            new GameEntry("pc", "Same Title", "Steam", true, null, "PC"),
        };

        var results = GameSearch.Search(catalog, "same title");

        Assert.Equal(2, results.Count);
        Assert.Equal(new[] { "pc", "switch" }, results.Select(game => game.GameId));
        Assert.All(results, game => Assert.True(game.IsInstalled));
    }

    [Fact]
    public void ReturnedSequenceIsAPointInTimeSnapshot()
    {
        var catalog = new List<GameEntry>
        {
            new("1", "Halo"),
            new("2", "Halo 2"),
        };

        var results = GameSearch.Search(catalog, "halo");
        catalog.Add(new GameEntry("3", "Halo 3"));
        catalog.Clear();

        Assert.Equal(new[] { "1", "2" }, results.Select(game => game.GameId));
        // The returned collection is fixed-size; callers cannot mutate shared state.
        Assert.Throws<NotSupportedException>(() => ((IList<GameEntry>)results).Add(new GameEntry("4", "Halo 4")));
    }

    [Fact]
    public void CatalogEnumerationHappensOncePerSearch()
    {
        var catalog = new SinglePassCatalog(
            new GameEntry("1", "Halo"),
            new GameEntry("2", "Halo 2"),
            new GameEntry("3", "Superhalo"));

        var results = GameSearch.Search(catalog, "halo");

        Assert.Equal(1, catalog.EnumerationCount);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void EmptyCatalogReturnsAnEmptySequenceThatIsSafeToEnumerateTwice()
    {
        var results = GameSearch.Search(Array.Empty<GameEntry>(), "halo");

        Assert.Empty(results);
        Assert.Empty(results);
    }

    [Fact]
    public void NullEntryInTheCatalogIsRejected()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            GameSearch.Search(new[] { new GameEntry("1", "Halo"), null! }, "halo"));

        Assert.Equal("catalog", error.ParamName);
    }

    [Fact]
    public void BlankQueryStillValidatesCatalogEntries()
    {
        // Blank queries return the full catalog in stable order, so catalog validation still applies.
        var catalog = new SinglePassCatalog(new GameEntry("1", "Halo"), null!);

        var error = Assert.Throws<ArgumentException>(() => GameSearch.Search(catalog, " "));

        Assert.Equal("catalog", error.ParamName);
        Assert.Equal(1, catalog.EnumerationCount);
    }

    [Fact]
    public void InvalidEntryIsRejectedEvenWhenItsTitleWouldNotMatch()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            GameSearch.Search(new[] { new GameEntry("1", "Halo"), new GameEntry("", "Other") }, "halo"));

        Assert.Equal("GameId", error.ParamName);
    }

    [Fact]
    public void LargeCatalogRemainsDeterministicAndOrdered()
    {
        var catalog = Enumerable.Range(0, 5_000)
            .Select(index => new GameEntry(
                $"id-{index:D4}",
                index % 3 == 0 ? "Halo " + index : "Unrelated " + index))
            .ToArray();

        var first = GameSearch.Search(catalog, "halo").Select(game => game.GameId).ToArray();
        var second = GameSearch.Search(catalog.Reverse(), "halo").Select(game => game.GameId).ToArray();

        Assert.Equal(first, second);
        Assert.All(first, gameId => Assert.StartsWith("Halo ", FindTitle(catalog, gameId), StringComparison.Ordinal));
    }

    private static string FindTitle(IEnumerable<GameEntry> catalog, string gameId) =>
        catalog.Single(game => string.Equals(game.GameId, gameId, StringComparison.Ordinal)).Title;

    /// <summary>Fails if search enumerates the source more than once.</summary>
    private sealed class SinglePassCatalog(params GameEntry[] entries) : IEnumerable<GameEntry>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<GameEntry> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount > 1)
            {
                throw new InvalidOperationException("The catalog was enumerated more than once.");
            }

            return ((IEnumerable<GameEntry>)entries).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
