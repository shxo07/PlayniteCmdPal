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
    private readonly object _itemsGate = new();
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

    public override IListItem[] GetItems()
    {
        lock (_itemsGate)
            return _items.ToArray();
    }

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
            // Without a captured UI context, defer the host notification as well. This
            // prevents catalog updates from nesting a FiltersPage notification inside the
            // parent page's catalog-publication/request critical section.
            _ = Task.Run(refresh);
        }
    }

    /// <summary>
    /// Reuses unchanged option item objects when a catalog refresh changes available metadata.
    /// This avoids unnecessary list resets; the host retains its search text and current focus
    /// for surviving rows as it processes the collection-changed notification.
    /// </summary>
    private void RefreshOptions()
    {
        // Take independent snapshots before entering the page-item lock. CatalogFilterState
        // never calls subscribers while holding its own lock, so this keeps lock ordering simple.
        var sources = _state.Sources;
        var platforms = _state.Platforms;
        var installed = _state.InstalledStatuses;
        var selectedSources = _state.SelectedSources;
        var selectedPlatforms = _state.SelectedPlatforms;
        int count;

        lock (_itemsGate)
        {
            if (_disposed)
                return;

            var liveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "status:installed", "status:not-installed",
            };
            foreach (var source in sources)
                liveKeys.Add(Key("source", source));
            foreach (var platform in platforms)
                liveKeys.Add(Key("platform", platform));

            foreach (var stale in _filterItems.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
                _filterItems.Remove(stale);

            var items = new List<IListItem>
            {
                GetFilterItem("status:installed", "Installed status", "Installed", () => installed.Contains(true),
                    () => ToggleStatus(true)),
                GetFilterItem("status:not-installed", "Installed status", "Not installed", () => installed.Contains(false),
                    () => ToggleStatus(false)),
            };

            foreach (var source in sources)
            {
                var value = source;
                items.Add(GetFilterItem(Key("source", value), "Source", value,
                    () => selectedSources.Contains(value, StringComparer.OrdinalIgnoreCase),
                    () => ToggleSource(value)));
            }

            foreach (var platform in platforms)
            {
                var value = platform;
                items.Add(GetFilterItem(Key("platform", value), "Platform", value,
                    () => selectedPlatforms.Contains(value, StringComparer.OrdinalIgnoreCase),
                    () => TogglePlatform(value)));
            }

            items.Add(CreateActionItem("Clear installed status", "Clear the status restriction", ClearStatuses));
            items.Add(CreateActionItem("Clear sources", "Clear all source selections", ClearSources));
            items.Add(CreateActionItem("Clear platforms", "Clear all platform selections", ClearPlatforms));
            items.Add(CreateActionItem("Clear all", "Remove every filter restriction", ClearAll));
            var notifyHost = _items.Length > 0;
            _items = items.ToArray();
            count = _items.Length;

            if (!notifyHost)
                return;
        }

        // Host callbacks are deliberately outside _itemsGate because GetItems can re-enter.
        RaiseItemsChanged(count);
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
        if (_state.ToggleInstalledStatus(status))
        {
            SynchronizeFilterItems();
            ScheduleResultsRefresh();
        }
    }

    private void ToggleSource(string source)
    {
        if (_state.ToggleSource(source))
        {
            SynchronizeFilterItems();
            ScheduleResultsRefresh();
        }
    }

    private void TogglePlatform(string platform)
    {
        if (_state.TogglePlatform(platform))
        {
            SynchronizeFilterItems();
            ScheduleResultsRefresh();
        }
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
        var installed = _state.InstalledStatuses;
        var sources = _state.SelectedSources;
        var platforms = _state.SelectedPlatforms;
        lock (_itemsGate)
        {
            foreach (var item in _filterItems.Values)
            {
                item.Synchronize(item.Dimension switch
                {
                    "Installed status" => installed.Contains(item.Value == "Installed"),
                    "Source" => sources.Contains(item.Value, StringComparer.OrdinalIgnoreCase),
                    "Platform" => platforms.Contains(item.Value, StringComparer.OrdinalIgnoreCase),
                    _ => false,
                });
            }
        }
    }

    /// <summary>
    /// A yield posts the refresh after ListItem command dispatch has returned, without an
    /// arbitrary timing delay. The parent page marshals its result notification to its UI context.
    /// </summary>
    private async void ScheduleResultsRefresh()
    {
        await Task.Yield();
        bool shouldRefresh;
        lock (_itemsGate)
            shouldRefresh = !_disposed;
        if (shouldRefresh)
            _filtersChanged();
    }

    private static string Key(string dimension, string value) => $"{dimension}:{value}";

    public void Dispose()
    {
        lock (_itemsGate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
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
