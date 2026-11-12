namespace PlayniteCmdPal.Core;

/// <summary>
/// Selects at most one safe direct-launch candidate. Unlike page search, this never
/// resolves ambiguity by relevance, installation state, source, or catalog order.
/// </summary>
public static class ExactGameMatch
{
    /// <summary>
    /// Trims only the query and compares the complete title using ordinal case-insensitive
    /// equality. Identical repeated records count once; conflicting records for one ID,
    /// invalid records, and equal titles belonging to different IDs fail closed.
    /// Catalog freshness and read errors must be checked by the caller.
    /// </summary>
    public static GameEntry? Find(IEnumerable<GameEntry> catalog, string? query)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var title = query.Trim();
        var entriesById = new Dictionary<string, GameEntry>(StringComparer.Ordinal);
        GameEntry? match = null;
        foreach (var game in catalog)
        {
            // Use precisely the same canonical-ID policy as the existing launch path.
            if (game is null || string.IsNullOrWhiteSpace(game.Title) ||
                !PlayniteLaunchUri.TryCreate(game.GameId, out _))
            {
                return null;
            }

            if (entriesById.TryGetValue(game.GameId, out var previous))
            {
                if (previous != game)
                {
                    return null;
                }

                continue;
            }

            entriesById.Add(game.GameId, game);
            if (!string.Equals(game.Title, title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = game;
        }

        return match;
    }
}
