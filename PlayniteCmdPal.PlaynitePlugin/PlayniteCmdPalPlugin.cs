using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteCmdPal.Snapshot;

namespace PlayniteCmdPal.PlaynitePlugin;

/// <summary>Read-only companion plugin. It projects Playnite's library into the accepted v1 JSON snapshot.</summary>
public sealed class PlayniteCmdPalPlugin : GenericPlugin
{
    public static readonly Guid PluginId = Guid.Parse("9E4CF37F-EF29-495F-9E83-4EA1A389ABD8");
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);
    private static readonly ILogger Logger = LogManager.GetLogger();
    private readonly CatalogExportCoordinator _coordinator;
    private readonly Dispatcher _dispatcher;
    private readonly string _snapshotPath;
    private readonly object _subscriptionGate = new();
    private bool _subscribed;
    private bool _disposed;
    private int _coordinatorStarted;

    public PlayniteCmdPalPlugin(IPlayniteAPI api) : base(api)
    {
        Properties = new GenericPluginProperties { HasSettings = false };
        _dispatcher = api.MainView.UIDispatcher;
        _snapshotPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlayniteCmdPal",
            "catalog-v1.json");
        _coordinator = new CatalogExportCoordinator(ExportAsync, Debounce, exception =>
            Logger.Error(exception, "PlayniteCmdPal catalog export failed; the previous snapshot was preserved."));
    }

    public override Guid Id => PluginId;

    public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
    {
        Subscribe();
        _coordinator.RequestExport();
        if (Interlocked.CompareExchange(ref _coordinatorStarted, 1, 0) == 0)
        {
            _ = RunCoordinatorAsync();
        }
    }

    public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) => _coordinator.RequestExport();

    public override void OnGameInstalled(OnGameInstalledEventArgs args) => _coordinator.RequestExport();

    public override void OnGameUninstalled(OnGameUninstalledEventArgs args) => _coordinator.RequestExport();

    public override void Dispose()
    {
        lock (_subscriptionGate)
        {
            if (_disposed)
            {
                base.Dispose();
                return;
            }

            _disposed = true;
            UnsubscribeLocked();
        }

        _coordinator.Dispose();
        base.Dispose();
    }

    private async Task RunCoordinatorAsync()
    {
        try
        {
            await _coordinator.RunAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "PlayniteCmdPal catalog export coordinator stopped.");
        }
    }

    private void Subscribe()
    {
        lock (_subscriptionGate)
        {
            if (_subscribed || _disposed)
            {
                return;
            }

            PlayniteApi.Database.DatabaseOpened += OnDatabaseOpened;
            PlayniteApi.Database.Games.ItemCollectionChanged += OnGamesChanged;
            PlayniteApi.Database.Games.ItemUpdated += OnGamesUpdated;
            PlayniteApi.Database.Sources.ItemUpdated += OnSourcesUpdated;
            PlayniteApi.Database.Sources.ItemCollectionChanged += OnSourcesChanged;
            PlayniteApi.Database.Platforms.ItemUpdated += OnPlatformsUpdated;
            PlayniteApi.Database.Platforms.ItemCollectionChanged += OnPlatformsChanged;
            _subscribed = true;
        }
    }

    private void UnsubscribeLocked()
    {
        if (!_subscribed)
        {
            return;
        }

        PlayniteApi.Database.DatabaseOpened -= OnDatabaseOpened;
        PlayniteApi.Database.Games.ItemCollectionChanged -= OnGamesChanged;
        PlayniteApi.Database.Games.ItemUpdated -= OnGamesUpdated;
        PlayniteApi.Database.Sources.ItemUpdated -= OnSourcesUpdated;
        PlayniteApi.Database.Sources.ItemCollectionChanged -= OnSourcesChanged;
        PlayniteApi.Database.Platforms.ItemUpdated -= OnPlatformsUpdated;
        PlayniteApi.Database.Platforms.ItemCollectionChanged -= OnPlatformsChanged;
        _subscribed = false;
    }

    private void OnDatabaseOpened(object sender, EventArgs args) => _coordinator.RequestExport();
    private void OnGamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args) => _coordinator.RequestExport();
    private void OnSourcesChanged(object sender, ItemCollectionChangedEventArgs<GameSource> args) => _coordinator.RequestExport();
    private void OnSourcesUpdated(object sender, ItemUpdatedEventArgs<GameSource> args) => _coordinator.RequestExport();
    private void OnPlatformsChanged(object sender, ItemCollectionChangedEventArgs<Platform> args) => _coordinator.RequestExport();
    private void OnPlatformsUpdated(object sender, ItemUpdatedEventArgs<Platform> args) => _coordinator.RequestExport();

    private void OnGamesUpdated(object sender, ItemUpdatedEventArgs<Game> args)
    {
        if (args.UpdatedItems == null || args.UpdatedItems.Any(IsRelevantGameUpdate))
        {
            _coordinator.RequestExport();
        }
    }

    private static bool IsRelevantGameUpdate(ItemUpdateEvent<Game> update)
    {
        var oldGame = update.OldData;
        var newGame = update.NewData;
        if (oldGame == null || newGame == null)
        {
            return true;
        }

        return !string.Equals(oldGame.Name, newGame.Name, StringComparison.Ordinal) ||
               oldGame.IsInstalled != newGame.IsInstalled ||
               oldGame.SourceId != newGame.SourceId ||
               !SameIds(oldGame.PlatformIds, newGame.PlatformIds);
    }

    private static bool SameIds(List<Guid>? left, List<Guid>? right) =>
        ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right));

    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        if (!PlayniteApi.Database.IsOpen)
        {
            return;
        }

        // Playnite's database objects are affinity-sensitive. Project on its dispatcher,
        // then serialize/flush/replace the file on a background thread.
        var snapshot = await _dispatcher.InvokeAsync(() => CreateSnapshot(cancellationToken), DispatcherPriority.Background, cancellationToken).Task.ConfigureAwait(false);
        await SnapshotFilePublisher.PublishAsync(_snapshotPath, snapshot, cancellationToken).ConfigureAwait(false);
    }

    private CatalogSnapshot CreateSnapshot(CancellationToken cancellationToken)
    {
        var games = new List<SnapshotGame>();
        var skipped = 0;
        var seen = 0;
        foreach (var game in PlayniteApi.Database.Games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++seen > SnapshotLimits.MaxGames)
            {
                throw new InvalidDataException("The Playnite library exceeds the snapshot game limit; the previous snapshot was preserved.");
            }

            if (game == null)
            {
                skipped++;
                continue;
            }

            try
            {
                games.Add(new SnapshotGame(
                    game.Id.ToString("D", CultureInfo.InvariantCulture),
                    game.Name,
                    game.IsInstalled,
                    GetSourceName(game.SourceId),
                    GetPlatformNames(game.PlatformIds)));
            }
            catch (ArgumentException)
            {
                skipped++;
            }
        }

        if (skipped > 0)
        {
            Logger.Warn($"PlayniteCmdPal skipped {skipped} invalid or over-limit library record(s) during export.");
        }

        return new CatalogSnapshot(DateTimeOffset.UtcNow, games);
    }

    private string? GetSourceName(Guid sourceId) =>
        sourceId == Guid.Empty ? null : PlayniteApi.Database.Sources[sourceId]?.Name;

    private string? GetPlatformNames(List<Guid>? platformIds)
    {
        if (platformIds == null || platformIds.Count == 0)
        {
            return null;
        }

        var names = platformIds
            .Select(id => PlayniteApi.Database.Platforms[id]?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return names.Length == 0 ? null : string.Join(", ", names);
    }
}
