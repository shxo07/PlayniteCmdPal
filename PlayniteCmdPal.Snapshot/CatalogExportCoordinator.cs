namespace PlayniteCmdPal.Snapshot;

/// <summary>
/// Coalesces refresh requests and runs at most one export at a time. Requests arriving during
/// an export cause one follow-up export, so the final database state is not lost.
/// </summary>
public sealed class CatalogExportCoordinator : IDisposable
{
    private readonly Func<CancellationToken, Task> _exportAsync;
    private readonly TimeSpan _debounce;
    private readonly Action<Exception> _onError;
    private readonly CancellationTokenSource _disposal = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _gate = new();
    private bool _requested;
    private bool _running;
    private bool _stopped;
    private bool _disposed;
    private bool _cancelCompleted;

    public CatalogExportCoordinator(Func<CancellationToken, Task> exportAsync, TimeSpan debounce, Action<Exception>? onError = null)
    {
        if (exportAsync is null) throw new ArgumentNullException(nameof(exportAsync));
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }

        _exportAsync = exportAsync;
        _debounce = debounce;
        _onError = onError ?? (_ => { });
    }

    public void RequestExport()
    {
        lock (_gate)
        {
            if (_disposed || _stopped)
            {
                return;
            }

            if (_requested)
            {
                return;
            }

            _requested = true;
            _signal.Release();
        }
    }

    public async Task RunAsync()
    {
        lock (_gate)
        {
            if (_running || _stopped || _disposed)
            {
                throw new InvalidOperationException("The export coordinator is already running, stopped, or disposed.");
            }

            _running = true;
        }

        try
        {
            while (true)
            {
                await _signal.WaitAsync(_disposal.Token).ConfigureAwait(false);
                if (_debounce > TimeSpan.Zero)
                {
                    await Task.Delay(_debounce, _disposal.Token).ConfigureAwait(false);
                }

                lock (_gate)
                {
                    _requested = false;
                }

                try
                {
                    await _exportAsync(_disposal.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_disposal.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _onError(exception);
                }
            }
        }
        catch (OperationCanceledException) when (_disposal.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                _running = false;
                _stopped = true;
                _signal.Dispose();
                if (_cancelCompleted)
                {
                    _disposal.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        try
        {
            _disposal.Cancel();
        }
        finally
        {
            lock (_gate)
            {
                _cancelCompleted = true;
                if (!_running)
                {
                    _signal.Dispose();
                    _disposal.Dispose();
                }
            }
        }
    }
}
