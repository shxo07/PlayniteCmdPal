using PlayniteCmdPal.Playnite;

// This verifier only reads the adapter's default snapshot. It intentionally
// reports aggregate status and never prints game names, IDs, or file contents.
try
{
    var adapter = new PlayniteCatalogAdapter();
    var initial = await adapter.GetAsync();
    Console.WriteLine($"Available: {initial.IsAvailable}");
    Console.WriteLine($"Game count: {initial.Games.Count}");
    Console.WriteLine($"Skipped records: {initial.SkippedRecordCount}");
    Console.WriteLine($"Stale: {initial.IsStale}");
    Console.WriteLine($"Issue: {initial.Issue?.Code.ToString() ?? "None"}");
    if (!initial.IsAvailable || initial.Issue is not null)
    {
        return 1;
    }

    var ids = new HashSet<string>(StringComparer.Ordinal);
    var validIds = initial.Games.All(game =>
        Guid.TryParseExact(game.GameId, "D", out var id) && id != Guid.Empty &&
        string.Equals(game.GameId, id.ToString("D"), StringComparison.Ordinal) && ids.Add(game.GameId));
    var mappedFields = initial.Games.All(game =>
        !string.IsNullOrWhiteSpace(game.Title) && game.IsInstalled.HasValue && game.IconReference is null);

    var cached = await adapter.GetAsync();
    var refreshed = await adapter.RefreshAsync();
    var sameIds = refreshed.IsAvailable && ids.SetEquals(refreshed.Games.Select(game => game.GameId));
    Console.WriteLine($"Canonical unique IDs: {validIds}");
    Console.WriteLine($"Required fields mapped: {mappedFields}");
    Console.WriteLine($"Second read used cache: {cached.IsFromCache}");
    Console.WriteLine($"Refresh available: {refreshed.IsAvailable}");
    Console.WriteLine($"Refresh read from file: {refreshed.IsAvailable && refreshed.Issue is null && !refreshed.IsFromCache}");
    Console.WriteLine($"Refresh stale: {refreshed.IsStale}");
    Console.WriteLine($"Refresh skipped records: {refreshed.SkippedRecordCount}");
    Console.WriteLine($"Refresh issue: {refreshed.Issue?.Code.ToString() ?? "None"}");
    Console.WriteLine($"Same IDs after refresh: {sameIds}");

    return initial.IsStale || initial.SkippedRecordCount != 0 || !validIds || !mappedFields ||
           !refreshed.IsAvailable || refreshed.Issue is not null || refreshed.IsStale ||
           refreshed.SkippedRecordCount != 0 ? 1 : 0;
}
catch (Exception exception)
{
    // Exception messages and paths may contain private information.
    Console.Error.WriteLine($"Verification failed: {exception.GetType().Name}");
    return 1;
}
