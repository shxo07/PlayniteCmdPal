using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

/// <summary>
/// Lifecycle and failure-isolation tests for the debounced export coordinator. The delegate is a
/// synthetic stand-in for a Playnite export, so no Playnite installation is required.
/// </summary>
public sealed class CatalogExportCoordinatorLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ConstructorRejectsMissingDelegateOrNegativeDebounce()
    {
        Assert.Equal("exportAsync", Assert.Throws<ArgumentNullException>(() => new CatalogExportCoordinator(null!, TimeSpan.Zero)).ParamName);
        Assert.Equal("debounce", Assert.Throws<ArgumentOutOfRangeException>(() => new CatalogExportCoordinator(_ => Task.CompletedTask, TimeSpan.FromMilliseconds(-1))).ParamName);
    }

    [Fact]
    public async Task ZeroDebounceStillPerformsTheRequestedExport()
    {
        var exported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new CatalogExportCoordinator(_ =>
        {
            exported.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.Zero);

        var run = coordinator.RunAsync();
        coordinator.RequestExport();

        await exported.Task.WaitAsync(Timeout);
        coordinator.Dispose();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task RequestMadeBeforeTheLoopStartsIsHonored()
    {
        var exported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new CatalogExportCoordinator(_ =>
        {
            exported.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(10));

        coordinator.RequestExport();
        var run = coordinator.RunAsync();

        await exported.Task.WaitAsync(Timeout);
        coordinator.Dispose();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ExportsNeverOverlapEvenForFollowUpRequests()
    {
        var active = 0;
        var maximumConcurrency = 0;
        var exports = 0;
        var gate = new object();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstExport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoExports = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondExportFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new CatalogExportCoordinator(async _ =>
        {
            int exportNumber;
            lock (gate)
            {
                maximumConcurrency = Math.Max(maximumConcurrency, Interlocked.Increment(ref active));
                exportNumber = ++exports;
                if (exportNumber == 2)
                {
                    twoExports.TrySetResult();
                }
            }

            try
            {
                if (exportNumber == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirstExport.Task;
                }
                else
                {
                    secondExportFinished.TrySetResult();
                }
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }, TimeSpan.FromMilliseconds(1));

        var run = coordinator.RunAsync();
        coordinator.RequestExport();

        // Issue the follow-up burst while the first export is held open.
        await firstStarted.Task.WaitAsync(Timeout);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => coordinator.RequestExport())));
        releaseFirstExport.TrySetResult();

        await twoExports.Task.WaitAsync(Timeout);
        await secondExportFinished.Task.WaitAsync(Timeout);

        // Dispose, then wait for the loop to exit before asserting. The loop cannot start another
        // export once RunAsync has completed, so "no third export" is proven rather than assumed
        // by sleeping and hoping the extra export would have appeared by then.
        coordinator.Dispose();
        await run.WaitAsync(Timeout);

        Assert.Equal(1, Volatile.Read(ref maximumConcurrency));
        Assert.Equal(2, Volatile.Read(ref exports));
    }

    [Fact]
    public async Task RequestsInsideTheDebounceWindowCollapseIntoOneExport()
    {
        var exports = 0;
        var exported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // A debounce window is time-based behavior, so it needs a real delay: no export can start
        // until the window elapses, which is what makes every request below land inside the same
        // window. The delay is the behavior under test; it is not used to establish an assertion.
        using var coordinator = new CatalogExportCoordinator(_ =>
        {
            Interlocked.Increment(ref exports);
            exported.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(200));

        var run = coordinator.RunAsync();
        for (var index = 0; index < 10; index++)
        {
            coordinator.RequestExport();
        }

        await exported.Task.WaitAsync(Timeout);

        // Let the loop exit before reading the count. The assertion below is "no second export was
        // started", which is observable once the loop has stopped; the earlier behaviour that
        // would have produced a second export did so before this export completed.
        coordinator.Dispose();
        await run.WaitAsync(Timeout);

        Assert.Equal(1, Volatile.Read(ref exports));
    }

    [Fact]
    public async Task RequestsDuringAnExportScheduleExactlyOneFollowUp()
    {
        var exports = 0;
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstExport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondExportFinished = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new CatalogExportCoordinator(async _ =>
        {
            // Count first, then signal: a signal sent before the increment could let the test
            // observe a stale count.
            var exportNumber = Interlocked.Increment(ref exports);
            if (exportNumber == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirstExport.Task;
                return;
            }

            secondExportFinished.TrySetResult(exportNumber);
        }, TimeSpan.FromMilliseconds(1));

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        await firstStarted.Task.WaitAsync(Timeout);

        // Hold the first export open until every extra request has been issued, so they are all
        // guaranteed to arrive while it is running.
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => coordinator.RequestExport())));
        releaseFirstExport.TrySetResult();

        // Exactly one follow-up is scheduled for requests made during an export, so the follow-up
        // is export number 2. Asserting the value it reports (rather than re-reading a live field
        // after a sleep) keeps this deterministic; the count is then confirmed after the loop has
        // exited, where no further export can begin.
        var followUpExportNumber = await secondExportFinished.Task.WaitAsync(Timeout);
        Assert.Equal(2, followUpExportNumber);

        coordinator.Dispose();
        await run.WaitAsync(Timeout);

        Assert.Equal(2, Volatile.Read(ref exports));
    }

    [Fact]
    public async Task ErrorsAreReportedAndNeverEscapeTheLoop()
    {
        var reported = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var firstErrorReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondExportSucceeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var successes = 0;
        using var coordinator = new CatalogExportCoordinator(
            async _ =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    await Task.Yield();
                    throw new InvalidOperationException("synthetic export failure");
                }

                // Increment before signalling, so the waited-on signal cannot expose a stale value.
                Interlocked.Increment(ref successes);
                secondExportSucceeded.TrySetResult();
            },
            TimeSpan.FromMilliseconds(10),
            exception =>
            {
                reported.Enqueue(exception);
                firstErrorReported.TrySetResult();
            });

        var run = coordinator.RunAsync();
        coordinator.RequestExport();

        // Signal from inside the callback, not from the delegate: the throw happens before the
        // coordinator catches it, so a signal sent from the delegate could be observed while the
        // queue is still empty.
        await firstErrorReported.Task.WaitAsync(Timeout);
        var firstError = Assert.Single(reported);
        Assert.Equal("synthetic export failure", firstError.Message);
        Assert.Equal(0, Volatile.Read(ref successes));

        coordinator.RequestExport();
        await secondExportSucceeded.Task.WaitAsync(Timeout);
        coordinator.Dispose();
        await run.WaitAsync(Timeout);

        Assert.Equal(1, Volatile.Read(ref successes));
        Assert.Single(reported);
    }

    [Fact]
    public async Task ADelegateThatReturnsAFaultedTaskIsTreatedAsAFailedExport()
    {
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterExport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        using var coordinator = new CatalogExportCoordinator(_ =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                return Task.FromException(new IOException("synthetic failure"));
            }

            laterExport.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(10), exception => reported.TrySetResult(exception));

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        var error = await reported.Task.WaitAsync(Timeout);
        Assert.IsAssignableFrom<IOException>(error);

        coordinator.RequestExport();
        await laterExport.Task.WaitAsync(Timeout);
        coordinator.Dispose();
        await run.WaitAsync(Timeout);
        Assert.Equal(2, Volatile.Read(ref attempts));
    }

    [Fact]
    public async Task DisposalCancelsTheTokenHandedToTheExport()
    {
        using var exportStarted = new SemaphoreSlim(0);
        var observedCancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new CatalogExportCoordinator(async token =>
        {
            exportStarted.Release();
            try
            {
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
                observedCancellation.TrySetResult(false);
            }
            catch (OperationCanceledException)
            {
                observedCancellation.TrySetResult(true);
            }
        }, TimeSpan.Zero);

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        Assert.True(await exportStarted.WaitAsync(Timeout));

        coordinator.Dispose();

        Assert.True(await observedCancellation.Task.WaitAsync(Timeout));
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task RunAsyncCanOnlyBeStartedOnce()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new CatalogExportCoordinator(_ => release.Task, TimeSpan.Zero);

        var run = coordinator.RunAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync());
        release.TrySetResult();
        coordinator.Dispose();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task StopAfterDisposalIsNotFatalAndIgnoresLaterRequests()
    {
        var exports = 0;
        var coordinator = new CatalogExportCoordinator(_ =>
        {
            Interlocked.Increment(ref exports);
            return Task.CompletedTask;
        }, TimeSpan.Zero);

        var run = coordinator.RunAsync();
        coordinator.Dispose();
        coordinator.Dispose(); // Idempotent.

        await run.WaitAsync(Timeout);
        coordinator.RequestExport(); // Ignored: no exception, no export.
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync());
        Assert.Equal(0, Volatile.Read(ref exports));
    }

    [Fact]
    public async Task DisposeWithoutEverRunningIsSafe()
    {
        var coordinator = new CatalogExportCoordinator(_ => Task.CompletedTask, TimeSpan.FromSeconds(1));

        coordinator.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync());
    }

    [Fact]
    public async Task DisposeWhileWaitingForTheFirstRequestCompletesTheLoop()
    {
        var coordinator = new CatalogExportCoordinator(_ => Task.CompletedTask, TimeSpan.Zero);
        var run = coordinator.RunAsync();

        coordinator.Dispose();

        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ErrorDelegateIsOptional()
    {
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new CatalogExportCoordinator(
            _ =>
            {
                attempted.TrySetResult();
                throw new IOException("synthetic failure");
            },
            TimeSpan.FromMilliseconds(5));

        var run = coordinator.RunAsync();
        coordinator.RequestExport();
        await attempted.Task.WaitAsync(Timeout);
        coordinator.Dispose();

        // Exiting without an onError callback must not fault the loop or the test.
        await run.WaitAsync(Timeout);
    }
}
