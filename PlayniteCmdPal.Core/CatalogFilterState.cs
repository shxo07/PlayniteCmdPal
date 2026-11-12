namespace PlayniteCmdPal.Core;

/// <summary>
/// Tracks available catalog metadata and independent multi-select filter selections.
/// A fresh state defaults to installed games; cleared states remain unrestricted.
/// </summary>
public sealed class CatalogFilterState
{
    private string[] _sources = [];
    private string[] _platforms = [];
    private readonly HashSet<bool> _installedStatuses = [true];
    private readonly HashSet<string> _selectedSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPlatforms = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? FilterOptionsChanged;

    public IReadOnlyList<string> Sources => _sources;
    public IReadOnlyList<string> Platforms => _platforms;
    public IReadOnlyCollection<bool> InstalledStatuses => _installedStatuses;
    public IReadOnlyCollection<string> SelectedSources => _selectedSources.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyCollection<string> SelectedPlatforms => _selectedPlatforms.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Number of active restrictions; selecting both status values is unrestricted.</summary>
    public int ActiveFilterCount =>
        (_installedStatuses.Count == 1 ? 1 : 0) + _selectedSources.Count + _selectedPlatforms.Count;

    /// <summary>
    /// Replaces available metadata options, preserving valid selections and dropping stale
    /// source/platform values. Call only for an available catalog; transient catalog failures
    /// must not be treated as an empty catalog.
    /// </summary>
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
        {
            FilterOptionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return optionsChanged || selectionsChanged;
    }

    public void SetInstalledStatuses(IEnumerable<bool>? statuses)
    {
        _installedStatuses.Clear();
        if (statuses is not null)
        {
            foreach (var status in statuses)
            {
                _installedStatuses.Add(status);
            }
        }
    }

    public void SetSources(IEnumerable<string>? sources) => SetValues(_selectedSources, sources, _sources);
    public void SetPlatforms(IEnumerable<string>? platforms) => SetValues(_selectedPlatforms, platforms, _platforms);
    public void ClearInstalledStatuses() => _installedStatuses.Clear();
    public void ClearSources() => _selectedSources.Clear();
    public void ClearPlatforms() => _selectedPlatforms.Clear();

    public void ClearAll()
    {
        _installedStatuses.Clear();
        _selectedSources.Clear();
        _selectedPlatforms.Clear();
    }

    public GameSearchFilters ToSearchFilters()
    {
        // Neither status is unrestricted. Both are also unrestricted, which intentionally
        // preserves games whose installation metadata is unknown.
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
        {
            selected.Remove(value);
        }

        return stale.Length > 0;
    }

    private static void SetValues(HashSet<string> target, IEnumerable<string>? values, IReadOnlyCollection<string> available)
    {
        target.Clear();
        if (values is null)
        {
            return;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var canonical = available.FirstOrDefault(option =>
                string.Equals(option, value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
            {
                target.Add(canonical);
            }
        }
    }
}
