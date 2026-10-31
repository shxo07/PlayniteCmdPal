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

    /// <summary>False when no valid snapshot has ever been loaded; an empty valid library is available.</summary>
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
    /// <summary>Loads on first use, then revalidates the file at most once per refresh interval.</summary>
    Task<CatalogReadResult> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests a fresh read, coalescing concurrent callers onto the same read.</summary>
    Task<CatalogReadResult> RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only, in-memory snapshot adapter. All file I/O and parsing run asynchronously and never
/// write to Playnite. The last valid snapshot survives missing/malformed later files.
/// </summary>
public sealed class PlayniteCatalogAdapter : IReadOnlyPlayniteCatalog
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    private readonly string _snapshotPath;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private readonly object _stateGate = new();
    private CatalogReadResult? _lastGood;
    private CatalogIssue? _lastIssue;
    private DateTimeOffset? _lastAttemptUtc;
    private Task<CatalogReadResult>? _inFlight;

    public PlayniteCatalogAdapter(string? snapshotPath = null, TimeProvider? timeProvider = null)
    {
        _snapshotPath = snapshotPath ?? GetDefaultSnapshotPath();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static string GetDefaultSnapshotPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayniteCmdPal", "catalog-v1.json");

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
        // Task.Run prevents a synchronous file/parse completion from clearing _inFlight
        // before it has been assigned, and keeps all adapter work off the caller's UI thread.
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
                        .Select(game => new GameEntry(game.Id, game.Name, game.Source, game.IsInstalled, null, game.Platform))
                        .ToArray();
                    _lastGood = new CatalogReadResult(games, snapshot.GeneratedAtUtc, IsStale(snapshot.GeneratedAtUtc, now), null, parsed.SkippedRecordCount, false);
                    _lastIssue = null;
                }
                else
                {
                    _lastIssue = parsed.Issue;
                }

                return CurrentResult(now, _lastIssue, isFromCache: parsed.Snapshot is null && _lastGood is not null);
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
        FileStream stream;
        try
        {
            stream = new FileStream(_snapshotPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CatalogSnapshotParseResult.Failure(CatalogErrorCode.Missing, "The Playnite catalog bridge is unavailable. Install/start the companion plugin and refresh.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return CatalogSnapshotParseResult.Failure(CatalogErrorCode.Unavailable, "The Playnite catalog snapshot could not be read. Check the companion plugin and file permissions, then refresh.");
        }

        using (stream)
        {
            try
            {
                if (stream.Length > SnapshotLimits.MaxBytes)
                {
                    return CatalogSnapshotParseResult.Failure(CatalogErrorCode.TooLarge, "The Playnite catalog snapshot exceeds the 32 MiB limit.");
                }

                using var buffer = new MemoryStream(capacity: (int)Math.Min(stream.Length, SnapshotLimits.MaxBytes));
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

                return await Task.Run(() => CatalogSnapshotParser.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length))).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return CatalogSnapshotParseResult.Failure(CatalogErrorCode.Unavailable, "The Playnite catalog snapshot could not be read. Check the companion plugin and file permissions, then refresh.");
            }
        }
    }

    private CatalogReadResult CurrentResult(DateTimeOffset now, CatalogIssue? issue, bool isFromCache)
    {
        if (_lastGood is not { } good)
        {
            return CatalogReadResult.Unavailable(issue ?? new CatalogIssue(CatalogErrorCode.Missing, "The Playnite catalog bridge is unavailable."));
        }

        return new CatalogReadResult(good.Games, good.GeneratedAtUtc, IsStale(good.GeneratedAtUtc!.Value, now), issue, good.SkippedRecordCount, isFromCache);
    }

    private static bool IsStale(DateTimeOffset generatedAtUtc, DateTimeOffset now) => now - generatedAtUtc >= StaleAfter;

    private static async Task<CatalogReadResult> Await(Task<CatalogReadResult> task, CancellationToken cancellationToken) =>
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
}
