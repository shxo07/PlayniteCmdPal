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

/// <summary>Read-only companion plugin exporting schema v2 catalog data and bridge-owned artwork.</summary>
public sealed class PlayniteCmdPalPlugin : GenericPlugin
{
    public static readonly Guid PluginId = Guid.Parse("9E4CF37F-EF29-495F-9E83-4EA1A389ABD8");
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan ArtworkRevalidationInterval = TimeSpan.FromMinutes(30);
    private static readonly ILogger Logger = LogManager.GetLogger();
    private static readonly Mutex ArtworkExportMutex = new Mutex(false, @"Local\PlayniteCmdPal.ArtworkExport");
    private readonly CatalogExportCoordinator _coordinator;
    private readonly Dispatcher _dispatcher;
    private readonly string _snapshotPath;
    private readonly string _artworkCacheRoot;
    private readonly object _subscriptionGate = new();
    private bool _subscribed;
    private bool _disposed;
    private int _coordinatorStarted;
    private Timer? _artworkRevalidationTimer;

    public PlayniteCmdPalPlugin(IPlayniteAPI api) : base(api)
    {
        Properties = new GenericPluginProperties { HasSettings = false };
        _dispatcher = api.MainView.UIDispatcher;
        var dataRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayniteCmdPal");
        _snapshotPath = System.IO.Path.Combine(dataRoot, "catalog-v2.json");
        _artworkCacheRoot = System.IO.Path.Combine(dataRoot, "artwork-v2");
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
            // Playnite may update bytes behind an unchanged database reference. Re-exporting
            // periodically re-reads the source and lets its content hash detect that change.
            _artworkRevalidationTimer = new Timer(
                _ => _coordinator.RequestExport(),
                null,
                ArtworkRevalidationInterval,
                ArtworkRevalidationInterval);
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

        _artworkRevalidationTimer?.Dispose();
        _artworkRevalidationTimer = null;
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
               !SameIds(oldGame.PlatformIds, newGame.PlatformIds) ||
               !string.Equals(oldGame.CoverImage, newGame.CoverImage, StringComparison.Ordinal) ||
               !string.Equals(oldGame.Icon, newGame.Icon, StringComparison.Ordinal);
    }

    private static bool SameIds(List<Guid>? left, List<Guid>? right) =>
        ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right));

    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        var ownsExportLock = false;
        try
        {
            try
            {
                ownsExportLock = ArtworkExportMutex.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                // The previous producer exited unexpectedly; this process now owns the mutex
                // and may perform transaction recovery before starting another generation.
                ownsExportLock = true;
            }

            if (!ownsExportLock)
                throw new IOException("Another PlayniteCmdPal artwork producer is still active.");
            if (!PlayniteApi.Database.IsOpen)
                return;

            // Playnite's database objects are affinity-sensitive. Project on its dispatcher,
        // then serialize/flush/replace the file on a background thread.
        using (var artwork = new ArtworkCacheGeneration(_artworkCacheRoot, snapshotPath: _snapshotPath))
        {
            var projection = await _dispatcher.InvokeAsync(() => CreateProjection(cancellationToken), DispatcherPriority.Background, cancellationToken).Task.ConfigureAwait(false);
            var snapshot = await Task.Run(() => CreateSnapshot(projection, artwork, cancellationToken), cancellationToken).ConfigureAwait(false);
            artwork.Activate(snapshot.GeneratedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            var snapshotPublished = false;
            try
            {
                try
                {
                    await SnapshotFilePublisher.PublishAsync(_snapshotPath, snapshot, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException)
                {
                    var withoutArtwork = new CatalogSnapshot(snapshot.GeneratedAtUtc,
                        snapshot.Games.Select(g => new SnapshotGame(g.Id, g.Name, g.IsInstalled, g.Source, g.Platform)).ToArray(),
                        SnapshotLimits.SchemaVersion);
                    await SnapshotFilePublisher.PublishAsync(_snapshotPath, withoutArtwork, cancellationToken).ConfigureAwait(false);
                    artwork.ClearActiveCache();
                }

                snapshotPublished = true;
                artwork.MarkPublished();
                artwork.Complete();
            }
            catch
            {
                if (snapshotPublished) artwork.PreserveForRecovery();
                else artwork.Rollback();
                throw;
            }
        }
        finally
        {
            if (ownsExportLock)
                ArtworkExportMutex.ReleaseMutex();
        }
    }

    private CatalogProjection CreateProjection(CancellationToken cancellationToken)
    {
        var database = PlayniteApi.Database;
        var filesRoot = System.IO.Path.Combine(database.DatabasePath, "files");
        var games = new List<GameProjection>();
        var seen = 0;
        foreach (var game in database.Games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++seen > SnapshotLimits.MaxGames)
                throw new InvalidDataException("The Playnite library exceeds the snapshot game limit; the previous snapshot was preserved.");
            if (game == null) continue;
            try
            {
                var id = game.Id.ToString("D", CultureInfo.InvariantCulture);
                games.Add(new GameProjection(id, game.Name, game.IsInstalled, GetSourceName(game.SourceId),
                    GetPlatformNames(game.PlatformIds), game.CoverImage, ResolveArtworkPath(id, game.CoverImage),
                    game.Icon, ResolveArtworkPath(id, game.Icon)));
            }
            catch (ArgumentException) { }
        }
        return new CatalogProjection(filesRoot, games);
    }

    private CatalogSnapshot CreateSnapshot(CatalogProjection projection, ArtworkCacheGeneration artwork, CancellationToken cancellationToken)
    {
        var games = new List<SnapshotGame>(projection.Games.Count);
        foreach (var game in projection.Games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cover = artwork.TryImport(game.Id, game.CoverReference, game.CoverPath, projection.FilesRoot);
            var icon = artwork.TryImport(game.Id, game.IconReference, game.IconPath, projection.FilesRoot);
            try
            {
                games.Add(new SnapshotGame(game.Id, game.Name, game.IsInstalled, game.Source, game.Platform, cover, icon));
            }
            catch (ArgumentException) { }
        }
        return new CatalogSnapshot(DateTimeOffset.UtcNow, games, SnapshotLimits.SchemaVersion);
    }

    private string? ResolveArtworkPath(string gameId, string? reference)
    {
        if (!ArtworkCacheGeneration.TryValidateReference(gameId, reference, out _)) return null;
        try { return PlayniteApi.Database.GetFullFilePath(reference!); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                   ex is ArgumentException || ex is NotSupportedException ||
                                   ex is System.Security.SecurityException) { return null; }
    }

    private sealed class CatalogProjection
    {
        public CatalogProjection(string filesRoot, List<GameProjection> games) { FilesRoot = filesRoot; Games = games; }
        public string FilesRoot { get; }
        public List<GameProjection> Games { get; }
    }

    private sealed class GameProjection
    {
        public GameProjection(string id, string name, bool installed, string? source, string? platform,
            string? coverReference, string? coverPath, string? iconReference, string? iconPath)
        {
            Id = id; Name = name; IsInstalled = installed; Source = source; Platform = platform;
            CoverReference = coverReference; CoverPath = coverPath; IconReference = iconReference; IconPath = iconPath;
        }
        public string Id { get; }
        public string Name { get; }
        public bool IsInstalled { get; }
        public string? Source { get; }
        public string? Platform { get; }
        public string? CoverReference { get; }
        public string? CoverPath { get; }
        public string? IconReference { get; }
        public string? IconPath { get; }
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
