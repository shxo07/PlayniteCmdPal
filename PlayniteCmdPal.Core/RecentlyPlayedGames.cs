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

    /// <summary>
    /// Formats a last-played value in one display time zone. Unspecified values retain the
    /// documented local-machine interpretation; the numeric offset makes the displayed zone
    /// unambiguous even across daylight-saving transitions.
    /// </summary>
    public static string FormatLastPlayed(DateTime lastActivity, TimeZoneInfo displayTimeZone)
    {
        if (displayTimeZone is null) throw new ArgumentNullException(nameof(displayTimeZone));

        var utc = lastActivity.ToUniversalTime();
        var instant = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        return TimeZoneInfo.ConvertTime(instant, displayTimeZone)
            .ToString("yyyy-MM-dd HH:mm zzz", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Keeps status and recovery items visible while applying search only to game entries.
    /// </summary>
    public static IReadOnlyList<T> ComposeVisibleItems<T>(
        IEnumerable<T> statusItems,
        IEnumerable<T> gameItems,
        string? searchText,
        Func<T, string> getTitle)
    {
        if (statusItems is null) throw new ArgumentNullException(nameof(statusItems));
        if (gameItems is null) throw new ArgumentNullException(nameof(gameItems));
        if (getTitle is null) throw new ArgumentNullException(nameof(getTitle));

        var visible = new List<T>();
        visible.AddRange(statusItems);
        visible.AddRange(string.IsNullOrWhiteSpace(searchText)
            ? gameItems
            : gameItems.Where(item => getTitle(item).Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        return visible.AsReadOnly();
    }
}
