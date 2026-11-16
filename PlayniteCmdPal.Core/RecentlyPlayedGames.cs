namespace PlayniteCmdPal.Core;

public enum RecentlyPlayedEmptyState
{
    EmptyLibrary,
    NoUsableRecords,
    NoPlayHistory,
}

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

    /// <summary>Distinguishes an empty library from invalid records and games without play history.</summary>
    public static RecentlyPlayedEmptyState GetEmptyState(int catalogGameCount, int skippedRecordCount)
    {
        if (catalogGameCount < 0) throw new ArgumentOutOfRangeException(nameof(catalogGameCount));
        if (skippedRecordCount < 0) throw new ArgumentOutOfRangeException(nameof(skippedRecordCount));
        if (catalogGameCount == 0 && skippedRecordCount > 0) return RecentlyPlayedEmptyState.NoUsableRecords;
        return catalogGameCount == 0
            ? RecentlyPlayedEmptyState.EmptyLibrary
            : RecentlyPlayedEmptyState.NoPlayHistory;
    }

    /// <summary>Formats a last-played value as a concise relative age using the supplied clock.</summary>
    public static string FormatRelativeLastPlayed(DateTime lastActivity, DateTimeOffset now)
    {
        var elapsed = now.UtcDateTime - lastActivity.ToUniversalTime();
        var isFuture = elapsed < TimeSpan.Zero;
        var age = elapsed.Duration();

        if (age < TimeSpan.FromMinutes(1))
            return isFuture ? "in a moment" : "just now";

        var (count, unit) = age switch
        {
            _ when age < TimeSpan.FromHours(1) => ((int)age.TotalMinutes, "minute"),
            _ when age < TimeSpan.FromDays(1) => ((int)age.TotalHours, "hour"),
            _ when age < TimeSpan.FromDays(7) => ((int)age.TotalDays, "day"),
            _ when age < TimeSpan.FromDays(30) => ((int)(age.TotalDays / 7), "week"),
            _ when age < TimeSpan.FromDays(365) => ((int)(age.TotalDays / 30), "month"),
            _ => ((int)(age.TotalDays / 365), "year"),
        };

        var phrase = $"{count} {unit}{(count == 1 ? string.Empty : "s")}";
        return isFuture ? $"in {phrase}" : $"{phrase} ago";
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
        var query = searchText?.Trim();
        visible.AddRange(string.IsNullOrEmpty(query)
            ? gameItems
            : gameItems.Where(item => getTitle(item).Contains(query, StringComparison.OrdinalIgnoreCase)));
        return visible.AsReadOnly();
    }
}
