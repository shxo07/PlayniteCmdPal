using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

/// <summary>
/// Keyboard-first filter picker. Each option is a normal Command Palette list item,
/// so the host owns arrow-key navigation and Enter invokes the toggle command.
/// </summary>
internal sealed partial class FiltersPage : DynamicListPage
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersChanged;
    private IListItem[] _items = [];

    public FiltersPage(CatalogFilterState state, Action filtersChanged)
    {
        _state = state;
        _filtersChanged = filtersChanged;

        Name = "Filters";
        Title = "Filters";
        RebuildItems(notify: false);
    }

    public override IListItem[] GetItems() => _items;

    private void RebuildItems(bool notify = true)
    {
        var items = new List<IListItem>
        {
            new ListItem(new NoOpCommand())
            {
                Title = "Installed status",
                Subtitle = "↑/↓ move · Enter toggles · Esc returns",
            },
            CreateOption("Installed", "status", "installed", _state.InstalledStatuses.Contains(true)),
            CreateOption("Not installed", "status", "not-installed", _state.InstalledStatuses.Contains(false)),
            new ListItem(new NoOpCommand()) { Title = "Source" },
        };

        foreach (var source in _state.Sources)
        {
            items.Add(CreateOption(source, "source", source, _state.SelectedSources.Contains(source, StringComparer.OrdinalIgnoreCase)));
        }

        items.Add(new ListItem(new NoOpCommand()) { Title = "Platform" });
        foreach (var platform in _state.Platforms)
        {
            items.Add(CreateOption(platform, "platform", platform, _state.SelectedPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase)));
        }

        items.Add(new ListItem(new ClearFiltersCommand(this, "status"))
        {
            Title = "Clear installed status",
            Subtitle = "Keep source and platform selections",
        });
        items.Add(new ListItem(new ClearFiltersCommand(this, "sources"))
        {
            Title = "Clear sources",
            Subtitle = "Keep installed status and platform selections",
        });
        items.Add(new ListItem(new ClearFiltersCommand(this, "platforms"))
        {
            Title = "Clear platforms",
            Subtitle = "Keep installed status and source selections",
        });
        items.Add(new ListItem(new ClearFiltersCommand(this, "all"))
        {
            Title = "Clear all filters",
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

            default:
                return;
        }

        RebuildItems(notify: false);
        _ = NotifyFiltersChangedAsync();
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

        RebuildItems();
        _ = NotifyFiltersChangedAsync();
    }

    private async Task NotifyFiltersChangedAsync()
    {
        // Defer the parent search refresh until the current command invocation has
        // returned, avoiding nested Command Palette notifications during dispatch.
        await Task.Delay(50);
        RaiseItemsChanged(_items.Length);
        _filtersChanged();
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
