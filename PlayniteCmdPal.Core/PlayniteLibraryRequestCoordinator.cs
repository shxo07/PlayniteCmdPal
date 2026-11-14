namespace PlayniteCmdPal.Core;

/// <summary>A cancellation/version lease for one unified-library page request.</summary>
public sealed record PlayniteLibraryRequest(int Version, CancellationToken CancellationToken);

/// <summary>
/// Ensures only the newest page request may publish catalog results or artwork. Starting
/// a request cancels the previous lease; disposal invalidates and cancels the final lease.
/// </summary>
public sealed class PlayniteLibraryRequestCoordinator : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _current;
    private int _version;
    private bool _disposed;

    public PlayniteLibraryRequest BeginRequest()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _current?.Cancel();
            _current?.Dispose();
            _current = new CancellationTokenSource();
            return new PlayniteLibraryRequest(++_version, _current.Token);
        }
    }

    public bool IsCurrent(int version)
    {
        lock (_gate)
            return !_disposed && version == _version;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _version++;
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }
}
