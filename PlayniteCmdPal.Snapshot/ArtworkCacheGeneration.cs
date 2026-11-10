using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace PlayniteCmdPal.Snapshot;

/// <summary>
/// Builds a bounded, content-addressed artwork generation. Only canonical Playnite database
/// references for the owning game are resolved; arbitrary paths never reach the SDK resolver.
/// </summary>
public sealed class ArtworkCacheGeneration : IDisposable
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
    private readonly string _root;
    private readonly string _staging;
    private readonly string _marker;
    private readonly string? _snapshotPath;
    private string _publicationToken = string.Empty;
    private string? _backup;
    private long _bytes;
    private readonly long _maxCacheBytes;
    private readonly int _maxArtworkBytes;
    private bool _hadPreviousRoot;
    private bool _activated;
    private bool _published;
    private bool _completed;
    private bool _cleanupFailure;

    private readonly Action<string, string> _moveFile;
    private readonly Action<string> _tryDeleteFile;

    public ArtworkCacheGeneration(string root, long maxCacheBytes = SnapshotLimits.MaxArtworkCacheBytes, int maxArtworkBytes = SnapshotLimits.MaxArtworkBytes, string? snapshotPath = null)
        : this(root, maxCacheBytes, maxArtworkBytes, snapshotPath, File.Move, TryDeleteFile)
    {
    }

    internal ArtworkCacheGeneration(string root, long maxCacheBytes, int maxArtworkBytes, string? snapshotPath,
        Action<string, string> moveFile, Action<string> tryDeleteFile)
    {
        _moveFile = moveFile ?? throw new ArgumentNullException(nameof(moveFile));
        _tryDeleteFile = tryDeleteFile ?? throw new ArgumentNullException(nameof(tryDeleteFile));
        if (maxCacheBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxCacheBytes));
        if (maxArtworkBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxArtworkBytes));
        _maxCacheBytes = maxCacheBytes;
        _maxArtworkBytes = maxArtworkBytes;
        _snapshotPath = snapshotPath;
        _root = Path.GetFullPath(root);
        var parent = Path.GetDirectoryName(_root) ?? throw new ArgumentException("Cache root needs a parent directory.", nameof(root));
        Directory.CreateDirectory(parent);
        _marker = _root + ".transaction";
        RecoverInterruptedGeneration(_root, _marker, _snapshotPath);
        _staging = _root + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(_staging);
    }

    public string? TryImport(string gameId, string? databaseReference, string? resolvedPath, string databaseFilesRoot)
    {
        // Once cleanup has failed, the staging directory may contain unaccounted bytes.
        // Fail closed immediately; no later import may add more data to this generation.
        if (_cleanupFailure) return null;
        if (!TryValidateReference(gameId, databaseReference, out var fileName) || string.IsNullOrWhiteSpace(resolvedPath))
            return null;
        try
        {
            var filesRoot = Path.GetFullPath(databaseFilesRoot);
            var expected = Path.GetFullPath(Path.Combine(filesRoot, gameId, fileName));
            var actual = Path.GetFullPath(resolvedPath);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) return null;

            var gameDirectory = new DirectoryInfo(Path.Combine(filesRoot, gameId));
            var source = new FileInfo(actual);
            var filesDirectory = new DirectoryInfo(filesRoot);
            if (!filesDirectory.Exists || (filesDirectory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                !gameDirectory.Exists || (gameDirectory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                !source.Exists || (source.Attributes & FileAttributes.ReparsePoint) != 0 ||
                source.Length <= 0 || source.Length > _maxArtworkBytes) return null;

            byte[] bytes;
            using (var input = new FileStream(actual, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new MemoryStream())
            {
                if ((File.GetAttributes(actual) & FileAttributes.ReparsePoint) != 0) return null;
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var read = input.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    if (output.Length + read > _maxArtworkBytes) return null;
                    output.Write(buffer, 0, read);
                }
                bytes = output.ToArray();
            }

            if (!TryDetectFormat(bytes, out var extension)) return null;
            string digest;
            using (var sha = SHA256.Create())
                digest = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            var destination = Path.Combine(_staging, digest + extension);
            if (!Extensions.Any(ext => File.Exists(Path.Combine(_staging, digest + ext))))
            {
                if (_bytes + bytes.Length > _maxCacheBytes) return null;
                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        output.Write(bytes, 0, bytes.Length);
                        output.Flush();
                    }
                    _moveFile(temporary, destination);
                    _bytes += bytes.Length;
                }
                catch
                {
                    var cleanupVerified = false;
                    try
                    {
                        _tryDeleteFile(temporary);
                        _tryDeleteFile(destination);
                        cleanupVerified = !PathExistsOrThrow(temporary) && !PathExistsOrThrow(destination);
                    }
                    catch
                    {
                        // An inaccessible path is not evidence that cleanup succeeded.
                        cleanupVerified = false;
                    }

                    if (!cleanupVerified) _cleanupFailure = true;
                    throw;
                }
            }
            return digest;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                   ex is ArgumentException || ex is NotSupportedException ||
                                   ex is System.Security.SecurityException)
        {
            return null;
        }
    }

    public static bool TryValidateReference(string gameId, string? reference, out string fileName) =>
        ArtworkReferenceValidator.TryValidate(gameId, reference, out fileName);

    public void Activate(string? publicationToken = null)
    {
        if (_cleanupFailure)
            throw new IOException("A failed artwork import left temporary data that could not be cleaned; the generation cannot be published.");
        if (_activated) throw new InvalidOperationException("Artwork generation is already active.");
        _backup = _root + ".backup-" + Guid.NewGuid().ToString("N");
        var rootAttributes = GetPathAttributes(_root);
        if (rootAttributes.HasValue && (rootAttributes.Value & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The artwork cache root cannot be a reparse point.");
        _hadPreviousRoot = rootAttributes.HasValue;
        _publicationToken = publicationToken ?? string.Empty;
        if (_publicationToken.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            throw new ArgumentException("The publication token cannot contain line breaks.", nameof(publicationToken));
        WriteMarker("pending");
        try
        {
            if (_hadPreviousRoot) Directory.Move(_root, _backup);
            Directory.Move(_staging, _root);
            _activated = true;
        }
        catch
        {
            if (_hadPreviousRoot && PathExistsOrThrow(_backup) && !PathExistsOrThrow(_root))
            {
                // If restoration fails, keep the marker so startup recovery can retry it.
                Directory.Move(_backup, _root);
            }

            if (PathExistsOrThrow(_root) || !_hadPreviousRoot) TryDeleteFile(_marker);
            throw;
        }
    }

    public void MarkPublished()
    {
        if (!_activated) throw new InvalidOperationException("Artwork generation has not been activated.");
        WriteMarker("published");
        _published = true;
    }

    public void PreserveForRecovery()
    {
        // Leave the transaction marker and backup intact. Startup compares the v2 snapshot
        // timestamp token to decide whether the atomic snapshot replace already committed.
        _completed = true;
        _activated = false;
    }

    public void ClearActiveCache()
    {
        if (!_activated || !PathExistsOrThrow(_root)) return;
        foreach (var file in Directory.GetFiles(_root)) DeleteFileOrThrow(file);
    }

    public void Complete()
    {
        if (!_published) throw new InvalidOperationException("Artwork generation cannot complete before snapshot publication.");
        try
        {
            DeleteDirectoryOrThrow(_backup);
            DeleteFileOrThrow(_marker);
            _completed = true;
        }
        catch
        {
            // Keep the published marker and transaction state so startup recovery can retry.
            // Do not let Dispose roll back a snapshot that has already been published.
            _completed = true;
            _activated = false;
            throw;
        }
    }

    public void Rollback()
    {
        if (_activated)
        {
            DeleteDirectoryOrThrow(_root);
            if (!string.IsNullOrEmpty(_backup) && PathExistsOrThrow(_backup))
                Directory.Move(_backup, _root);

            // Clear activation as soon as restoration is complete. If marker deletion
            // fails, Dispose must not retry by deleting the restored previous generation.
            _activated = false;
            DeleteFileOrThrow(_marker);
            _completed = true;
            return;
        }

        DeleteDirectoryOrThrow(_staging);
    }

    public void Dispose()
    {
        if (!_completed) Rollback();
    }

    private static void RecoverInterruptedGeneration(string root, string marker, string? snapshotPath)
    {
        var parent = Path.GetDirectoryName(root)!;
        var prefix = Path.GetFileName(root) + ".staging-";
        foreach (var stagingPath in Directory.GetDirectories(parent, prefix + "*"))
            DeleteDirectoryOrThrow(stagingPath);
        foreach (var markerTemporary in Directory.GetFiles(parent, Path.GetFileName(marker) + ".*.tmp"))
            DeleteFileOrThrow(markerTemporary);
        if (!PathExistsOrThrow(marker))
        {
            var orphanedBackups = Directory.GetDirectories(parent, Path.GetFileName(root) + ".backup-*");
            if (!PathExistsOrThrow(root) && orphanedBackups.Length > 0)
            {
                var recoveryBackup = orphanedBackups.OrderByDescending(Directory.GetCreationTimeUtc).First();
                Directory.Move(recoveryBackup, root);
            }

            foreach (var orphanedBackup in Directory.GetDirectories(parent, Path.GetFileName(root) + ".backup-*"))
                DeleteDirectoryOrThrow(orphanedBackup);
            return;
        }
        try
        {
            var lines = File.ReadAllText(marker, Encoding.UTF8).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (lines.Length < 4) throw new InvalidDataException("The artwork cache transaction marker is incomplete.");
            var token = lines[3];
            var publicationState = lines[0] == "published"
                ? SnapshotPublicationState.Published
                : GetSnapshotPublicationState(snapshotPath, token);
            var published = publicationState == SnapshotPublicationState.Published;
            var backup = lines[1];
            var hadPrevious = lines[2] == "1";
            if (!IsValidBackupPath(root, backup))
                throw new InvalidDataException("The artwork cache transaction marker contains an invalid backup path.");
            if (published) DeleteDirectoryOrThrow(backup);
            else if (PathExistsOrThrow(backup))
            {
                var backupAttributes = GetPathAttributes(backup);
                if (backupAttributes.HasValue && (backupAttributes.Value & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("The artwork cache backup cannot be a reparse point.");
                DeleteDirectoryOrThrow(root);
                if (PathExistsOrThrow(root)) throw new IOException("The interrupted artwork cache could not be replaced during recovery.");
                Directory.Move(backup, root);
            }
            else if (hadPrevious && !PathExistsOrThrow(root))
            {
                // Both the previous generation's backup and the active cache are missing.
                // Do not discard the marker: the published catalog may still reference
                // artwork from the lost generation, and recovery cannot safely complete.
                throw new IOException("The previous artwork cache and its recovery backup are both missing.");
            }
            else if (!hadPrevious)
            {
                DeleteDirectoryOrThrow(root);
                if (PathExistsOrThrow(root)) throw new IOException("The incomplete artwork cache could not be removed during recovery.");
            }
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            // Do not start a new generation while the previous transaction is unresolved.
            throw new IOException("The previous artwork cache transaction could not be recovered; the existing cache was left untouched.", ex);
        }
        DeleteFileOrThrow(marker);
        foreach (var backupPath in Directory.GetDirectories(parent, Path.GetFileName(root) + ".backup-*"))
            DeleteDirectoryOrThrow(backupPath);
    }

    private static bool IsValidBackupPath(string root, string backup)
    {
        var prefix = root + ".backup-";
        if (!backup.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = backup.Substring(prefix.Length);
        if (!Guid.TryParseExact(suffix, "N", out var id) ||
            !string.Equals(suffix, id.ToString("N"), StringComparison.Ordinal))
        {
            return false;
        }

        var expected = Path.GetFullPath(prefix + suffix);
        var actual = Path.GetFullPath(backup);
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetDirectoryName(actual), Path.GetDirectoryName(root), StringComparison.OrdinalIgnoreCase);
    }

    private void WriteMarker(string state)
    {
        var temporary = _marker + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var content = state + Environment.NewLine + (_backup ?? string.Empty) + Environment.NewLine +
                (_hadPreviousRoot ? "1" : "0") + Environment.NewLine + _publicationToken;
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(_marker)) File.Replace(temporary, _marker, null);
            else File.Move(temporary, _marker);
        }
        finally
        {
            TryDeleteFile(temporary);
        }
    }

    private enum SnapshotPublicationState
    {
        NotPublished,
        Published,
    }

    private static SnapshotPublicationState GetSnapshotPublicationState(string? snapshotPath, string token)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath) || string.IsNullOrEmpty(token))
            return SnapshotPublicationState.NotPublished;

        try
        {
            using var stream = new FileStream(
                snapshotPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > SnapshotLimits.MaxBytes)
                return SnapshotPublicationState.NotPublished;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var json = reader.ReadToEnd();
            return json.StartsWith(
                "{\"schemaVersion\":2,\"generatedAtUtc\":\"" + token + "\",\"games\":",
                StringComparison.Ordinal)
                ? SnapshotPublicationState.Published
                : SnapshotPublicationState.NotPublished;
        }
        catch (FileNotFoundException)
        {
            return SnapshotPublicationState.NotPublished;
        }
        catch (DirectoryNotFoundException)
        {
            return SnapshotPublicationState.NotPublished;
        }
        // Sharing, access, and other IO failures intentionally propagate. Recovery must
        // retain both generations and the marker when publication evidence is unreadable.
    }

    private static bool TryDetectFormat(byte[] data, out string extension) =>
        ArtworkFileSignature.TryGetExtension(data, out extension);

    private static void DeleteDirectoryOrThrow(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var attributes = GetPathAttributes(path);
        if (!attributes.HasValue) return;

        // Never recurse through a directory reparse point.
        Directory.Delete(path, recursive: (attributes.Value & FileAttributes.ReparsePoint) == 0);
        if (PathExistsOrThrow(path))
            throw new IOException("The artwork directory could not be removed: " + path);
    }

    private static void DeleteFileOrThrow(string path)
    {
        if (!PathExistsOrThrow(path)) return;
        File.Delete(path);
        if (PathExistsOrThrow(path))
            throw new IOException("The artwork file could not be removed: " + path);
    }

    private static bool PathExistsOrThrow(string path) => GetPathAttributes(path).HasValue;

    private static FileAttributes? GetPathAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }


    private static void TryDeleteFile(string path)
    {
        try { DeleteFileOrThrow(path); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
    }
}