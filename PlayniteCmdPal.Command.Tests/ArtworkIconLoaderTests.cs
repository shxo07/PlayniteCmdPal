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
    public async Task SavedPlayniteIconIsUsedAndCoverArtworkIsNeverRequested()
    {
        var resolved = new ConcurrentQueue<string>();
        var loader = CreateLoader(id =>
        {
            resolved.Enqueue(id);
            return "C:\\artwork\\icon.png";
        }, (_, _) => Task.FromResult(true));

        var icon = await loader.GetIconAsync(Game(cover: CoverId, icon: IconId), CancellationToken.None);

        Assert.NotNull(icon);
        Assert.Equal(new[] { IconId }, resolved.ToArray());
    }

    [Fact]
    public async Task MissingOrInvalidSavedIconDoesNotFallBackToCoverArtwork()
    {
        var resolved = new ConcurrentQueue<string>();
        var loader = CreateLoader(id =>
        {
            resolved.Enqueue(id);
            return "C:\\artwork\\broken.ico";
        }, (_, _) => Task.FromResult(false));

        var icon = await loader.GetIconAsync(Game(cover: CoverId, icon: IconId), CancellationToken.None);

        Assert.Null(icon);
        Assert.Equal(new[] { IconId }, resolved.ToArray());
    }

    [Fact]
    public async Task MissingOrUndecodableArtworkReturnsNullWithoutThrowing()
    {
        var loader = CreateLoader(_ => null, (_, _) => Task.FromResult(false));
        var icon = await loader.GetIconAsync(Game(cover: CoverId), CancellationToken.None);
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

    [Fact]
    public async Task TransientResolutionFailureIsNotCachedAndCanRecover()
    {
        var attempts = 0;
        var loader = CreateLoader(_ =>
        {
            return Interlocked.Increment(ref attempts) == 1 ? null : "C:\\artwork\\recovered.png";
        }, (_, _) => Task.FromResult(true));
        var game = Game(cover: CoverId);

        Assert.Null(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.NotNull(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task DecoderFailureIsNotCachedAndCanRecover()
    {
        var attempts = 0;
        var loader = CreateLoader(_ => "C:\\artwork\\recover-decoder.png", (_, _) =>
            Task.FromResult(Interlocked.Increment(ref attempts) > 1));
        var game = Game(cover: CoverId);

        Assert.Null(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.NotNull(await loader.GetIconAsync(game, CancellationToken.None));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task CancellingAQueuedLoadPreventsItsResolverFromStarting()
    {
        var allDecodersStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDecoders = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolveCount = 0;
        var decoderCount = 0;
        var loader = CreateLoader(_ =>
        {
            Interlocked.Increment(ref resolveCount);
            return "C:\\artwork\\queued.png";
        }, async (_, _) =>
        {
            if (Interlocked.Increment(ref decoderCount) == 4) allDecodersStarted.TrySetResult(true);
            await releaseDecoders.Task;
            return true;
        });

        var active = new[]
        {
            loader.GetIconAsync(Game(cover: CoverId), CancellationToken.None),
            loader.GetIconAsync(Game(cover: IconId), CancellationToken.None),
            loader.GetIconAsync(Game(cover: OtherId), CancellationToken.None),
            loader.GetIconAsync(Game(cover: "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"), CancellationToken.None),
        };
        await allDecodersStarted.Task;

        using var queuedCancellation = new CancellationTokenSource();
        var queued = loader.GetIconAsync(
            Game(cover: "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"),
            queuedCancellation.Token);
        queuedCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        releaseDecoders.TrySetResult(true);
        await Task.WhenAll(active);
        Assert.Equal(4, resolveCount);
    }

    [Fact]
    public async Task CancellingOneWaiterDoesNotCancelSharedArtworkNeededByAnother()
    {
        var decodeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDecode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodeCount = 0;
        var loader = CreateLoader(_ => "C:\\artwork\\shared.png", async (_, token) =>
        {
            Interlocked.Increment(ref decodeCount);
            decodeStarted.TrySetResult(true);
            await releaseDecode.Task.WaitAsync(token);
            return true;
        });
        var game = Game(cover: CoverId);
        using var firstCancellation = new CancellationTokenSource();

        var first = loader.GetIconAsync(game, firstCancellation.Token);
        await decodeStarted.Task;
        var second = loader.GetIconAsync(game, CancellationToken.None);
        firstCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        releaseDecode.TrySetResult(true);
        Assert.NotNull(await second);
        Assert.Equal(1, decodeCount);
    }

    [Fact]
    public async Task UnderlyingArtworkWorkIsCancelledWhenItsLastWaiterLeaves()
    {
        var decodeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodeCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = CreateLoader(_ => "C:\\artwork\\cancel.png", async (_, token) =>
        {
            decodeStarted.TrySetResult(true);
            using var registration = token.Register(() => decodeCancelled.TrySetResult(true));
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        });
        using var cancellation = new CancellationTokenSource();

        var request = loader.GetIconAsync(Game(cover: CoverId), cancellation.Token);
        await decodeStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await decodeCancelled.Task;
    }

    private static ArtworkIconLoader CreateLoader(
        Func<string, string?> resolvePath,
        Func<string, CancellationToken, Task<bool>> decoder,
        int capacity = 96) => new(resolvePath, decoder, capacity, _ => true);

    private static GameEntry Game(string? cover = null, string? icon = null) =>
        new(
            "11111111-1111-4111-8111-111111111111",
            "Example game",
            IconReference: icon ?? cover,
            CoverArtworkReference: cover);
}
