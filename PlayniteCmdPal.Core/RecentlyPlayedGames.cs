namespace PlayniteCmdPal.Core;

/// <summary>Deterministic newest-first ordering for games with recorded play history.</summary>
public static class RecentlyPlayedGames
{
    /// <summary>
    /// Returns only games with a LastActivity value. Playnite exposes DateTime? without a
    /// documented universal timezone contract, so preserve the original ticks and kind.
    /// </summary>
    public static IReadOnlyList<GameEntry> Sort(IEnumerable<GameEntry> games)
    {
        if (games is null) throw new ArgumentNullException(nameof(games));

        return games
            .Where(game => game.LastActivity.HasValue)
            .OrderByDescending(game => game.LastActivity!.Value.Ticks)
            .ThenBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.Title, StringComparer.Ordinal)
            .ThenBy(game => game.GameId, StringComparer.Ordinal)
            .ToArray();
    }
}
