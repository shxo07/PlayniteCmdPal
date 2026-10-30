using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class CatalogExportCoordinatorTests
{
    [Fact]
    public async Task CoalescesBurstIntoOneExport()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var coordinator = new CatalogExportCoordinator(async _ =>
        {
            Interlocked.Increment(ref count);
            entered.TrySetResult();
            await release.Task;
            finished.TrySetResult();
        }, TimeSpan.FromMilliseconds(30));

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        coordinator.RequestExport();
        coordinator.RequestExport();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        Assert.Equal(1, Volatile.Read(ref count));
        coordinator.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RequestDuringExportSchedulesOneFollowUp()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var coordinator = new CatalogExportCoordinator(async _ =>
        {
            var current = Interlocked.Increment(ref count);
            if (current == 1)
            {
                first.TrySetResult();
                await releaseFirst.Task;
            }
            else
            {
                second.TrySetResult();
            }
        }, TimeSpan.FromMilliseconds(10));

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.RequestExport();
        coordinator.RequestExport();
        releaseFirst.SetResult();
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Volatile.Read(ref count));
        coordinator.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FailedExportDoesNotStopLaterRequests()
    {
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var succeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var coordinator = new CatalogExportCoordinator(_ =>
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                failed.TrySetResult();
                throw new IOException("synthetic failure");
            }

            succeeded.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(10), _ => { });

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.RequestExport();
        await succeeded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Volatile.Read(ref count));
        coordinator.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisposedCoordinatorCannotRunOrAcceptRequests()
    {
        var count = 0;
        var coordinator = new CatalogExportCoordinator(_ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }, TimeSpan.Zero);

        coordinator.Dispose();
        coordinator.RequestExport();
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync());
        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public async Task DisposeCancelsPendingDebounce()
    {
        using var coordinator = new CatalogExportCoordinator(_ => Task.CompletedTask, TimeSpan.FromSeconds(30));
        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        coordinator.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
