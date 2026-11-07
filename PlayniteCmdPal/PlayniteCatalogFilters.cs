using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

/// <summary>
/// Exposes independent, keyboard-selectable catalog filters. CmdPal keeps the active filter IDs;
/// the page translates them into the SDK-independent search filter model.
/// </summary>
internal sealed partial class PlayniteCatalogFilters : Filters
{
    private readonly CatalogFilterState _state = new();

    public PlayniteCatalogFilters()
    {
        // Start unfiltered; users can explicitly choose a status or metadata filter.
        CurrentFilterId = CatalogFilterState.AllGamesId;
    }

    public override IFilterItem[] GetFilters()
    {
        var filters = new List<IFilterItem>
        {
            new Filter { Id = CatalogFilterState.AllGamesId, Name = "All games" },
            new Separator(),
            new Filter { Id = CatalogFilterState.InstalledId, Name = "Installed" },
            new Filter { Id = CatalogFilterState.NotInstalledId, Name = "Not installed" },
        };

        if (_state.Sources.Count > 0)
        {
            filters.Add(new Separator());
            filters.AddRange(_state.Sources.Select(source => (IFilterItem)new Filter
            {
                Id = Encode("source", source),
                Name = $"Source: {source}",
            }));
        }

        if (_state.Platforms.Count > 0)
        {
            filters.Add(new Separator());
            filters.AddRange(_state.Platforms.Select(platform => (IFilterItem)new Filter
            {
                Id = Encode("platform", platform),
                Name = $"Platform: {platform}",
            }));
        }

        return filters.ToArray();
    }

    /// <summary>
    /// Updates the options and selection from the latest catalog. Returns true when the
    /// host must re-read the page's Filters property to refresh its dropdown options.
    /// </summary>
    public bool UpdateCatalog(IEnumerable<GameEntry> games)
    {
        _state.SelectFilter(CurrentFilterId);
        var optionsChanged = _state.UpdateCatalog(games);

        if (!string.Equals(CurrentFilterId, _state.CurrentFilterId, StringComparison.Ordinal))
        {
            CurrentFilterId = _state.CurrentFilterId;
        }

        return optionsChanged;
    }

    public GameSearchFilters ToSearchFilters()
    {
        _state.SelectFilter(CurrentFilterId);
        return _state.ToSearchFilters();
    }

    private static string Encode(string dimension, string value) =>
        $"{dimension}:{Uri.EscapeDataString(value)}";
}
