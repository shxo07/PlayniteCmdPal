using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteCmdPal.Core;

/// <summary>A host-visible option in the native catalog filter dropdown.</summary>
public sealed record CatalogFilterOption(string? Id, string? Name, bool IsSeparator = false);

/// <summary>
/// Keeps the available metadata filter options and selected native filter in sync with a catalog.
/// The Command Palette SDK supports one selected filter ID at a time.
/// </summary>
public sealed class CatalogFilterState
{
    public const string AllGamesId = "all-games";
    public const string InstalledId = "installed";
    public const string NotInstalledId = "not-installed";

    /// <summary>Raised after the visible dropdown options change and must be re-read by the host.</summary>
    public event EventHandler? FilterOptionsChanged;

    private string[] _sources = [];
    private string[] _platforms = [];

    public string CurrentFilterId { get; private set; } = AllGamesId;

    public IReadOnlyList<string> Sources => _sources;

    public IReadOnlyList<string> Platforms => _platforms;

    /// <summary>
    /// Replaces the available source/platform options and keeps the selected filter valid.
    /// Returns true only when the visible option lists changed.
    /// </summary>
    public bool UpdateCatalog(IEnumerable<GameEntry> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        var entries = games.ToArray();
        var sources = GetValues(entries, game => game.Source);
        var platforms = GetValues(entries, game => game.Platform);
        var optionsChanged =
            !_sources.SequenceEqual(sources, StringComparer.Ordinal)
            || !_platforms.SequenceEqual(platforms, StringComparer.Ordinal);

        _sources = sources;
        _platforms = platforms;
        CurrentFilterId = NormalizeSelection(CurrentFilterId);

        if (optionsChanged)
        {
            FilterOptionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return optionsChanged;
    }

    /// <summary>Returns a stable snapshot of the options currently exposed to the host.</summary>
    public IReadOnlyList<CatalogFilterOption> GetOptions()
    {
        var options = new List<CatalogFilterOption>
        {
            new(AllGamesId, "All games"),
            new(null, null, IsSeparator: true),
            new(InstalledId, "Installed"),
            new(NotInstalledId, "Not installed"),
        };

        if (_sources.Length > 0)
        {
            options.Add(new CatalogFilterOption(null, null, IsSeparator: true));
            options.AddRange(_sources.Select(source => new CatalogFilterOption(
                Encode("source", source), $"Source: {source}")));
        }

        if (_platforms.Length > 0)
        {
            options.Add(new CatalogFilterOption(null, null, IsSeparator: true));
            options.AddRange(_platforms.Select(platform => new CatalogFilterOption(
                Encode("platform", platform), $"Platform: {platform}")));
        }

        return options;
    }

    /// <summary>Applies a filter ID selected by the native Command Palette host.</summary>
    public void SelectFilter(string? filterId)
    {
        CurrentFilterId = NormalizeSelection(filterId);
    }

    public GameSearchFilters ToSearchFilters()
    {
        if (CurrentFilterId == InstalledId)
        {
            return new GameSearchFilters(IsInstalled: true);
        }

        if (CurrentFilterId == NotInstalledId)
        {
            return new GameSearchFilters(IsInstalled: false);
        }

        if (TryGetValue(CurrentFilterId, "source:", out var source))
        {
            return new GameSearchFilters(Sources: new[] { source });
        }

        if (TryGetValue(CurrentFilterId, "platform:", out var platform))
        {
            return new GameSearchFilters(Platforms: new[] { platform });
        }

        return new GameSearchFilters();
    }

    private string NormalizeSelection(string? filterId)
    {
        if (string.Equals(filterId, AllGamesId, StringComparison.Ordinal)
            || string.Equals(filterId, InstalledId, StringComparison.Ordinal)
            || string.Equals(filterId, NotInstalledId, StringComparison.Ordinal))
        {
            return filterId!;
        }

        if (TryGetValue(filterId, "source:", out var selectedSource))
        {
            var currentSource = _sources.FirstOrDefault(
                value => string.Equals(value, selectedSource, StringComparison.OrdinalIgnoreCase));
            return currentSource is null ? AllGamesId : Encode("source", currentSource);
        }

        if (TryGetValue(filterId, "platform:", out var selectedPlatform))
        {
            var currentPlatform = _platforms.FirstOrDefault(
                value => string.Equals(value, selectedPlatform, StringComparison.OrdinalIgnoreCase));
            return currentPlatform is null ? AllGamesId : Encode("platform", currentPlatform);
        }

        return AllGamesId;
    }

    private static string[] GetValues(
        IEnumerable<GameEntry> games,
        Func<GameEntry, string?> selector) =>
        games
            .Select(game => selector(game)?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string Encode(string dimension, string value) =>
        $"{dimension}:{Uri.EscapeDataString(value)}";

    private static bool TryGetValue(string? filterId, string prefix, out string value)
    {
        if (filterId is not null && filterId.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = Uri.UnescapeDataString(filterId[prefix.Length..]);
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }
}
