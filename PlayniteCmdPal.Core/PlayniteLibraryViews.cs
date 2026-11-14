namespace PlayniteCmdPal.Core;

/// <summary>The in-page discovery modes exposed by the unified Playnite command.</summary>
public enum PlayniteLibraryViewMode
{
    Search,
    RecentlyPlayed,
    Favorites,
}

/// <summary>
/// Selects catalog entries for the unified Playnite discovery views while keeping
/// ranking, favorites identity, and recent-activity ordering in the core layer.
/// </summary>
public static class PlayniteLibraryViews
{
    public static IReadOnlyList<GameEntry> Search(
        IEnumerable<GameEntry> games,
        PlayniteLibraryViewMode mode,
        string? query,
        IReadOnlyCollection<string>? favoriteGameIds = null,
        GameSearchFilters? filters = null)
    {
        ArgumentNullException.ThrowIfNull(games);
        var catalog = games.ToArray();

        return mode switch
        {
            PlayniteLibraryViewMode.Search => GameSearch.Search(catalog, query, filters),
            PlayniteLibraryViewMode.Favorites => SearchFavorites(catalog, query, favoriteGameIds),
            PlayniteLibraryViewMode.RecentlyPlayed => SearchRecentlyPlayed(catalog, query),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static IReadOnlyList<GameEntry> SearchFavorites(
        IEnumerable<GameEntry> games,
        string? query,
        IReadOnlyCollection<string>? favoriteGameIds)
    {
        if (favoriteGameIds is null || favoriteGameIds.Count == 0)
            return Array.Empty<GameEntry>();

        var ids = favoriteGameIds.ToHashSet(StringComparer.Ordinal);
        return GameSearch.Search(games.Where(game => ids.Contains(game.GameId)), query);
    }

    private static IReadOnlyList<GameEntry> SearchRecentlyPlayed(IEnumerable<GameEntry> games, string? query)
    {
        var ordered = RecentlyPlayedGames.Sort(games);
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrEmpty(normalizedQuery))
            return ordered;

        // Preserve LastActivity ordering; search narrows the ordered sequence rather than
        // applying title relevance ranking that would change the recent-first contract.
        return ordered
            .Where(game => game.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
