using System.Security.Cryptography;
using PlayniteCmdPal.Core;
using PlayniteCmdPal.Snapshot;

namespace PlayniteCmdPal.Playnite;

public sealed class CatalogReadResult
{
    internal CatalogReadResult(
        IReadOnlyList<GameEntry> games,
        DateTimeOffset? generatedAtUtc,
        bool isStale,
        CatalogIssue? issue,
        int skippedRecordCount,
        bool isFromCache)
    {
        Games = games;
        GeneratedAtUtc = generatedAtUtc;
        IsStale = isStale;
        Issue = issue;
        SkippedRecordCount = skippedRecordCount;
        IsFromCache = isFromCache;
    }

    public bool IsAvailable => GeneratedAtUtc.HasValue;
    public IReadOnlyList<GameEntry> Games { get; }
    public DateTimeOffset? GeneratedAtUtc { get; }
    public bool IsStale { get; }
    public CatalogIssue? Issue { get; }
    public int SkippedRecordCount { get; }
    public bool IsFromCache { get; }

    public static CatalogReadResult Unavailable(CatalogIssue issue) =>
        new(Array.Empty<GameEntry>(), null, false, issue, 0, false);
}

public interface IReadOnlyPlayniteCatalog
{
    Task<CatalogReadResult> GetAsync(CancellationToken cancellationToken = default);
    Task<CatalogReadResult> RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only adapter that prefers v2 and falls back to v1 only when v2 is absent.
/// It never writes to Playnite or to the bridge-owned artwork cache.
/// </summary>
public sealed class PlayniteCatalogAdapter : IReadOnlyPlayniteCatalog
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    private static readonly string[] ArtworkExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
    private readonly string _snapshotPath;
    private readonly string? _legacySnapshotPath;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private readonly object _stateGate = new();
    private CatalogReadResult? _lastGood;
    private CatalogIssue? _lastIssue;
    private DateTimeOffset? _lastAttemptUtc;
    private Task<CatalogReadResult>? _inFlight;

