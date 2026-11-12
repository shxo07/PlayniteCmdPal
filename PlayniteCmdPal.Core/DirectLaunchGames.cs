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
