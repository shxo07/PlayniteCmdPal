using PlayniteCmdPal.Core;

namespace PlayniteCmdPal.Playnite;

/// <summary>
/// Resolves a single exact-title match for a Playnite global fallback. Only healthy,
/// installed, uniquely titled games can be exposed or launched.
/// </summary>
public sealed class PlayniteDirectLaunch
{
    private readonly IReadOnlyPlayniteCatalog _catalog;

    public PlayniteDirectLaunch(IReadOnlyPlayniteCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Finds the unique installed game whose title exactly matches the query.</summary>
    public Task<GameEntry?> FindMatchAsync(string? query, CancellationToken cancellationToken = default) =>
        ReadMatchAsync(query, forceRefresh: false, cancellationToken);

    /// <summary>Finds an exact installed title, optionally bypassing the adapter's normal cache interval.</summary>
    public Task<GameEntry?> FindMatchAsync(
        string? query,
        bool forceRefresh,
        CancellationToken cancellationToken = default) =>
        ReadMatchAsync(query, forceRefresh, cancellationToken);

    /// <summary>
    /// Re-reads the snapshot before invocation. The same stable ID must still be the
    /// unique installed exact-title match; never substitute another ID or an uninstalled
    /// same-title entry.
    /// </summary>
    public async Task<bool> RevalidateAsync(
        string query,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!PlayniteLaunchUri.TryCreate(gameId, out _)) return false;

        var current = await ReadMatchAsync(query, forceRefresh: true, cancellationToken).ConfigureAwait(false);
        return current is not null && string.Equals(current.GameId, gameId, StringComparison.Ordinal);
    }

    private async Task<GameEntry?> ReadMatchAsync(
        string? query,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query)) return null;

        var catalog = forceRefresh
            ? await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false)
            : await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Last-good/partial data remains browsable in the pages, but cannot authorize
        // a global direct-launch fallback. Unknown/uninstalled entries are excluded by
        // InstalledGameMatch, and any remaining catalog ambiguity fails closed.
        if (!IsHealthy(catalog)) return null;
        return InstalledGameMatch.Find(catalog.Games, query);
    }

    private static bool IsHealthy(CatalogReadResult catalog) =>
        catalog.IsAvailable && !catalog.IsStale && catalog.Issue is null && catalog.SkippedRecordCount == 0;
}
