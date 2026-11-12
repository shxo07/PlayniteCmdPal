using PlayniteCmdPal.Core;

namespace PlayniteCmdPal.Playnite;

/// <summary>
/// Conservative matching over the supported snapshot transport. A direct-launch row
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

    public Task<GameEntry?> FindMatchAsync(string? query, CancellationToken cancellationToken = default) =>
        ReadMatchAsync(query, forceRefresh: false, cancellationToken);

    /// <summary>
    /// Re-reads the snapshot before invocation. Never retarget a previously displayed
    /// row to a different ID, even if that ID now has the same title.
    /// </summary>
    public async Task<bool> RevalidateAsync(string query, string gameId, CancellationToken cancellationToken = default)
    {
        var current = await ReadMatchAsync(query, forceRefresh: true, cancellationToken).ConfigureAwait(false);
        return current is not null && string.Equals(current.GameId, gameId, StringComparison.Ordinal);
    }

    private async Task<GameEntry?> ReadMatchAsync(string? query, bool forceRefresh, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var catalog = forceRefresh
            ? await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false)
            : await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // A skipped record could conceal another entry with this title. The full page
        // can explain these conditions; the single global launch row cannot.
        if (!catalog.IsAvailable || catalog.IsStale || catalog.Issue is not null || catalog.SkippedRecordCount != 0)
        {
            return null;
        }

        return ExactGameMatch.Find(catalog.Games, query);
    }
}
