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
    public void IcoArtworkIsConvertedToPngBeforeItEntersTheContentAddressedCache()
    {
        var ico = new byte[26];
        ico[2] = 1;
        ico[4] = 1;
        ico[6] = 16;
        ico[7] = 16;
        ico[10] = 1;
        ico[12] = 32;
        ico[14] = 4;
        ico[18] = 22;
        ico[22] = 1; ico[23] = 2; ico[24] = 3; ico[25] = 4;

        var png = new byte[24];
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Array.Copy(signature, png, signature.Length);
        png[16] = 0x49; png[17] = 0x45; png[18] = 0x4E; png[19] = 0x44;
        var source = Path.Combine(_filesRoot, GameId, FileIdA + ".ico");
        File.WriteAllBytes(source, ico);

        using var cache = new ArtworkCacheGeneration(
            _cacheRoot, SnapshotLimits.MaxArtworkCacheBytes, SnapshotLimits.MaxArtworkBytes,
            null, File.Move, _ => { }, _ => png);
        var id = cache.TryImport(GameId, GameId + "\\" + FileIdA + ".ico", source, _filesRoot);

        Assert.Equal(Digest(png), id);
        cache.Activate(); cache.MarkPublished(); cache.Complete();
        Assert.True(File.Exists(Path.Combine(_cacheRoot, id + ".png")));
    }

    [Fact]
    public void IcoConversionFailureReportsTheExactStageAndException()
    {
        var ico = new byte[26];
        ico[2] = 1;
        ico[4] = 1;
        ico[6] = 16;
        ico[7] = 16;
        ico[10] = 1;
        ico[12] = 32;
        ico[14] = 4;
        ico[18] = 22;
        ico[22] = 1; ico[23] = 2; ico[24] = 3; ico[25] = 4;
        var source = Path.Combine(_filesRoot, GameId, FileIdA + ".ico");
        File.WriteAllBytes(source, ico);

        using var cache = new ArtworkCacheGeneration(
            _cacheRoot,
            convertIcoToPng: _ => throw new InvalidDataException("Injected ICO decoder failure."));
        string? diagnostic = null;

        var id = cache.TryImport(
            GameId,
            GameId + "\\" + FileIdA + ".ico",
            source,
            _filesRoot,
            message => diagnostic = message);

        Assert.Null(id);
        Assert.NotNull(diagnostic);
        Assert.Contains("stage=ico-conversion", diagnostic);
        Assert.Contains("Injected ICO decoder failure", diagnostic);
        Assert.Contains(source, diagnostic);
        Assert.Empty(Directory.GetFiles(GetStagingDirectory()));
    }

    [Fact]
    public void MissingIcoConverterReportsThatConversionIsNotConfigured()
    {
        var ico = new byte[26];
        ico[2] = 1;
        ico[4] = 1;
        ico[6] = 16;
        ico[7] = 16;
        ico[10] = 1;
        ico[12] = 32;
        ico[14] = 4;
        ico[18] = 22;
        ico[22] = 1; ico[23] = 2; ico[24] = 3; ico[25] = 4;
        var source = Path.Combine(_filesRoot, GameId, FileIdA + ".ico");
        File.WriteAllBytes(source, ico);

        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        string? diagnostic = null;
        var id = cache.TryImport(
            GameId,
            GameId + "\\" + FileIdA + ".ico",
            source,
            _filesRoot,
            message => diagnostic = message);

        Assert.Null(id);
        Assert.Contains("stage=ico-conversion", diagnostic);
        Assert.Contains("not configured", diagnostic);
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
    public void RejectsReparsePointArtworkSource()
    {
        if (!OperatingSystem.IsWindows()) return;

        var external = Path.Combine(_directory, "external.jpg");
        File.WriteAllBytes(external, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        var link = Path.Combine(_filesRoot, GameId, FileIdA + ".jpg");
        try { File.CreateSymbolicLink(link, external); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new InvalidOperationException(
                "This Windows test requires symbolic-link creation privileges; the security assertion was not executed.",
                exception);
        }

        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        Assert.Null(cache.TryImport(GameId, Reference(FileIdA), link, _filesRoot));
        Assert.Empty(Directory.GetFiles(GetStagingDirectory()));
    }

    [Fact]
    public void RejectsReparsePointGameDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;

        var externalGameDirectory = Path.Combine(_directory, "external-game");
        Directory.CreateDirectory(externalGameDirectory);
        File.WriteAllBytes(Path.Combine(externalGameDirectory, FileIdA + ".jpg"), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        var gameDirectory = Path.Combine(_filesRoot, GameId);
        Directory.Delete(gameDirectory);
        try { Directory.CreateSymbolicLink(gameDirectory, externalGameDirectory); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new InvalidOperationException(
                "This Windows test requires symbolic-link creation privileges; the security assertion was not executed.",
                exception);
        }

        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        Assert.Null(cache.TryImport(GameId, Reference(FileIdA), Path.Combine(gameDirectory, FileIdA + ".jpg"), _filesRoot));
        Assert.Empty(Directory.GetFiles(GetStagingDirectory()));
    }

    [Fact]
    public void RejectsReparsePointCacheRootDuringActivation()
    {
        if (!OperatingSystem.IsWindows()) return;

        var externalCache = Path.Combine(_directory, "external-cache");
        Directory.CreateDirectory(externalCache);
        Directory.CreateDirectory(Path.GetDirectoryName(_cacheRoot)!);
        try { Directory.CreateSymbolicLink(_cacheRoot, externalCache); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new InvalidOperationException(
                "This Windows test requires symbolic-link creation privileges; the security assertion was not executed.",
                exception);
        }

        using var cache = new ArtworkCacheGeneration(_cacheRoot);
        Assert.Throws<InvalidDataException>(() => cache.Activate());
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
    public void UnresolvedTemporaryFileCleanupStopsAllSubsequentImports()
    {
        var firstBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var secondBytes = new byte[] { 0xFF, 0xD8, 0x09, 0xFF, 0xD9 };
        var firstSource = WriteSource(FileIdA, firstBytes);
        var secondSource = WriteSource(FileIdB, secondBytes);
        using var cache = new ArtworkCacheGeneration(
            _cacheRoot,
            SnapshotLimits.MaxArtworkCacheBytes,
            SnapshotLimits.MaxArtworkBytes,
            null,
            (_, _) => throw new IOException("Injected move failure."),
            _ => { }); // Simulate a filesystem that refuses temporary-file cleanup.

        Assert.Null(cache.TryImport(GameId, Reference(FileIdA), firstSource, _filesRoot));
        var retainedTemporaryFiles = Directory.GetFiles(GetStagingDirectory());
        Assert.Single(retainedTemporaryFiles);
        Assert.EndsWith(".tmp", retainedTemporaryFiles[0]);

        Assert.Null(cache.TryImport(GameId, Reference(FileIdB), secondSource, _filesRoot));
        Assert.Equal(retainedTemporaryFiles, Directory.GetFiles(GetStagingDirectory()));
        Assert.Throws<IOException>(() => cache.Activate());
    }

    [Fact]
    public void RevalidationDetectsChangedBytesBehindAnUnchangedPlayniteReference()
    {
        var originalBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var updatedBytes = new byte[] { 0xFF, 0xD8, 0x08, 0xFF, 0xD9 };
        var source = WriteSource(FileIdA, originalBytes);
        string? originalId;
        using (var first = new ArtworkCacheGeneration(_cacheRoot))
        {
            originalId = first.TryImport(GameId, Reference(FileIdA), source, _filesRoot);
            first.Activate(); first.MarkPublished(); first.Complete();
        }

        File.WriteAllBytes(source, updatedBytes);
        using (var second = new ArtworkCacheGeneration(_cacheRoot))
        {
            var updatedId = second.TryImport(GameId, Reference(FileIdA), source, _filesRoot);
            Assert.NotEqual(originalId, updatedId);
            second.Activate(); second.MarkPublished(); second.Complete();
        }

        Assert.False(File.Exists(Path.Combine(_cacheRoot, originalId + ".jpg")));
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(updatedBytes) + ".jpg")));
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
    public void MarkerDeletionFailureAfterRollbackCannotDeleteRestoredPreviousCache()
    {
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x06, 0xFF, 0xD9 };
        var oldSource = WriteSource(FileIdA, oldBytes);
        var newSource = WriteSource(FileIdB, newBytes);
        using (var initial = new ArtworkCacheGeneration(_cacheRoot))
        {
            initial.TryImport(GameId, Reference(FileIdA), oldSource, _filesRoot);
            initial.Activate(); initial.MarkPublished(); initial.Complete();
        }

        var transaction = new ArtworkCacheGeneration(_cacheRoot);
        transaction.TryImport(GameId, Reference(FileIdB), newSource, _filesRoot);
        transaction.Activate();
        var marker = _cacheRoot + ".transaction";
        using (new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => transaction.Rollback());
            Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
            transaction.Dispose();
            Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        }

        using (var recovery = new ArtworkCacheGeneration(_cacheRoot)) { }
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        Assert.False(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
    }

    [Fact]
    public void RecoveryDefersWhenPublishedSnapshotEvidenceIsExclusivelyLocked()
    {
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x07, 0xFF, 0xD9 };
        var oldSource = WriteSource(FileIdA, oldBytes);
        var newSource = WriteSource(FileIdB, newBytes);
        using (var initial = new ArtworkCacheGeneration(_cacheRoot))
        {
            initial.TryImport(GameId, Reference(FileIdA), oldSource, _filesRoot);
            initial.Activate(); initial.MarkPublished(); initial.Complete();
        }

        const string token = "2026-11-04T05:34:25.0000000Z";
        var snapshotPath = Path.Combine(_directory, "catalog-v2.json");
        var transaction = new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath);
        transaction.TryImport(GameId, Reference(FileIdB), newSource, _filesRoot);
        transaction.Activate(token);
        File.WriteAllText(snapshotPath, "{\"schemaVersion\":2,\"generatedAtUtc\":\"" + token + "\",\"games\":[]}");

        using (new FileStream(snapshotPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath));
            Assert.True(File.Exists(_cacheRoot + ".transaction"));
            Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
            Assert.True(Directory.GetDirectories(Path.GetDirectoryName(_cacheRoot)!, "artwork-v2.backup-*").Length == 1);
        }

        using (var recovery = new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath)) { }
        Assert.True(File.Exists(Path.Combine(_cacheRoot, Digest(newBytes) + ".jpg")));
        Assert.False(File.Exists(Path.Combine(_cacheRoot, Digest(oldBytes) + ".jpg")));
        Assert.False(File.Exists(_cacheRoot + ".transaction"));
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
    public async Task MetadataOnlyFallbackPublicationCleanupFailurePreservesGenerationForRecovery()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string token = "2026-11-04T12:00:00.0000000Z";
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var source = WriteSource(FileIdA, bytes);
        var snapshotPath = Path.Combine(_directory, "catalog-v2.json");
        var cache = new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath);
        var artworkId = cache.TryImport(GameId, Reference(FileIdA), source, _filesRoot);
        Assert.NotNull(artworkId);
        cache.Activate(token);

        var activeArtwork = Path.Combine(_cacheRoot, artworkId + ".jpg");
        using (new FileStream(activeArtwork, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var publishAttempts = 0;
            async Task PublishWithInjectedSizeFailure(
                string path, CatalogSnapshot candidate, CancellationToken cancellationToken)
            {
                publishAttempts++;
                if (publishAttempts == 1)
                    throw new InvalidDataException("Injected artwork-bearing snapshot size failure.");

                // The production fallback snapshot is passed to the real atomic publisher.
                await SnapshotFilePublisher.PublishAsync(path, candidate, cancellationToken);
            }

            var oversizedArtworkSnapshot = new CatalogSnapshot(
                DateTimeOffset.Parse(token, null, System.Globalization.DateTimeStyles.RoundtripKind),
                new[] { new SnapshotGame(GameId, "Game", true, coverArtworkId: artworkId, iconArtworkId: artworkId) },
                SnapshotLimits.SchemaVersion);

            await Assert.ThrowsAsync<IOException>(() => ArtworkSnapshotPublisher.PublishAsync(
                snapshotPath, oversizedArtworkSnapshot, cache, PublishWithInjectedSizeFailure));

            Assert.Equal(2, publishAttempts);
            var parsed = CatalogSnapshotParser.Parse(await File.ReadAllBytesAsync(snapshotPath));
            Assert.True(parsed.Snapshot is not null);
            var publishedGame = Assert.Single(parsed.Snapshot!.Games);
            Assert.Null(publishedGame.CoverArtworkId);
            Assert.Null(publishedGame.IconArtworkId);
            Assert.True(File.Exists(_cacheRoot + ".transaction"));
            Assert.True(File.Exists(activeArtwork));
        }

        cache.Dispose();
        using (var recovery = new ArtworkCacheGeneration(_cacheRoot, snapshotPath: snapshotPath)) { }
        Assert.False(File.Exists(_cacheRoot + ".transaction"));
        Assert.True(Directory.Exists(_cacheRoot));
        Assert.True(File.Exists(activeArtwork));
    }

    [Fact]
    public void OrphanedBackupCleanupFailureIsSurfacedAndBlocksNewGeneration()
    {
        var parent = Path.GetDirectoryName(_cacheRoot)!;
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(_cacheRoot);
        var orphanedBackup = _cacheRoot + ".backup-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(orphanedBackup);
        var lockedFile = Path.Combine(orphanedBackup, "locked.bin");
        File.WriteAllBytes(lockedFile, new byte[] { 1, 2, 3 });

        if (OperatingSystem.IsWindows())
        {
            using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => new ArtworkCacheGeneration(_cacheRoot));
                Assert.True(Directory.Exists(orphanedBackup));
            }
        }

        using (var recovery = new ArtworkCacheGeneration(_cacheRoot)) { }
        Assert.False(Directory.Exists(orphanedBackup));
    }

    [Fact]
    public void StartupRecoveryPreservesMarkerWhenPreviousCacheAndBackupAreBothMissing()
    {
        var oldBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var newBytes = new byte[] { 0xFF, 0xD8, 0x0A, 0xFF, 0xD9 };
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
        var backup = Directory.GetDirectories(
            Path.GetDirectoryName(_cacheRoot)!, "artwork-v2.backup-*").Single();
        interrupted.PreserveForRecovery();

        // Simulate loss of both the active generation and the rollback generation.
        Directory.Delete(_cacheRoot, recursive: true);
        Directory.Delete(backup, recursive: true);

        var marker = _cacheRoot + ".transaction";
        Assert.Throws<IOException>(() => new ArtworkCacheGeneration(_cacheRoot));
        Assert.True(File.Exists(marker));
        Assert.False(Directory.Exists(_cacheRoot));
        Assert.False(Directory.Exists(backup));
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