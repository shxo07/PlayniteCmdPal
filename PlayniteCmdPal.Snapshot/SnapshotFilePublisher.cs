namespace PlayniteCmdPal.Snapshot;

/// <summary>Atomically publishes a complete snapshot. A failed publication never deletes the previous file.</summary>
public static class SnapshotFilePublisher
{
    public static async Task PublishAsync(string path, CatalogSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A snapshot path is required.", nameof(path));
        }

        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The snapshot path needs a directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await CatalogSnapshotSerializer.SerializeAsync(stream, snapshot, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
                if (stream.Length > SnapshotLimits.MaxBytes)
                {
                    throw new InvalidDataException("The serialized snapshot exceeds the 32 MiB limit.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, destinationBackupFileName: null);
            }
            else
            {
                try
                {
                    File.Move(temporaryPath, fullPath);
                }
                catch (IOException) when (File.Exists(fullPath))
                {
                    // Another writer won the first-creation race. Its complete file is now
                    // the destination, so replace it atomically rather than failing the export.
                    File.Replace(temporaryPath, fullPath, destinationBackupFileName: null);
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
                // Publication already failed or succeeded; never mask that outcome while cleaning a temp file.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
