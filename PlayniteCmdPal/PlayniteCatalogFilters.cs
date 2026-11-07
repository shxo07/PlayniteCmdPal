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
    private bool _updatingCatalog;
    private bool _optionsChangedWhileUpdating;

    public event EventHandler? OptionsChanged;

    public PlayniteCatalogFilters()
    {
        // Start unfiltered; users can explicitly choose a status or metadata filter.
        CurrentFilterId = CatalogFilterState.AllGamesId;
        _state.FilterOptionsChanged += State_FilterOptionsChanged;
    }

    private void State_FilterOptionsChanged(object? sender, EventArgs args)
    {
        if (_updatingCatalog)
        {
            _optionsChangedWhileUpdating = true;
            return;
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public override IFilterItem[] GetFilters() => _state.GetOptions()
        .Select(option => option.IsSeparator
            ? (IFilterItem)new Separator()
            : new Filter { Id = option.Id!, Name = option.Name! })
        .ToArray();

    /// <summary>
    /// Updates the options and selection from the latest catalog. Returns true when the
    /// host must re-read the page's Filters property to refresh its dropdown options.
    /// </summary>
    public bool UpdateCatalog(IEnumerable<GameEntry> games)
    {
        _state.SelectFilter(CurrentFilterId);
        _updatingCatalog = true;
        bool optionsChanged;
        try
        {
            optionsChanged = _state.UpdateCatalog(games);
        }
        finally
        {
            _updatingCatalog = false;
        }

        if (!string.Equals(CurrentFilterId, _state.CurrentFilterId, StringComparison.Ordinal))
        {
            CurrentFilterId = _state.CurrentFilterId;
        }

        if (_optionsChangedWhileUpdating)
        {
            _optionsChangedWhileUpdating = false;
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return optionsChanged;
    }

    public GameSearchFilters ToSearchFilters()
    {
        _state.SelectFilter(CurrentFilterId);
        return _state.ToSearchFilters();
    }
 
}
