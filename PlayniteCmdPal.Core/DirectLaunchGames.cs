namespace PlayniteCmdPal.Core;

/// <summary>
/// Canonical, stable-ID game identity for direct commands, independent of the host's
/// query matching. Equal titles are distinct choices, never a reason to pick one ID.
/// </summary>
public static class DirectLaunchGames
{
    /// <summary>
    /// Builds the complete command catalog in one scan plus deterministic sorting.
    /// Identical repeated records count once; invalid or conflicting records fail closed.
    /// Callers must check catalog health/freshness and disambiguate equal display titles.
    /// </summary>
    public static IReadOnlyList<GameEntry> GetCanonicalGames(IEnumerable<GameEntry> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var entries = ReadCanonicalEntries(catalog);
        if (entries is null) return Array.Empty<GameEntry>();

        var games = entries.Values
            .OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.Title, StringComparer.Ordinal)
            .ThenBy(game => game.GameId, StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(games);
    }

    /// <summary>
    /// Finds a unique installed game by exact title for the global fallback. Installation
    /// state must be true; uninstalled/unknown entries do not appear or create ambiguity.
    /// The entire catalog is validated first so an invalid or conflicting ID cannot hide a
    /// duplicate and make an otherwise unsafe title look unique. Query matching is trimmed
    /// and case-insensitive but stored titles are not rewritten.
    /// </summary>
    public static GameEntry? FindInstalledExactTitle(IEnumerable<GameEntry> catalog, string? query)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(query)) return null;

        var entries = ReadCanonicalEntries(catalog);
        if (entries is null) return null;

        var title = query.Trim();
        GameEntry? match = null;
        foreach (var game in entries.Values)
        {
            if (game.IsInstalled is not true ||
                !string.Equals(game.Title, title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match is not null) return null;
            match = game;
        }

        return match;
    }

    /// <summary>
    /// Resolves only the explicitly selected canonical ID, not a title/query. Validates
    /// the entire input so a conflicting duplicate ID cannot silently retarget a row.
    /// </summary>
    public static GameEntry? FindById(IEnumerable<GameEntry> catalog, string? gameId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!PlayniteLaunchUri.TryCreate(gameId, out _)) return null;

        var entries = ReadCanonicalEntries(catalog);
        return entries is not null && entries.TryGetValue(gameId!, out var game) ? game : null;
    }

    private static Dictionary<string, GameEntry>? ReadCanonicalEntries(IEnumerable<GameEntry> catalog)
    {
        var entries = new Dictionary<string, GameEntry>(StringComparer.Ordinal);
        foreach (var game in catalog)
        {
            // Use precisely the same canonical-ID policy as the existing launch path.
            if (game is null || string.IsNullOrWhiteSpace(game.Title) ||
                !PlayniteLaunchUri.TryCreate(game.GameId, out _))
            {
                return null;
            }

            if (entries.TryGetValue(game.GameId, out var previous))
            {
                if (previous != game) return null;
            }
            else
            {
                entries.Add(game.GameId, game);
            }
        }

        return entries;
    }
}
