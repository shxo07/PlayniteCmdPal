namespace PlayniteCmdPal.Core;

/// <summary>
/// Reuses UI/domain row objects by stable key and drops entries whose keys are no longer
/// present in the authoritative snapshot. Callers are responsible for synchronizing access.
/// </summary>
public sealed class KeyedItemCache<T> where T : class
{
    private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);

    public T GetOrAdd(string key, Func<T> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(create);

        if (_items.TryGetValue(key, out var existing))
            return existing;

        var item = create() ?? throw new InvalidOperationException("The item factory returned null.");
        _items.Add(key, item);
        return item;
    }

    public void RetainKeys(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var retained = keys.ToHashSet(StringComparer.Ordinal);
        foreach (var key in _items.Keys.Where(key => !retained.Contains(key)).ToArray())
            _items.Remove(key);
    }

    public int Count => _items.Count;
}
