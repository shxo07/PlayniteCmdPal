namespace PlayniteCmdPal.Core;

/// <summary>
/// Serializes request creation and result publication so an older asynchronous request
/// cannot overwrite the result of a newer request.
/// </summary>
public sealed class LatestRequestGate
{
    private readonly object _gate = new();
    private int _version;

    public (int Version, T State) BeginRequest<T>(Func<T> captureState)
    {
        ArgumentNullException.ThrowIfNull(captureState);
        lock (_gate)
        {
            var version = ++_version;
            return (version, captureState());
        }
    }

    public T Synchronize<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            return action();
        }
    }

    public bool TryPublish(int version, Action publish)
    {
        ArgumentNullException.ThrowIfNull(publish);
        lock (_gate)
        {
            if (version != _version)
            {
                return false;
            }

            publish();
            return true;
        }
    }
}
