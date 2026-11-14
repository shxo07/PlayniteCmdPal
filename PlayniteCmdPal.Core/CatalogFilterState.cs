namespace PlayniteCmdPal.Core;

/// <summary>Persistable installed/source/platform filter selections.</summary>
public sealed record CatalogFilterSelection(
    bool[] InstalledStatuses,
    string[] Sources,
    string[] Platforms);

/// <summary>
/// Tracks available catalog metadata and independent multi-select filter selections.
/// A fresh state defaults to installed games; restored or cleared states remain unrestricted
/// or retain the user's explicit choice.
/// </summary>
public sealed class CatalogFilterState
{
    private string[] _sources = [];
    private string[] _platforms = [];
    private readonly HashSet<bool> _installedStatuses = [true];
    private readonly HashSet<string> _selectedSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPlatforms = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? FilterOptionsChanged;
    public event EventHandler? FilterSelectionsChanged;

    public IReadOnlyList<string> Sources => _sources;
    public IReadOnlyList<string> Platforms => _platforms;
    public IReadOnlyCollection<bool> InstalledStatuses => _installedStatuses;
    public IReadOnlyCollection<string> SelectedSources => _selectedSources.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyCollection<string> SelectedPlatforms => _selectedPlatforms.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    public int ActiveFilterCount =>
        (_installedStatuses.Count == 1 ? 1 : 0) + _selectedSources.Count + _selectedPlatforms.Count;

    public CatalogFilterSelection CaptureSelections() => new(
        _installedStatuses.OrderBy(value => value).ToArray(),
        SelectedSources.ToArray(),
        SelectedPlatforms.ToArray());

    /// <summary>
    /// Restores selections before catalog metadata is available. UpdateCatalog subsequently
    /// canonicalizes them against the live catalog and drops values that no longer exist.
    /// </summary>
    public void RestoreSelections(CatalogFilterSelection? selection)
    {
        if (selection is null)
            return;

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

    public bool UpdateCatalog(IEnumerable<GameEntry> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        var entries = games.ToArray();
        var sources = GetValues(entries, game => game.Source);
        var platforms = GetValues(entries, game => game.Platform);
        var optionsChanged =
            !_sources.SequenceEqual(sources, StringComparer.Ordinal) ||
            !_platforms.SequenceEqual(platforms, StringComparer.Ordinal);

        _sources = sources;
        _platforms = platforms;

        var selectionsChanged =
            RetainAvailable(_selectedSources, _sources) |
            RetainAvailable(_selectedPlatforms, _platforms);

        if (optionsChanged || selectionsChanged)
            FilterOptionsChanged?.Invoke(this, EventArgs.Empty);
        if (selectionsChanged)
            FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);

        return optionsChanged || selectionsChanged;
    }

    public void SetInstalledStatuses(IEnumerable<bool>? statuses)
    {
        var next = statuses?.ToHashSet() ?? [];
        if (_installedStatuses.SetEquals(next))
            return;

        _installedStatuses.Clear();
        _installedStatuses.UnionWith(next);
        FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSources(IEnumerable<string>? sources) =>
        SetValues(_selectedSources, sources, _sources);

    public void SetPlatforms(IEnumerable<string>? platforms) =>
        SetValues(_selectedPlatforms, platforms, _platforms);

    public void ClearInstalledStatuses() => SetInstalledStatuses([]);
    public void ClearSources() => SetSources([]);
    public void ClearPlatforms() => SetPlatforms([]);

    public void ClearAll()
    {
        var changed = _installedStatuses.Count > 0 || _selectedSources.Count > 0 || _selectedPlatforms.Count > 0;
        _installedStatuses.Clear();
        _selectedSources.Clear();
        _selectedPlatforms.Clear();
        if (changed)
            FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public GameSearchFilters ToSearchFilters()
    {
        bool? installed = _installedStatuses.Count == 1 ? _installedStatuses.Single() : null;
        return new GameSearchFilters(
            IsInstalled: installed,
            Sources: _selectedSources.Count == 0 ? null : SelectedSources,
            Platforms: _selectedPlatforms.Count == 0 ? null : SelectedPlatforms);
    }

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

    private void SetValues(HashSet<string> target, IEnumerable<string>? values, IReadOnlyCollection<string> available)
    {
        var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values is not null)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                var canonical = available.FirstOrDefault(option =>
                    string.Equals(option, value.Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonical is not null)
                    next.Add(canonical);
            }
        }

        if (target.SetEquals(next))
            return;
        target.Clear();
        target.UnionWith(next);
        FilterSelectionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
