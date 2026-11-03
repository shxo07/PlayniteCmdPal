using System.Security.Cryptography;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class ArtworkCacheGenerationTests : IDisposable
{
    private const string GameId = "11111111-1111-4111-8111-111111111111";
    private const string FileIdA = "22222222-2222-4222-8222-222222222222";
    private const string FileIdB = "33333333-3333-4333-8333-333333333333";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.ArtworkTests", Guid.NewGuid().ToString("N"));
    private readonly string _filesRoot;
    private readonly string _cacheRoot;

    public ArtworkCacheGenerationTests()
    {
        _filesRoot = Path.Combine(_directory, "playnite", "files");
        _cacheRoot = Path.Combine(_directory, "bridge", "artwork-v2");
        Directory.CreateDirectory(Path.Combine(_filesRoot, GameId));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void ImportsValidArtworkIntoTheBridgeOwnedContentAddressedCache()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var source = WriteSource(FileIdA, bytes);
        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        var id = cache.TryImport(GameId, Reference(FileIdA), source, _filesRoot);
        Assert.Equal(Digest(bytes), id);
        cache.Activate(); cache.MarkPublished(); cache.Complete();
        Assert.True(File.Exists(Path.Combine(_cacheRoot, id + ".jpg")));
        Assert.Single(Directory.GetFiles(_cacheRoot));
    }


    [Fact]
    public void MissingArtworkFileProducesNoIdentifierAndDoesNotFailTheCatalogExport()
    {
        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        var missingPath = Path.Combine(_filesRoot, GameId, FileIdA + ".jpg");

        var id = cache.TryImport(GameId, Reference(FileIdA), missingPath, _filesRoot);

        Assert.Null(id);
    }

    [Fact]
    public void IdenticalArtworkUsesOneStableCacheEntry()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0x01, 0xFF, 0xD9 };
        var first = WriteSource(FileIdA, bytes);
        var second = WriteSource(FileIdB, bytes);
        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        var firstId = cache.TryImport(GameId, Reference(FileIdA), first, _filesRoot);
        var secondId = cache.TryImport(GameId, Reference(FileIdB), second, _filesRoot);
        Assert.Equal(firstId, secondId);
        Assert.Single(Directory.GetFiles(GetStagingDirectory()));
    }

    [Fact]
    public void RejectsTraversalRawPathsMismatchedGameFoldersAndUnsupportedExtensions()
    {
        var outside = Path.Combine(_directory, "outside.jpg");
        File.WriteAllBytes(outside, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        Assert.Null(cache.TryImport(GameId, @"..\..\outside.jpg", outside, _filesRoot));
        Assert.Null(cache.TryImport(GameId, outside, outside, _filesRoot));
        Assert.Null(cache.TryImport(GameId, "33333333-3333-4333-8333-333333333333/" + FileIdA + ".jpg",
            Path.Combine(_filesRoot, "33333333-3333-4333-8333-333333333333", FileIdA + ".jpg"), _filesRoot));
        Assert.Null(cache.TryImport(GameId, GameId + "/" + FileIdA + ".bmp", outside, _filesRoot));
    }

    [Fact]
    public void RejectsTruncatedUnknownAndOversizedImageFiles()
    {
        var truncated = WriteSource(FileIdA, new byte[] { 0xFF, 0xD8 });
        var unknown = WriteSource(FileIdB, new byte[] { 1, 2, 3, 4 });
        using var cache = new ArtworkCacheGeneration(_cacheRoot, maxArtworkBytes: 4);
        Assert.Null(cache.TryImport(GameId, Reference(FileIdA), truncated, _filesRoot));
        Assert.Null(cache.TryImport(GameId, Reference(FileIdB), unknown, _filesRoot));
        const string fileId = "44444444-4444-4444-8444-444444444444";
        var oversized = WriteSource(fileId, new byte[] { 0xFF, 0xD8, 0x01, 0xFF, 0xD9 });
        Assert.Null(cache.TryImport(GameId, Reference(fileId), oversized, _filesRoot));
    }

    [Fact]
    public void EnforcesTheCacheLimitWhileImporting()
    {
        var firstBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var secondBytes = new byte[] { 0xFF, 0xD8, 0x01, 0xFF, 0xD9 };
        var first = WriteSource(FileIdA, firstBytes);
        var second = WriteSource(FileIdB, secondBytes);
        using var cache = new ArtworkCacheGeneration(_cacheRoot, maxCacheBytes: firstBytes.Length);
        Assert.NotNull(cache.TryImport(GameId, Reference(FileIdA), first, _filesRoot));
        Assert.Null(cache.TryImport(GameId, Reference(FileIdB), second, _filesRoot));
    }

    [Fact]
    public void ReplacesTheWholeCacheGenerationAfterSuccessfulPublication()
    {
        var firstBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var secondBytes = new byte[] { 0xFF, 0xD8, 0x02, 0xFF, 0xD9 };
        var firstSource = WriteSource(FileIdA, firstBytes);
        var secondSource = WriteSource(FileIdB, secondBytes);
        var firstId = Digest(firstBytes);
        var secondId = Digest(secondBytes);
        using (var first = new ArtworkCacheGeneration(_cacheRoot))
        {
            first.TryImport(GameId, Reference(FileIdA), firstSource, _filesRoot);
            first.Activate(); first.MarkPublished(); first.Complete();
        }
        using (var second = new ArtworkCacheGeneration(_cacheRoot))
        {
            second.TryImport(GameId, Reference(FileIdB), secondSource, _filesRoot);
            second.Activate(); second.MarkPublished(); second.Complete();
        }
        Assert.False(File.Exists(Path.Combine(_cacheRoot, firstId + ".jpg")));
        Assert.True(File.Exists(Path.Combine(_cacheRoot, secondId + ".jpg")));
        Assert.Single(Directory.GetFiles(_cacheRoot));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(_cacheRoot)!, "artwork-v2.backup-*"));
    }

    [Fact]
    public void FailedPublicationRollsBackToThePreviousCompleteCache()
    {
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x02, 0xFF, 0xD9 };
        var oldSource = WriteSource(FileIdA, oldBytes);
        var newSource = WriteSource(FileIdB, newBytes);
        using (var first = new ArtworkCacheGeneration(_cacheRoot))
        {
            first.TryImport(GameId, Reference(FileIdA), oldSource, _filesRoot);
            first.Activate(); first.MarkPublished(); first.Complete();
        }
        using (var failed = new ArtworkCacheGeneration(_cacheRoot))
        {
            failed.TryImport(GameId, Reference(FileIdB), newSource, _filesRoot);
            failed.Activate();
            failed.Rollback();
        }
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        Assert.False(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
    }

    [Fact]
    public void CleanupFailureKeepsPublishedTransactionMarkerForRecovery()
    {
        if (!OperatingSystem.IsWindows()) return;

        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var source = WriteSource(FileIdA, bytes);
        using (var initial = new ArtworkCacheGeneration(_cacheRoot))
        {
            initial.TryImport(GameId, Reference(FileIdA), source, _filesRoot);
            initial.Activate();
            initial.MarkPublished();
            initial.Complete();
        }

        var next = new ArtworkCacheGeneration(_cacheRoot);
        next.TryImport(GameId, Reference(FileIdA), source, _filesRoot);
        next.Activate();
        next.MarkPublished();
        var backup = Directory.GetDirectories(Path.GetDirectoryName(_cacheRoot)!, "artwork-v2.backup-*").Single();
        var backupFile = Directory.GetFiles(backup).Single();
        using (new FileStream(backupFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => next.Complete());
            Assert.True(File.Exists(_cacheRoot + ".transaction"));
        }

        next.Dispose();
        using (var recovery = new ArtworkCacheGeneration(_cacheRoot)) { }
        Assert.False(File.Exists(_cacheRoot + ".transaction"));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(_cacheRoot)!, "artwork-v2.backup-*"));
    }

    [Fact]
    public void StartupRecoveryRestoresPreviousCacheAfterInterruptedSwap()
    {
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x03, 0xFF, 0xD9 };
        var oldSource = WriteSource(FileIdA, oldBytes);
        var newSource = WriteSource(FileIdB, newBytes);
        using (var initial = new ArtworkCacheGeneration(_cacheRoot))
        {
            initial.TryImport(GameId, Reference(FileIdA), oldSource, _filesRoot);
            initial.Activate(); initial.MarkPublished(); initial.Complete();
        }
        var interrupted = new ArtworkCacheGeneration(_cacheRoot);
        interrupted.TryImport(GameId, Reference(FileIdB), newSource, _filesRoot);
        interrupted.Activate();
        using (var recovery = new ArtworkCacheGeneration(_cacheRoot)) { }
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        Assert.False(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
    }

    [Fact]
    public void StartupRecoveryKeepsCommittedCacheGeneration()
    {
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x04, 0xFF, 0xD9 };
        var oldSource = WriteSource(FileIdA, oldBytes);
        var newSource = WriteSource(FileIdB, newBytes);
        using (var initial = new ArtworkCacheGeneration(_cacheRoot))
        {
            initial.TryImport(GameId, Reference(FileIdA), oldSource, _filesRoot);
            initial.Activate(); initial.MarkPublished(); initial.Complete();
        }
        var committed = new ArtworkCacheGeneration(_cacheRoot);
        committed.TryImport(GameId, Reference(FileIdB), newSource, _filesRoot);
        committed.Activate();
        committed.MarkPublished();
        using (var recovery = new ArtworkCacheGeneration(_cacheRoot)) { }
        Assert.False(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
    }


    [Fact]
    public void StartupRecoveryRecognizesSnapshotPublishedBeforeCommitMarkerWasWritten()
    {
        const string token = "2026-11-01T12:34:56.0000000Z";
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x05, 0xFF, 0xD9 };
        var oldSource = WriteSource(FileIdA, oldBytes);
        var newSource = WriteSource(FileIdB, newBytes);
        using (var initial = new ArtworkCacheGeneration(_cacheRoot))
        {
            initial.TryImport(GameId, Reference(FileIdA), oldSource, _filesRoot);
            initial.Activate(); initial.MarkPublished(); initial.Complete();
        }

        var snapshotPath = Path.Combine(_directory, "catalog-v2.json");
        var interrupted = new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath);
        interrupted.TryImport(GameId, Reference(FileIdB), newSource, _filesRoot);
        interrupted.Activate(token);
        File.WriteAllText(snapshotPath, "{\"schemaVersion\":2,\"generatedAtUtc\":\"" + token + "\",\"games\":[]}");

        using (var recovery = new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath)) { }

        Assert.False(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
    }

    [Fact]
    public void StartupRecoveryRejectsBackupMarkerPathTraversal()
    {
        var parent = Path.GetDirectoryName(_cacheRoot)!;
        Directory.CreateDirectory(parent);
        var outside = Path.Combine(parent, "outside-cache");
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        var traversalBackup = _cacheRoot + ".backup-" + Guid.NewGuid().ToString("N") +
            Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "outside-cache";
        File.WriteAllText(_cacheRoot + ".transaction",
            "pending" + Environment.NewLine + traversalBackup + Environment.NewLine + "1" + Environment.NewLine);

        Assert.Throws<IOException>(() => new ArtworkCacheGeneration(_cacheRoot));
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    private string WriteSource(string fileId, byte[] bytes)
    {
        var directory = Path.Combine(_filesRoot, GameId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileId + ".jpg");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string GetStagingDirectory() => Directory.GetDirectories(Path.GetDirectoryName(_cacheRoot)!, "artwork-v2.staging-*").Single();
    private static string Reference(string fileId) => GameId + "\\" + fileId + ".jpg";
    private static string Digest(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
    }
}