using PlayniteCmdPal.Core;

namespace PlayniteCmdPal.Playnite;

/// <summary>
/// Stable-ID commands over the supported snapshot transport. A direct-launch row
/// must not silently use last-known-good data after a failed read or a partial catalog.
/// </summary>
public sealed class PlayniteDirectLaunch
{
    private readonly IReadOnlyPlayniteCatalog _catalog;

    public PlayniteDirectLaunch(IReadOnlyPlayniteCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Eligible root commands; the host owns query filtering and ranking.</summary>
    public async Task<IReadOnlyList<GameEntry>> GetCandidatesAsync(
        bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var catalog = await ReadCatalogAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
        return IsHealthy(catalog)
            ? DirectLaunchGames.GetCanonicalGames(catalog.Games)
            : Array.Empty<GameEntry>();
    }

    /// <summary>
    /// Re-reads the snapshot and verifies the explicitly selected ID and displayed
    /// identity/metadata. Equal titles under other IDs do not make an explicit choice
    /// ambiguous. Never substitute a different ID, even if it now has the same title.
    /// </summary>
    public async Task<bool> RevalidateAsync(GameEntry displayedGame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(displayedGame);
        cancellationToken.ThrowIfCancellationRequested();
        if (!PlayniteLaunchUri.TryCreate(displayedGame.GameId, out _)) return false;

        var catalog = await ReadCatalogAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
        var current = IsHealthy(catalog) ? DirectLaunchGames.FindById(catalog.Games, displayedGame.GameId) : null;
        return current is not null &&
            string.Equals(current.Title, displayedGame.Title, StringComparison.Ordinal) &&
            string.Equals(current.Source, displayedGame.Source, StringComparison.Ordinal) &&
            string.Equals(current.Platform, displayedGame.Platform, StringComparison.Ordinal) &&
            current.IsInstalled == displayedGame.IsInstalled;
    }

    private async Task<CatalogReadResult> ReadCatalogAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var catalog = forceRefresh
            ? await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false)
            : await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return catalog;
    }

    // Last-good/partial data remains browsable on the page, but cannot authorize
    // direct root commands after a failed refresh or a conflicting/invalid record.
    private static bool IsHealthy(CatalogReadResult catalog) =>
        catalog.IsAvailable && !catalog.IsStale && catalog.Issue is null && catalog.SkippedRecordCount == 0;
}
