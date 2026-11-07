using System.Collections.Concurrent;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal;
using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Command.Tests;

public sealed class ArtworkIconLoaderTests
{
    private const string CoverId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string IconId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string OtherId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task CoverArtworkIsPreferredWhenBothCandidatesDecode()
    {
        var decoded = new ConcurrentQueue<string>();
        var loader = CreateLoader(_ => "C:\\artwork\\image.png", (path, _) =>
        {
            decoded.Enqueue(path);
            return Task.FromResult(true);
        });

        var icon = await loader.GetIconAsync(Game(cover: CoverId, icon: IconId), CancellationToken.None);

        Assert.NotNull(icon);
        Assert.Single(decoded);
    }

    [Fact]
    public async Task InvalidCoverFallsBackToTheGameIcon()
    {
        var resolved = new ConcurrentQueue<string>();
        var loader = CreateLoader(id =>
        {
            resolved.Enqueue(id);
            return id == CoverId ? "C:\\artwork\\broken.png" : "C:\\artwork\\icon.png";
        }, (path, _) => Task.FromResult(path.EndsWith("icon.png", StringComparison.Ordinal)));

        var icon = await loader.GetIconAsync(Game(cover: CoverId, icon: IconId), CancellationToken.None);

        Assert.NotNull(icon);
        Assert.Equal(new[] { CoverId, IconId }, resolved.ToArray());
    }

    [Fact]
    public async Task MissingOrUndecodableArtworkReturnsNullWithoutThrowing()
    {
        var loader = CreateLoader(_ => null, (_, _) => Task.FromResult(false));
        var icon = await loader.GetIconAsync(Game(cover: CoverId, icon: IconId), CancellationToken.None);
        Assert.Null(icon);
    }

    [Fact]
    public async Task RepeatedRequestsReuseTheValidatedArtworkCache()
    {
        var resolveCount = 0;
        var decodeCount = 0;
        var loader = CreateLoader(_ =>
        {
            Interlocked.Increment(ref resolveCount);
            return "C:\\artwork\\cover.png";
        }, (_, _) =>
        {
            Interlocked.Increment(ref decodeCount);
            return Task.FromResult(true);
        });
        var game = Game(cover: CoverId);

        Assert.NotNull(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.NotNull(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.Equal(1, resolveCount);
        Assert.Equal(1, decodeCount);
    }

    [Fact]
    public async Task CacheEvictsLeastRecentlyUsedEntriesAtItsConfiguredBound()
    {
        var resolveCounts = new ConcurrentDictionary<string, int>();
        var loader = CreateLoader(id =>
        {
            resolveCounts.AddOrUpdate(id, 1, (_, count) => count + 1);
            return $"C:\\artwork\\{id}.png";
        }, (_, _) => Task.FromResult(true), capacity: 2);

        await loader.GetIconAsync(Game(cover: CoverId), CancellationToken.None);
        await loader.GetIconAsync(Game(cover: IconId), CancellationToken.None);
        await loader.GetIconAsync(Game(cover: CoverId), CancellationToken.None);
        await loader.GetIconAsync(Game(cover: OtherId), CancellationToken.None);
        await loader.GetIconAsync(Game(cover: IconId), CancellationToken.None);

        Assert.Equal(1, resolveCounts[CoverId]);
        Assert.Equal(2, resolveCounts[IconId]);
        Assert.Equal(1, resolveCounts[OtherId]);
    }

    [Fact]
    public async Task CatalogRefreshInvalidatesTheArtworkCache()
    {
        var resolveCount = 0;
        var loader = CreateLoader(_ =>
        {
            Interlocked.Increment(ref resolveCount);
            return "C:\\artwork\\cover.png";
        }, (_, _) => Task.FromResult(true));
        var game = Game(cover: CoverId);

        Assert.NotNull(await loader.GetIconAsync(game, CancellationToken.None));
        loader.Invalidate();
        Assert.NotNull(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.Equal(2, resolveCount);
    }

    [Fact]
    public async Task ConcurrentRequestsForSameArtworkShareOneDecode()
    {
        var decodeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDecode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodeCount = 0;
        var loader = CreateLoader(_ => "C:\\artwork\\cover.png", async (_, _) =>
        {
            Interlocked.Increment(ref decodeCount);
            decodeStarted.TrySetResult(true);
            await releaseDecode.Task;
            return true;
        });

        var game = Game(cover: CoverId);
        var first = loader.GetIconAsync(game, CancellationToken.None);
        await decodeStarted.Task;
        var second = loader.GetIconAsync(game, CancellationToken.None);
        releaseDecode.TrySetResult(true);

        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(1, decodeCount);
    }

    [Fact]
    public async Task MalformedArtworkIdentifiersAreRejectedBeforeResolution()
    {
        var resolveCount = 0;
        var loader = CreateLoader(_ =>
        {
            Interlocked.Increment(ref resolveCount);
            return "C:\\artwork\\unexpected.png";
        }, (_, _) => Task.FromResult(true));

        var icon = await loader.GetIconAsync(Game(cover: "../not-an-id"), CancellationToken.None);

        Assert.Null(icon);
        Assert.Equal(0, resolveCount);
    }

    private static ArtworkIconLoader CreateLoader(
        Func<string, string?> resolvePath,
        Func<string, CancellationToken, Task<bool>> decoder,
        int capacity = 96) => new(resolvePath, decoder, capacity, _ => true);

    private static GameEntry Game(string? cover = null, string? icon = null) =>
        new(
            "11111111-1111-4111-8111-111111111111",
            "Example game",
            IconReference: icon,
            CoverArtworkReference: cover);
}
