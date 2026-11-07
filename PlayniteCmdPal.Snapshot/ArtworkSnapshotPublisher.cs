namespace PlayniteCmdPal.Snapshot;

/// <summary>
/// Publishes a catalog and its matching artwork generation. If the artwork-bearing
/// snapshot exceeds the transport limit, publishes a metadata-only v2 snapshot and
/// clears the now-unreferenced active artwork cache.
/// </summary>
public static class ArtworkSnapshotPublisher
{
    public static Task PublishAsync(
        string snapshotPath,
        CatalogSnapshot snapshot,
        ArtworkCacheGeneration artwork,
        CancellationToken cancellationToken = default) =>
        PublishAsync(snapshotPath, snapshot, artwork, SnapshotFilePublisher.PublishAsync, cancellationToken);

    internal static async Task PublishAsync(
        string snapshotPath,
        CatalogSnapshot snapshot,
        ArtworkCacheGeneration artwork,
        Func<string, CatalogSnapshot, CancellationToken, Task> publishAsync,
        CancellationToken cancellationToken = default)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (artwork is null) throw new ArgumentNullException(nameof(artwork));
        if (publishAsync is null) throw new ArgumentNullException(nameof(publishAsync));

        var snapshotPublished = false;
        try
        {
            try
            {
                await publishAsync(snapshotPath, snapshot, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                var withoutArtwork = new CatalogSnapshot(
                    snapshot.GeneratedAtUtc,
                    snapshot.Games.Select(game => new SnapshotGame(
                        game.Id, game.Name, game.IsInstalled, game.Source, game.Platform)).ToArray(),
                    SnapshotLimits.SchemaVersion);
                await publishAsync(snapshotPath, withoutArtwork, cancellationToken).ConfigureAwait(false);
                snapshotPublished = true;
                artwork.ClearActiveCache();
            }

            snapshotPublished = true;
            artwork.MarkPublished();
            artwork.Complete();
        }
        catch
        {
            if (snapshotPublished) artwork.PreserveForRecovery();
            else artwork.Rollback();
            throw;
        }
    }
}
