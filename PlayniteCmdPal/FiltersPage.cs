using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

/// <summary>
/// Keyboard-accessible filter picker. Each option and clear action is a native
/// Command Palette list item backed by an InvokableCommand.
/// </summary>
internal sealed partial class FiltersPage : DynamicListPage
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersChanged;
    private readonly SynchronizationContext? _uiContext;
    private IListItem[] _items = [];

    public FiltersPage(CatalogFilterState state, Action filtersChanged)
    {
        _state = state;
        _filtersChanged = filtersChanged;
        _uiContext = SynchronizationContext.Current;

        Name = "Filters";
        Title = "Filters";
        RebuildItems();
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // The filter picker is a fixed list, not a text-search page.
    }

    private void RebuildItems(bool notify = false)
    {
        var items = new List<IListItem>
        {
            CreateOption("Installed", "status", "installed", _state.InstalledStatuses.Contains(true)),
            CreateOption("Not installed", "status", "not-installed", _state.InstalledStatuses.Contains(false)),
        };

        foreach (var source in _state.Sources)
        {
            items.Add(CreateOption(source, "source", source,
                _state.SelectedSources.Contains(source, StringComparer.OrdinalIgnoreCase)));
        }

        foreach (var platform in _state.Platforms)
        {
            items.Add(CreateOption(platform, "platform", platform,
                _state.SelectedPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase)));
        }

        // A dedicated non-action row creates a clear visual break before the
        // clear controls without changing their order or appearance.
        items.Add(new ListItem(new NoOpCommand())
        {
            Title = "CLEAR FILTERS",
            Subtitle = " ",
        });

        items.Add(new ListItem(new ClearFiltersCommand(this, "status"))
        {
            Title = "Clear status",
            Subtitle = "Keep sources and platforms",
        });
        items.Add(new ListItem(new ClearFiltersCommand(this, "sources"))
        {
            Title = "Clear sources",
            Subtitle = "Keep installed status and platforms",
        });
        items.Add(new ListItem(new ClearFiltersCommand(this, "platforms"))
        {
            Title = "Clear platforms",
            Subtitle = "Keep installed status and sources",
        });
        items.Add(new ListItem(new ClearFiltersCommand(this, "all"))
        {
            Title = "Clear all",
            Subtitle = "Remove every filter",
        });

        _items = items.ToArray();
        if (notify)
        {
            RaiseItemsChanged(_items.Length);
        }
    }

    private ListItem CreateOption(string title, string dimension, string value, bool selected) =>
        new(new ToggleFilterCommand(this, dimension, value))
        {
            Title = $"{(selected ? "☑" : "☐")} {title}",
            Subtitle = dimension switch
            {
                "status" => "Installed status",
                "source" => "Source",
                "platform" => "Platform",
                _ => string.Empty,
            },
        };

    private void Toggle(string dimension, string value)
    {
        switch (dimension)
        {
            case "status":
                var statuses = _state.InstalledStatuses.ToHashSet();
                var status = value == "installed";
                if (!statuses.Add(status))
                {
                    statuses.Remove(status);
                }
                _state.SetInstalledStatuses(statuses);
                break;

            case "source":
                var sources = _state.SelectedSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!sources.Add(value))
                {
                    sources.Remove(value);
                }
                _state.SetSources(sources);
                break;

            case "platform":
                var platforms = _state.SelectedPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!platforms.Add(value))
                {
                    platforms.Remove(value);
                }
                _state.SetPlatforms(platforms);
                break;
        }

        ScheduleRefresh();
    }

    private void Clear(string dimension)
    {
        switch (dimension)
        {
            case "status":
                _state.ClearInstalledStatuses();
                break;
            case "sources":
                _state.ClearSources();
                break;
            case "platforms":
                _state.ClearPlatforms();
                break;
            case "all":
                _state.ClearAll();
                break;
            default:
                return;
        }

        ScheduleRefresh();
    }

    /// <summary>
    /// Do not raise ItemsChanged or refresh the parent page inside InvokableCommand.Invoke.
    /// The host is still dispatching the command at that point; a nested synchronous
    /// notification can swallow the activation. Defer both updates until Invoke returns.
    /// </summary>
    private async void ScheduleRefresh()
    {
        await Task.Delay(50);

        void Refresh()
        {
            RebuildItems();
            RaiseItemsChanged(_items.Length);
            _filtersChanged();
        }

        if (_uiContext is { } context &&
            !ReferenceEquals(context, SynchronizationContext.Current))
        {
            context.Post(_ => Refresh(), null);
        }
        else
        {
            Refresh();
        }
    }

    private sealed partial class ToggleFilterCommand : InvokableCommand
    {
        private readonly FiltersPage _page;
        private readonly string _dimension;
        private readonly string _value;

        public ToggleFilterCommand(FiltersPage page, string dimension, string value)
        {
            _page = page;
            _dimension = dimension;
            _value = value;
        }

        public override string Name => "Toggle filter";

        public override CommandResult Invoke()
        {
            _page.Toggle(_dimension, _value);
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class ClearFiltersCommand : InvokableCommand
    {
        private readonly FiltersPage _page;
        private readonly string _dimension;

        public ClearFiltersCommand(FiltersPage page, string dimension)
        {
            _page = page;
            _dimension = dimension;
        }

        public override string Name => "Clear filters";

        public override CommandResult Invoke()
        {
            _page.Clear(_dimension);
            return CommandResult.KeepOpen();
        }
    }
}
