namespace PlayniteCmdPal.Core;

/// <summary>Persistable installed/source/platform filter selections.</summary>
public sealed record CatalogFilterSelection(
    bool[] InstalledStatuses,
    string[] Sources,
    string[] Platforms);

/// <summary>
/// Tracks available catalog metadata and independent multi-select filter selections.
/// All mutable state is protected by a private lock. Public collection properties return
/// snapshots, and notifications are always raised after releasing the lock.
/// </summary>
public sealed class CatalogFilterState
{
    private readonly object _gate = new();
    private string[] _sources = [];
    private string[] _platforms = [];
    private readonly HashSet<bool> _installedStatuses = [true];
    private readonly HashSet<string> _selectedSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPlatforms = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? FilterOptionsChanged;
    public event EventHandler? FilterSelectionsChanged;

    public IReadOnlyList<string> Sources
    {
        get { lock (_gate) return _sources.ToArray(); }
    }

    public IReadOnlyList<string> Platforms
    {
        get { lock (_gate) return _platforms.ToArray(); }
    }

    public IReadOnlyCollection<bool> InstalledStatuses
    {
        get { lock (_gate) return _installedStatuses.OrderBy(value => value).ToArray(); }
    }

    public IReadOnlyCollection<string> SelectedSources
    {
        get { lock (_gate) return Sort(_selectedSources); }
    }

    public IReadOnlyCollection<string> SelectedPlatforms
    {
        get { lock (_gate) return Sort(_selectedPlatforms); }
    }

    public int ActiveFilterCount
    {
        get
        {
            lock (_gate)
            {
                return (_installedStatuses.Count == 1 ? 1 : 0)
                    + _selectedSources.Count
                    + _selectedPlatforms.Count;
            }
        }
    }

    public CatalogFilterSelection CaptureSelections()
    {
        lock (_gate)
        {
            return new CatalogFilterSelection(
                _installedStatuses.OrderBy(value => value).ToArray(),
                Sort(_selectedSources),
                Sort(_selectedPlatforms));
        }
    }

    /// <summary>
    /// Restores selections before catalog metadata is available. UpdateCatalog subsequently
    /// canonicalizes them against the live catalog and drops values that no longer exist.
    /// </summary>
    public void RestoreSelections(CatalogFilterSelection? selection)
    {
        if (selection is null)
            return;

        lock (_gate)
        {
            _installedStatuses.Clear();
            foreach (var status in selection.InstalledStatuses ?? [])
                _installedStatuses.Add(status);

            _selectedSources.Clear();
            foreach (var source in selection.Sources ?? [])
            {
                if (!string.IsNullOrWhiteSpace(source))
                    _selectedSources.Add(source.Trim());
            }

            _selectedPlatforms.Clear();
            foreach (var platform in selection.Platforms ?? [])
            {
                if (!string.IsNullOrWhiteSpace(platform))
                    _selectedPlatforms.Add(platform.Trim());
            }
        }
    }

    public bool UpdateCatalog(IEnumerable<GameEntry> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        // Enumerate caller-owned input before entering the state lock.
        var entries = games.ToArray();
        var sources = GetValues(entries, game => game.Source);
        var platforms = GetValues(entries, game => game.Platform);
        bool optionsChanged;
        bool selectionsChanged;

        lock (_gate)
        {
            optionsChanged =
                !_sources.SequenceEqual(sources, StringComparer.Ordinal) ||
                !_platforms.SequenceEqual(platforms, StringComparer.Ordinal);

            _sources = sources;
            _platforms = platforms;

            selectionsChanged =
                RetainAvailable(_selectedSources, _sources) |
                RetainAvailable(_selectedPlatforms, _platforms);
        }

        // Never call external subscribers while holding _gate: subscribers may refresh
        // pages, persist preferences, or read state and must not participate in lock order.
        if (optionsChanged || selectionsChanged)
            FilterOptionsChanged?.Invoke(this, EventArgs.Empty);
        if (selectionsChanged)
            FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);

        return optionsChanged || selectionsChanged;
    }

    public void SetInstalledStatuses(IEnumerable<bool>? statuses)
    {
        var next = statuses?.ToHashSet() ?? [];
        bool changed;
        lock (_gate)
        {
            changed = !_installedStatuses.SetEquals(next);
            if (changed)
            {
                _installedStatuses.Clear();
                _installedStatuses.UnionWith(next);
            }
        }

        if (changed)
            FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSources(IEnumerable<string>? sources) =>
        SetValues(_selectedSources, sources, isSource: true);

    public void SetPlatforms(IEnumerable<string>? platforms) =>
        SetValues(_selectedPlatforms, platforms, isSource: false);

    public void ClearInstalledStatuses() => SetInstalledStatuses([]);
    public void ClearSources() => SetSources([]);
    public void ClearPlatforms() => SetPlatforms([]);

    public void ClearAll()
    {
        bool changed;
        lock (_gate)
        {
            changed = _installedStatuses.Count > 0
                || _selectedSources.Count > 0
                || _selectedPlatforms.Count > 0;
            _installedStatuses.Clear();
            _selectedSources.Clear();
            _selectedPlatforms.Clear();
        }

        if (changed)
            FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public GameSearchFilters ToSearchFilters()
    {
        lock (_gate)
        {
            bool? installed = _installedStatuses.Count == 1 ? _installedStatuses.Single() : null;
            return new GameSearchFilters(
                IsInstalled: installed,
                Sources: _selectedSources.Count == 0 ? null : Sort(_selectedSources),
                Platforms: _selectedPlatforms.Count == 0 ? null : Sort(_selectedPlatforms));
        }
    }

    private static string[] Sort(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] GetValues(IEnumerable<GameEntry> games, Func<GameEntry, string?> selector) =>
        games.Select(game => selector(game)?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static bool RetainAvailable(HashSet<string> selected, IReadOnlyCollection<string> available)
    {
        var stale = selected.Where(value => !available.Contains(value, StringComparer.OrdinalIgnoreCase)).ToArray();
        foreach (var value in stale)
            selected.Remove(value);
        return stale.Length > 0;
    }

    private void SetValues(HashSet<string> target, IEnumerable<string>? values, bool isSource)
    {
        // Materialize caller input before taking the lock; it may be a lazy enumerable
        // backed by UI or application state that itself reads this object.
        var requested = values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).ToArray() ?? [];
        bool changed;

        lock (_gate)
        {
            var available = isSource ? _sources : _platforms;
            var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in requested)
            {
                var canonical = available.FirstOrDefault(option =>
                    string.Equals(option, value, StringComparison.OrdinalIgnoreCase));
                if (canonical is not null)
                    next.Add(canonical);
            }

            changed = !target.SetEquals(next);
            if (changed)
            {
                target.Clear();
                target.UnionWith(next);
            }
        }

        if (changed)
            FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