    public PlayniteCatalogAdapter(
        string? snapshotPath = null,
        TimeProvider? timeProvider = null,
        string? legacySnapshotPath = null)
    {
        _snapshotPath = snapshotPath ?? GetDefaultSnapshotPath();
        _legacySnapshotPath = snapshotPath is null ? GetLegacySnapshotPath() : legacySnapshotPath;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static string GetDefaultSnapshotPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayniteCmdPal", "catalog-v2.json");

    public static string GetLegacySnapshotPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayniteCmdPal", "catalog-v1.json");

    public static string GetDefaultArtworkCachePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayniteCmdPal", "artwork-v2");

    /// <summary>
    /// Resolves a validated opaque digest only inside the fixed cache root. The cached bytes
    /// are rechecked for size, signature, extension, and SHA-256 before a path is returned.
    /// </summary>
    public static string? ResolveArtworkPath(string? artworkId, string? cacheRoot = null)
    {
        if (!SnapshotValidation.IsArtworkIdOrNull(artworkId) || artworkId is null)
        {
            return null;
        }

        try
        {
            var root = Path.GetFullPath(cacheRoot ?? GetDefaultArtworkCachePath());
            var directory = new DirectoryInfo(root);
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return ResolveArtworkInBackups(root, artworkId);
            }

            foreach (var extension in ArtworkExtensions)
            {
                var path = Path.GetFullPath(Path.Combine(root, artworkId + extension));
                if (!string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length <= 0 || input.Length > SnapshotLimits.MaxArtworkBytes)
                {
                    continue;
                }

                using var output = new MemoryStream();
                var chunk = new byte[16 * 1024];
                while (true)
                {
                    var read = input.Read(chunk, 0, chunk.Length);
                    if (read == 0)
                    {
                        break;
                    }

                    if (output.Length + read > SnapshotLimits.MaxArtworkBytes)
                    {
                        return null;
                    }

                    output.Write(chunk, 0, read);
                }

                var bytes = output.ToArray();
                if (!ArtworkFileSignature.TryGetExtension(bytes, out var detectedExtension) ||
                    !string.Equals(detectedExtension, extension, StringComparison.Ordinal))
                {
                    continue;
                }

                using var sha = SHA256.Create();
                var digest = string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
                if (string.Equals(digest, artworkId, StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return ResolveArtworkInBackups(root, artworkId);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string? ResolveArtworkInBackups(string root, string artworkId)
    {
        var parent = Path.GetDirectoryName(root);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            return null;
        }

        foreach (var backupRoot in Directory.GetDirectories(parent, Path.GetFileName(root) + ".backup-*"))
        {
            var backupDirectory = new DirectoryInfo(backupRoot);
            if ((backupDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            var resolved = ResolveArtworkPath(artworkId, backupRoot);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    public Task<CatalogReadResult> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<CatalogReadResult>(cancellationToken);
        }

        lock (_stateGate)
        {
            if (_inFlight is not null)
            {
                return Await(_inFlight, cancellationToken);
            }

            var now = _timeProvider.GetUtcNow();
            if (_lastAttemptUtc is { } lastAttempt && now - lastAttempt < RefreshInterval)
            {
                return Task.FromResult(CurrentResult(now, _lastIssue, isFromCache: true));
            }

            return Await(StartReadLocked(), cancellationToken);
        }
    }

    public Task<CatalogReadResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<CatalogReadResult>(cancellationToken);
        }

        lock (_stateGate)
        {
            return Await(_inFlight ?? StartReadLocked(), cancellationToken);
        }
    }

    private Task<CatalogReadResult> StartReadLocked()
    {
        _lastAttemptUtc = _timeProvider.GetUtcNow();
        var read = Task.Run(ReadCoreAsync);
        _inFlight = read;
        return read;
    }

    private async Task<CatalogReadResult> ReadCoreAsync()
    {
        await _readGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var parsed = await ReadAndParseAsync().ConfigureAwait(false);
            var now = _timeProvider.GetUtcNow();
            lock (_stateGate)
            {
                if (parsed.Snapshot is { } snapshot)
                {
                    var games = snapshot.Games
                        .Select(game => new GameEntry(
                            game.Id,
                            game.Name,
                            game.Source,
                            game.IsInstalled,
                            game.IconArtworkId,
                            game.Platform,
                            game.CoverArtworkId))
                        .ToArray();
                    _lastGood = new CatalogReadResult(
                        games,
                        snapshot.GeneratedAtUtc,
                        IsStale(snapshot.GeneratedAtUtc, now),
                        null,
                        parsed.SkippedRecordCount,
                        false);
                    _lastIssue = null;
                }
                else
                {
                    _lastIssue = parsed.Issue;
                }

                return CurrentResult(now, _lastIssue, parsed.Snapshot is null && _lastGood is not null);
            }
        }
        finally
        {
            lock (_stateGate)
            {
                _inFlight = null;
            }

            _readGate.Release();
        }
    }

    private async Task<CatalogSnapshotParseResult> ReadAndParseAsync()
    {
        var result = await ReadPathAsync(_snapshotPath).ConfigureAwait(false);
        if (result.Issue?.Code == CatalogErrorCode.Missing && _legacySnapshotPath is not null)
        {
            return await ReadPathAsync(_legacySnapshotPath).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<CatalogSnapshotParseResult> ReadPathAsync(string path)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CatalogSnapshotParseResult.Failure(CatalogErrorCode.Missing, "The Playnite catalog snapshot is missing.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return CatalogSnapshotParseResult.Failure(
                CatalogErrorCode.Unavailable,
                "The Playnite catalog snapshot could not be read. Check the companion plugin and file permissions, then refresh.");
        }

        using (stream)
        {
            try
            {
                if (stream.Length > SnapshotLimits.MaxBytes)
                {
                    return CatalogSnapshotParseResult.Failure(CatalogErrorCode.TooLarge, "The Playnite catalog snapshot exceeds the 32 MiB limit.");
                }

                using var buffer = new MemoryStream((int)Math.Min(stream.Length, SnapshotLimits.MaxBytes));
                var chunk = new byte[64 * 1024];
                while (true)
                {
                    var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length)).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    if (buffer.Length + read > SnapshotLimits.MaxBytes)
                    {
                        return CatalogSnapshotParseResult.Failure(CatalogErrorCode.TooLarge, "The Playnite catalog snapshot exceeds the 32 MiB limit.");
                    }

                    buffer.Write(chunk, 0, read);
                }

                return await Task.Run(() =>
                    CatalogSnapshotParser.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length))).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return CatalogSnapshotParseResult.Failure(
                    CatalogErrorCode.Unavailable,
                    "The Playnite catalog snapshot could not be read. Check the companion plugin and file permissions, then refresh.");
            }
        }
    }

    private CatalogReadResult CurrentResult(DateTimeOffset now, CatalogIssue? issue, bool isFromCache)
    {
        if (_lastGood is not { } good)
        {
            return CatalogReadResult.Unavailable(
                issue ?? new CatalogIssue(CatalogErrorCode.Missing, "The Playnite catalog bridge is unavailable."));
        }

        return new CatalogReadResult(
            good.Games,
            good.GeneratedAtUtc,
            IsStale(good.GeneratedAtUtc!.Value, now),
            issue,
            good.SkippedRecordCount,
            isFromCache);
    }

    private static bool IsStale(DateTimeOffset generatedAtUtc, DateTimeOffset now) =>
        now - generatedAtUtc >= StaleAfter;

    private static async Task<CatalogReadResult> Await(Task<CatalogReadResult> task, CancellationToken cancellationToken) =>
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
}