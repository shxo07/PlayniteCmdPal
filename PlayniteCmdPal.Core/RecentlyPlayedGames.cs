namespace PlayniteCmdPal.Core;

/// <summary>Deterministic newest-first ordering for games with recorded play history.</summary>
public static class RecentlyPlayedGames
{
    /// <summary>
    /// Returns only games with a LastActivity value. UTC and Local values are compared as
    /// instants. Unspecified values follow DateTime.ToUniversalTime's documented policy and
    /// are interpreted as local machine time; this avoids silently treating an unspecified
    /// wall-clock value as UTC.
    /// </summary>
    public static IReadOnlyList<GameEntry> Sort(IEnumerable<GameEntry> games)
    {
        if (games is null) throw new ArgumentNullException(nameof(games));

        return games
            .Where(game => game.LastActivity.HasValue)
            .OrderByDescending(game => game.LastActivity!.Value.ToUniversalTime().Ticks)
            .ThenBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.Title, StringComparer.Ordinal)
            .ThenBy(game => game.GameId, StringComparer.Ordinal)
            .ToArray();
    }
}
