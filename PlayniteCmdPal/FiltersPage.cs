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
/// Keyboard-first multi-select filter picker. The check-shaped glyphs are visual list-item
/// indicators (not native CmdPal checkbox controls or checkbox accessibility semantics).
/// </summary>
internal sealed partial class FiltersPage : ListPage
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersChanged;
    private readonly SynchronizationContext? _uiContext;
    private readonly Dictionary<string, FilterItem> _filterItems = new(StringComparer.OrdinalIgnoreCase);
    private IListItem[] _items = [];
    private bool _disposed;

    public FiltersPage(CatalogFilterState state, Action filtersChanged)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _filtersChanged = filtersChanged ?? throw new ArgumentNullException(nameof(filtersChanged));
        _uiContext = SynchronizationContext.Current;

        Name = "Filters";
        Title = "Filters";
        _state.FilterOptionsChanged += OnFilterOptionsChanged;
        RefreshOptions();
    }

    public override IListItem[] GetItems() => _items;

    private void OnFilterOptionsChanged(object? sender, EventArgs e)
    {
        void refresh() => RefreshOptions();
        if (_uiContext is { } context)
        {
            // Always enqueue: catalog changes can be raised while the parent page is
            // publishing its own result collection and must not nest host notifications.
            context.Post(_ => refresh(), null);
        }
        else
        {
            refresh();
        }
    }

    /// <summary>
    /// Reuses unchanged option item objects when a catalog refresh changes available metadata.
    /// This avoids unnecessary list resets; the host retains its search text and current focus
    /// for surviving rows as it processes the collection-changed notification.
    /// </summary>
    private void RefreshOptions()
    {
        if (_disposed)
        {
            return;
        }

        var liveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "status:installed", "status:not-installed",
        };
        foreach (var source in _state.Sources)
        {
            liveKeys.Add(Key("source", source));
        }
        foreach (var platform in _state.Platforms)
        {
            liveKeys.Add(Key("platform", platform));
        }

        foreach (var stale in _filterItems.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
        {
            _filterItems.Remove(stale);
        }

        var items = new List<IListItem>
        {
            GetFilterItem("status:installed", "Installed status", "Installed", () => _state.InstalledStatuses.Contains(true),
                () => ToggleStatus(true)),
            GetFilterItem("status:not-installed", "Installed status", "Not installed", () => _state.InstalledStatuses.Contains(false),
                () => ToggleStatus(false)),
        };

        foreach (var source in _state.Sources)
        {
            var value = source;
            items.Add(GetFilterItem(Key("source", value), "Source", value,
                () => _state.SelectedSources.Contains(value, StringComparer.OrdinalIgnoreCase),
                () => ToggleSource(value)));
        }

        foreach (var platform in _state.Platforms)
        {
            var value = platform;
            items.Add(GetFilterItem(Key("platform", value), "Platform", value,
                () => _state.SelectedPlatforms.Contains(value, StringComparer.OrdinalIgnoreCase),
                () => TogglePlatform(value)));
        }

        items.Add(CreateActionItem("Clear installed status", "Clear the status restriction", ClearStatuses));
        items.Add(CreateActionItem("Clear sources", "Clear all source selections", ClearSources));
        items.Add(CreateActionItem("Clear platforms", "Clear all platform selections", ClearPlatforms));
        items.Add(CreateActionItem("Clear all", "Remove every filter restriction", ClearAll));
        var notifyHost = _items.Length > 0;
        _items = items.ToArray();
        if (notifyHost)
        {
            RaiseItemsChanged(_items.Length);
        }
    }

    private FilterItem GetFilterItem(string key, string dimension, string value, Func<bool> isChecked, Action toggle)
    {
        if (!_filterItems.TryGetValue(key, out var item))
        {
            item = new FilterItem(dimension, value, isChecked(), toggle);
            _filterItems.Add(key, item);
        }
        else
        {
            item.Synchronize(isChecked());
        }

        return item;
    }

    private static ListItem CreateActionItem(string title, string subtitle, Action action) =>
        new(new AnonymousCommand(action) { Result = CommandResult.KeepOpen() })
        {
            Title = title,
            Subtitle = subtitle,
            Icon = new IconInfo("\uE74D"),
        };

    private void ToggleStatus(bool status)
    {
        var selected = _state.InstalledStatuses.ToHashSet();
        if (!selected.Add(status))
        {
            selected.Remove(status);
        }
        _state.SetInstalledStatuses(selected);
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void ToggleSource(string source)
    {
        var selected = _state.SelectedSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!selected.Add(source))
        {
            selected.Remove(source);
        }
        _state.SetSources(selected);
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void TogglePlatform(string platform)
    {
        var selected = _state.SelectedPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!selected.Add(platform))
        {
            selected.Remove(platform);
        }
        _state.SetPlatforms(selected);
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void ClearStatuses()
    {
        _state.ClearInstalledStatuses();
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void ClearSources()
    {
        _state.ClearSources();
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void ClearPlatforms()
    {
        _state.ClearPlatforms();
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void ClearAll()
    {
        _state.ClearAll();
        SynchronizeFilterItems();
        ScheduleResultsRefresh();
    }

    private void SynchronizeFilterItems()
    {
        foreach (var item in _filterItems.Values)
        {
            item.Synchronize(item.Dimension switch
            {
                "Installed status" => _state.InstalledStatuses.Contains(item.Value == "Installed"),
                "Source" => _state.SelectedSources.Contains(item.Value, StringComparer.OrdinalIgnoreCase),
                "Platform" => _state.SelectedPlatforms.Contains(item.Value, StringComparer.OrdinalIgnoreCase),
                _ => false,
            });
        }
    }

    /// <summary>
    /// A yield posts the refresh after ListItem command dispatch has returned, without an
    /// arbitrary timing delay. The parent page marshals its result notification to its UI context.
    /// </summary>
    private async void ScheduleResultsRefresh()
    {
        await Task.Yield();
        if (!_disposed)
        {
            _filtersChanged();
        }
    }

    private static string Key(string dimension, string value) => $"{dimension}:{value}";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _state.FilterOptionsChanged -= OnFilterOptionsChanged;
    }

    internal sealed partial class FilterItem : ListItem
    {
        public FilterItem(string dimension, string value, bool isChecked, Action toggle)
            : base(new AnonymousCommand(toggle) { Result = CommandResult.KeepOpen() })
        {
            Dimension = dimension;
            Value = value;
            Title = $"{dimension}: {value}";
            Synchronize(isChecked);
        }

        public string Dimension { get; }
        public string Value { get; }
        public bool IsChecked { get; private set; }

        public void Synchronize(bool isChecked)
        {
            IsChecked = isChecked;
            Icon = new IconInfo(isChecked ? "\uE73A" : "\uE739");
        }
    }
}
