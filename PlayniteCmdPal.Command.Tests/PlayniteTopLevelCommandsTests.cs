using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Command.Tests;

public sealed class PlayniteTopLevelCommandsTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string Title = "Orbit Rally 16";
    private const string IconId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string IconPath = @"C:\artwork\synthetic-installed-icon.png";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Command.Tests", Guid.NewGuid().ToString("N"));
    private readonly ICommandItem[] _navigation = [
        new CommandItem(new NoOpCommand()) { Title = "Playnite" },
    ];
    private string SnapshotPath => Path.Combine(_directory, "catalog.json");

    [Fact]
    public async Task ProviderKeepsGamesOutOfTopLevelAndOffersOneIconBearingInstalledGameFallback()
    {
        var adapter = await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true, iconArtworkId: IconId),
            new SnapshotGame(GameB, "Uninstalled Synthetic Game", false, iconArtworkId: IconId));
        var resolved = new ConcurrentQueue<string>();
        var loader = new ArtworkIconLoader(id =>
        {
            resolved.Enqueue(id);
            return IconPath;
        }, (_, _) => Task.FromResult(true), fileExists: _ => true);
        var dispatched = new List<ProcessStartInfo>();
        using var provider = new PlayniteCmdPalCommandsProvider(
            adapter,
            _navigation,
            loader,
            id => new LaunchGameCommand(id, dispatched.Add).Invoke());
        ICommandProvider sdk = provider;

        Assert.Equal("Playnite", sdk.DisplayName);
        Assert.True(sdk.Frozen);
        var rootCommands = sdk.TopLevelCommands();
        Assert.Equal(_navigation, rootCommands);
        Assert.Equal("Playnite", Assert.Single(rootCommands).Title);
        Assert.Same(rootCommands, sdk.TopLevelCommands());
        Assert.Same(sdk.FallbackCommands(), sdk.FallbackCommands());
        var fallback = Assert.IsType<PlayniteGameFallback>(Assert.Single(sdk.FallbackCommands()));
        Assert.Empty(fallback.Title);

        // Searching the extension name does not expose every game's root row.
        var handler = Assert.IsAssignableFrom<IFallbackHandler>(fallback.FallbackHandler);
        handler.UpdateQuery("playnite");
        Assert.Empty(fallback.Title);
        Assert.Same(rootCommands, sdk.TopLevelCommands());

        // Exact installed-game search returns only the user's requested action label.
        handler.UpdateQuery(Title);
        Assert.Equal(Title, fallback.Title);
        Assert.Equal("Open with Playnite", fallback.Subtitle);
        var command = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);
        Assert.Equal("Open with Playnite", command.Name);
        Assert.Equal($"playnitecmdpal.open-game.{GameA}", command.Id);
        await fallback.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new Uri(IconPath).AbsoluteUri, fallback.Icon!.Light.Icon);
        Assert.Equal(new[] { IconId }, resolved.ToArray());

        Assert.Equal(CommandResultKind.Dismiss, command.Invoke().Kind);
        Assert.Equal($"playnite://playnite/start/{GameA}", Assert.Single(dispatched).FileName);
        Assert.True(dispatched[0].UseShellExecute);
        Assert.Same(rootCommands, sdk.TopLevelCommands());
        Assert.Empty(fallback.Title);
    }

    [Fact]
    public async Task UninstalledOnlyExactTitleDoesNotProduceAGlobalFallback()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameB, Title, false, iconArtworkId: IconId));
        using var provider = new PlayniteCmdPalCommandsProvider(adapter, _navigation);
        var fallback = Assert.IsType<PlayniteGameFallback>(Assert.Single(provider.FallbackCommands()));

        fallback.UpdateQuery(Title);

        Assert.Empty(fallback.Title);
        Assert.Empty(fallback.Subtitle);
        Assert.IsType<NoOpCommand>(fallback.Command);
        Assert.Equal(_navigation, provider.TopLevelCommands());
    }

    [Fact]
    public void ProviderLookupOnlyResolvesTheSingleUnifiedNavigationItem()
    {
        var provider = new PlayniteCmdPalCommandsProvider(
            new StubCatalog(CatalogReadResult.Unavailable(new CatalogIssue(CatalogErrorCode.Missing, "Synthetic missing catalog"))),
            _navigation);

        Assert.Same(_navigation[0], provider.TopLevelCommands()[0]);
        Assert.Null(provider.GetCommandItem("playnitecmdpal.open-game." + GameA));
        Assert.Null(provider.GetCommand("playnitecmdpal.open-game." + GameA));
        var rootCommands = provider.TopLevelCommands();
        Assert.Equal(_navigation, rootCommands);
        Assert.Same(rootCommands, provider.TopLevelCommands());
        provider.Dispose();
    }

    private async Task<PlayniteCatalogAdapter> CreateCatalogAsync(params SnapshotGame[] games)
    {
        Directory.CreateDirectory(_directory);
        var snapshot = new CatalogSnapshot(DateTimeOffset.UtcNow, games, SnapshotLimits.SchemaVersion);
        await SnapshotFilePublisher.PublishAsync(SnapshotPath, snapshot,
            CancellationToken.None, SnapshotLimits.SchemaVersion, includeLastActivity: false);
        return new PlayniteCatalogAdapter(SnapshotPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class StubCatalog(CatalogReadResult result) : IReadOnlyPlayniteCatalog
    {
        public Task<CatalogReadResult> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
        public Task<CatalogReadResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
