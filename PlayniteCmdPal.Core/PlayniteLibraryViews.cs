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
/// Shared metadata filters are applied consistently after each view's own selection.
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
            PlayniteLibraryViewMode.Favorites => SearchFavorites(catalog, query, favoriteGameIds, filters),
            PlayniteLibraryViewMode.RecentlyPlayed => SearchRecentlyPlayed(catalog, query, filters),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static IReadOnlyList<GameEntry> SearchFavorites(
        IEnumerable<GameEntry> games,
        string? query,
        IReadOnlyCollection<string>? favoriteGameIds,
        GameSearchFilters? filters)
    {
        if (favoriteGameIds is null || favoriteGameIds.Count == 0)
            return Array.Empty<GameEntry>();

        var ids = favoriteGameIds.ToHashSet(StringComparer.Ordinal);
        return GameSearch.Search(games.Where(game => ids.Contains(game.GameId)), query, filters);
    }

    private static IReadOnlyList<GameEntry> SearchRecentlyPlayed(
        IEnumerable<GameEntry> games,
        string? query,
        GameSearchFilters? filters)
    {
        var ordered = RecentlyPlayedGames.Sort(games);
        var normalizedQuery = query?.Trim();
        return ordered
            .Where(game => (filters is null || filters.Matches(game)) &&
                (string.IsNullOrEmpty(normalizedQuery) ||
                 game.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
