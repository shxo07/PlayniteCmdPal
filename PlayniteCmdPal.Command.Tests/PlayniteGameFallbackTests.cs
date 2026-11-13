using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal;
using PlayniteCmdPal.Playnite;
using PlayniteCmdPal.Snapshot;
using Xunit;

namespace PlayniteCmdPal.Command.Tests;

public sealed class PlayniteGameFallbackTests : IDisposable
{
    private const string GameA = "11111111-1111-4111-8111-111111111111";
    private const string GameB = "22222222-2222-4222-8222-222222222222";
    private const string Title = "Orbit Rally 16";
    private const string OtherTitle = "Moon Orchard";
    private const string IconId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherIconId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string CoverId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string IconPath = @"C:\artwork\synthetic-icon.png";
    private const string OtherIconPath = @"C:\artwork\other-synthetic-icon.png";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Command.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();
    private string SnapshotPath => Path.Combine(_directory, "catalog-v1.json");

    [Fact]
    public async Task ExactMatchDisplaysTheGameAndOpenWithPlayniteThenUsesTheExistingLaunchPath()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        var dispatched = new List<ProcessStartInfo>();
        using var fallback = new PlayniteGameFallback(adapter,
            id => new LaunchGameCommand(id, dispatched.Add).Invoke());

        // Exercise the public SDK handler chain, not just the async test seam.
        IFallbackCommandItem item = fallback;
        var handler = Assert.IsAssignableFrom<IFallbackHandler>(item.FallbackHandler);
        Assert.Same(fallback, handler);
        handler.UpdateQuery("  ORBIT RALLY 16  ");

        Assert.Equal(Title, fallback.Title);
        Assert.Equal("Open with Playnite", fallback.Subtitle);
        var command = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);
        Assert.Equal("Open with Playnite", command.Name);
        Assert.Equal(CommandResultKind.Dismiss, command.Invoke().Kind);
        Assert.Equal($"playnite://playnite/start/{GameA}", Assert.Single(dispatched).FileName);
        Assert.True(dispatched[0].UseShellExecute);
        Assert.Empty(fallback.Title);

        // An already-selected generation cannot dispatch twice.
        Assert.Equal(CommandResultKind.ShowToast, command.Invoke().Kind);
        Assert.Single(dispatched);
    }

    [Theory]
    [InlineData("Orbit", false)]
    [InlineData("Unknown", false)]
    [InlineData("", false)]
    [InlineData("Orbit Rally 16", true)]
    public async Task PartialUnknownAndUninstalledOnlyQueriesDoNotProduceARow(string query, bool hasInstalledMatch)
    {
        var games = new List<SnapshotGame> { new(GameA, Title, false) };
        if (hasInstalledMatch)
        {
            games.Add(new SnapshotGame(GameB, Title, true));
        }

        using var fallback = new PlayniteGameFallback(await CreateCatalogAsync(games.ToArray()));

        await fallback.UpdateQueryAsync(query);

        if (hasInstalledMatch)
        {
            Assert.Equal(Title, fallback.Title);
            Assert.Equal("Open with Playnite", fallback.Subtitle);
        }
        else
        {
            Assert.Empty(fallback.Title);
            Assert.Empty(fallback.Subtitle);
            Assert.IsType<NoOpCommand>(fallback.Command);
        }
    }

    [Fact]
    public async Task TwoInstalledGamesWithAnIdenticalTitleAreSuppressedAsAmbiguous()
    {
        using var fallback = new PlayniteGameFallback(await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, true)));

        await fallback.UpdateQueryAsync(Title);

        Assert.Empty(fallback.Title);
        Assert.Empty(fallback.Subtitle);
        Assert.IsType<NoOpCommand>(fallback.Command);
    }

    [Fact]
    public async Task RetainedCommandNeverLaunchesANewerQueryMatch()
    {
        var adapter = await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, OtherTitle, true));
        var launched = new List<string>();
        using var fallback = new PlayniteGameFallback(adapter, id =>
        {
            launched.Add(id);
            return CommandResult.Dismiss();
        });
        await fallback.UpdateQueryAsync(Title);
        var previous = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);

        await fallback.UpdateQueryAsync(OtherTitle);

        Assert.Equal(CommandResultKind.ShowToast, previous.Invoke().Kind);
        Assert.Empty(launched);
        Assert.Equal(OtherTitle, fallback.Title);
        Assert.Equal(CommandResultKind.Dismiss, Assert.IsAssignableFrom<InvokableCommand>(fallback.Command).Invoke().Kind);
        Assert.Equal(new[] { GameB }, launched);
    }

    [Fact]
    public async Task ClearingQueryHidesTheRowEvenThoughTheOldCommandStillHasAnActionName()
    {
        var launched = false;
        using var fallback = new PlayniteGameFallback(
            await CreateCatalogAsync(new SnapshotGame(GameA, Title, true)),
            _ => { launched = true; return CommandResult.Dismiss(); });
        await fallback.UpdateQueryAsync(Title);
        var previous = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);

        await fallback.UpdateQueryAsync(string.Empty);

        Assert.Equal("Open with Playnite", previous.Name);
        Assert.Empty(fallback.Title);
        Assert.Equal(CommandResultKind.ShowToast, previous.Invoke().Kind);
        Assert.False(launched);
    }

    [Fact]
    public async Task PropertyNotificationCanReenterWithANewerQueryWithoutPublishingStaleState()
    {
        using var fallback = new PlayniteGameFallback(await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, OtherTitle, true)));
        var reentered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = 0;
        fallback.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(fallback.Subtitle) && Interlocked.CompareExchange(ref handled, 1, 0) == 0)
            {
                Task.Run(() => fallback.UpdateQueryAsync(OtherTitle))
                    .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                reentered.TrySetResult(true);
            }
        };

        await fallback.UpdateQueryAsync(Title);
        await reentered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OtherTitle, fallback.Title);
        Assert.Equal("Open with Playnite", fallback.Subtitle);
    }

    [Fact]
    public async Task ReentrantQueryDuringClearCanSupersedeTheOuterQuerySafely()
    {
        using var fallback = new PlayniteGameFallback(await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, OtherTitle, true)));
        await fallback.UpdateQueryAsync(Title);

        var reentered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = 0;
        fallback.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(fallback.Subtitle) && string.IsNullOrEmpty(fallback.Subtitle) &&
                Interlocked.CompareExchange(ref handled, 1, 0) == 0)
            {
                Task.Run(() => fallback.UpdateQueryAsync(OtherTitle))
                    .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                reentered.TrySetResult(true);
            }
        };

        await fallback.UpdateQueryAsync(Title);
        await reentered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OtherTitle, fallback.Title);
        Assert.Equal("Open with Playnite", fallback.Subtitle);
    }

    [Fact]
    public async Task OlderOverlappingReadCannotPublishAfterTheNewerQuery()
    {
        var healthy = await (await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, OtherTitle, true))).GetAsync();
        var olderRead = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var catalog = new StubCatalog(healthy)
        {
            Read = _ => Interlocked.Increment(ref reads) == 1 ? olderRead.Task : Task.FromResult(healthy),
        };
        using var fallback = new PlayniteGameFallback(catalog);
        var older = fallback.UpdateQueryAsync(Title);
        Assert.False(older.IsCompleted);

        await fallback.UpdateQueryAsync(OtherTitle);
        olderRead.SetResult(healthy);
        await older;

        Assert.Equal(OtherTitle, fallback.Title);
        Assert.Equal("Open with Playnite", fallback.Subtitle);
    }

    [Fact]
    public async Task QueryChangeDuringInvocationRevalidationPreventsAnyDispatch()
    {
        var healthy = await (await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, OtherTitle, true))).GetAsync();
        var refreshEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = new TaskCompletionSource<CatalogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new StubCatalog(healthy)
        {
            Refresh = _ => { refreshEntered.SetResult(true); return refresh.Task; },
        };
        var launched = false;
        using var fallback = new PlayniteGameFallback(catalog,
            _ => { launched = true; return CommandResult.Dismiss(); });
        await fallback.UpdateQueryAsync(Title);
        var command = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);
        var invocation = Task.Run(() => command.Invoke());
        await refreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await fallback.UpdateQueryAsync(OtherTitle);
        refresh.SetResult(healthy);

        Assert.Equal(CommandResultKind.ShowToast, (await invocation).Kind);
        Assert.False(launched);
        Assert.Equal(OtherTitle, fallback.Title);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("replaced")]
    [InlineData("uninstalled")]
    [InlineData("ambiguous")]
    public async Task CatalogChangesAfterDisplayCannotLaunchAnUnintendedGame(string change)
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        var launched = false;
        using var fallback = new PlayniteGameFallback(adapter,
            _ => { launched = true; return CommandResult.Dismiss(); });
        await fallback.UpdateQueryAsync(Title);
        var command = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);
        switch (change)
        {
            case "missing":
                File.Delete(SnapshotPath);
                break;
            case "stale":
                _time.Advance(PlayniteCatalogAdapter.StaleAfter);
                break;
            case "replaced":
                await CreateCatalogAsync(new SnapshotGame(GameB, Title, true));
                break;
            case "uninstalled":
                await CreateCatalogAsync(new SnapshotGame(GameA, Title, false));
                break;
            default:
                await CreateCatalogAsync(new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, true));
                break;
        }

        var result = command.Invoke();

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        Assert.Contains("Search games", Assert.IsAssignableFrom<IToastArgs>(result.Args).Message);
        Assert.False(launched);
        Assert.Empty(fallback.Title);
    }

    [Fact]
    public async Task LaunchFailurePreservesTheExistingActionableFailureResult()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        using var fallback = new PlayniteGameFallback(adapter,
            id => new LaunchGameCommand(id, _ => throw new Win32Exception("Synthetic failure")).Invoke());
        await fallback.UpdateQueryAsync(Title);

        var result = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command).Invoke();

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        Assert.Contains("Windows couldn't open Playnite", Assert.IsAssignableFrom<IToastArgs>(result.Args).Message);
    }

    [Fact]
    public async Task ReadFailureDoesNotEscapeTheSdkCallbackOrShowANoisyRow()
    {
        var catalog = new StubCatalog(CatalogReadResult.Unavailable(new CatalogIssue(CatalogErrorCode.Missing, "Synthetic missing catalog")))
        {
            Read = _ => Task.FromException<CatalogReadResult>(new IOException("Synthetic read failure")),
        };
        using var fallback = new PlayniteGameFallback(catalog);

        await fallback.UpdateQueryAsync(Title);

        Assert.Empty(fallback.Title);
        Assert.IsType<NoOpCommand>(fallback.Command);
    }

    [Fact]
    public async Task DisposedFallbackInvalidatesRetainedCommandsAndIgnoresFurtherQueries()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true));
        var launched = false;
        using var fallback = new PlayniteGameFallback(adapter,
            _ => { launched = true; return CommandResult.Dismiss(); });
        await fallback.UpdateQueryAsync(Title);
        var command = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);

        fallback.Dispose();
        await fallback.UpdateQueryAsync(Title);

        Assert.Empty(fallback.Title);
        Assert.Equal(CommandResultKind.ShowToast, command.Invoke().Kind);
        Assert.False(launched);
    }

    [Fact]
    public async Task SdkCallbackPublishesTheMatchBeforeDecodingAndNotifiesWhenItsSavedIconArrives()
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true,
            coverArtworkId: CoverId, iconArtworkId: IconId));
        var decodeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDecode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolved = new ConcurrentQueue<string>();
        var loader = CreateArtworkLoader(id => { resolved.Enqueue(id); return IconPath; }, async (_, token) =>
        {
            decodeStarted.TrySetResult(true);
            await releaseDecode.Task.WaitAsync(token);
            return true;
        });
        using var fallback = new PlayniteGameFallback(adapter, artwork: loader);
        var iconChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fallback.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(fallback.Icon) && !ReferenceEquals(fallback.Icon, ArtworkIconLoader.FallbackIcon))
            {
                iconChanged.TrySetResult(true);
            }
        };

        // The synchronous SDK callback must finish even while decoding is blocked.
        IFallbackCommandItem item = fallback;
        var handler = Assert.IsAssignableFrom<IFallbackHandler>(item.FallbackHandler);
        await Task.Run(() => handler.UpdateQuery(Title)).WaitAsync(TimeSpan.FromSeconds(5));
        await decodeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Title, fallback.Title);
        Assert.Equal("Open with Playnite", fallback.Subtitle);
        Assert.Same(ArtworkIconLoader.FallbackIcon, fallback.Icon);
        var artworkUpdate = fallback.ArtworkUpdate;
        Assert.False(artworkUpdate.IsCompleted);

        releaseDecode.TrySetResult(true);
        await artworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(iconChanged.Task.IsCompletedSuccessfully);
        Assert.Equal(new Uri(IconPath).AbsoluteUri, fallback.Icon!.Light.Icon);
        Assert.Equal(new[] { IconId }, resolved.ToArray());
        Assert.Equal(Title, fallback.Title);
    }

    [Theory]
    [InlineData("missing-icon")]
    [InlineData("missing-cache")]
    [InlineData("corrupt-icon")]
    [InlineData("read-failure")]
    public async Task UnavailableArtworkKeepsTheNeutralIconWithoutUsingCoverOrPreventingLaunch(string failure)
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true,
            coverArtworkId: CoverId, iconArtworkId: failure == "missing-icon" ? null : IconId));
        var resolved = new ConcurrentQueue<string>();
        var loader = CreateArtworkLoader(id =>
        {
            resolved.Enqueue(id);
            if (failure == "read-failure") throw new IOException("Synthetic artwork read failure");
            return failure == "missing-cache" ? null : IconPath;
        }, (_, _) => Task.FromResult(failure != "corrupt-icon"));
        var launched = new List<string>();
        using var fallback = new PlayniteGameFallback(adapter,
            id => { launched.Add(id); return CommandResult.Dismiss(); }, loader);

        await fallback.UpdateQueryAsync(Title);
        await fallback.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Title, fallback.Title);
        Assert.Same(ArtworkIconLoader.FallbackIcon, fallback.Icon);
        Assert.Equal(failure == "missing-icon" ? Array.Empty<string>() : new[] { IconId }, resolved.ToArray());
        Assert.Equal(CommandResultKind.Dismiss, Assert.IsAssignableFrom<InvokableCommand>(fallback.Command).Invoke().Kind);
        Assert.Equal(new[] { GameA }, launched);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Moon Orchard")]
    public async Task ClearingOrChangingToAGameWithoutArtworkResetsThePreviousIcon(string nextQuery)
    {
        var adapter = await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true, iconArtworkId: IconId),
            new SnapshotGame(GameB, OtherTitle, true));
        using var fallback = new PlayniteGameFallback(adapter,
            artwork: CreateArtworkLoader(_ => IconPath, (_, _) => Task.FromResult(true)));
        await fallback.UpdateQueryAsync(Title);
        await fallback.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotSame(ArtworkIconLoader.FallbackIcon, fallback.Icon);

        await fallback.UpdateQueryAsync(nextQuery);
        await fallback.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(nextQuery, fallback.Title);
        Assert.Same(ArtworkIconLoader.FallbackIcon, fallback.Icon);
    }

    [Fact]
    public async Task CancelledOlderIconCannotOverwriteANewerGamesIconEvenIfItsDecoderFinishesLate()
    {
        var adapter = await CreateCatalogAsync(
            new SnapshotGame(GameA, Title, true, iconArtworkId: IconId),
            new SnapshotGame(GameB, OtherTitle, true, iconArtworkId: OtherIconId));
        var decodeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDecode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldDecodeFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = CreateArtworkLoader(id => id == IconId ? IconPath : OtherIconPath, async (path, _) =>
        {
            if (path == IconPath)
            {
                decodeStarted.TrySetResult(true);
                // Simulate a decoder that cannot abort its underlying image operation.
                await releaseDecode.Task;
                oldDecodeFinished.TrySetResult(true);
            }
            return true;
        });
        using var fallback = new PlayniteGameFallback(adapter, artwork: loader);
        await fallback.UpdateQueryAsync(Title);
        var oldArtwork = fallback.ArtworkUpdate;
        await decodeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await fallback.UpdateQueryAsync(OtherTitle);
            await fallback.ArtworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
            // Leaving the old query cancels its waiter without waiting for that decoder.
            await oldArtwork.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new Uri(OtherIconPath).AbsoluteUri, fallback.Icon!.Light.Icon);
        }
        finally
        {
            releaseDecode.TrySetResult(true);
        }
        await oldDecodeFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OtherTitle, fallback.Title);
        Assert.Equal(new Uri(OtherIconPath).AbsoluteUri, fallback.Icon!.Light.Icon);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokingOrDisposingDuringArtworkLoadCancelsItWithoutLatePublication(bool dispose)
    {
        var adapter = await CreateCatalogAsync(new SnapshotGame(GameA, Title, true, iconArtworkId: IconId));
        var decodeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodeCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = CreateArtworkLoader(_ => IconPath, async (_, token) =>
        {
            decodeStarted.TrySetResult(true);
            using var registration = token.Register(() => decodeCancelled.TrySetResult(true));
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        });
        var launched = new List<string>();
        using var fallback = new PlayniteGameFallback(adapter,
            id => { launched.Add(id); return CommandResult.Dismiss(); }, loader);
        await fallback.UpdateQueryAsync(Title);
        var artworkUpdate = fallback.ArtworkUpdate;
        var command = Assert.IsAssignableFrom<InvokableCommand>(fallback.Command);
        await decodeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (dispose)
        {
            fallback.Dispose();
            Assert.Equal(CommandResultKind.ShowToast, command.Invoke().Kind);
            Assert.Empty(launched);
        }
        else
        {
            // Launch uses only the verified game ID; it must never await the image.
            var result = await Task.Run(() => command.Invoke()).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(CommandResultKind.Dismiss, result.Kind);
            Assert.Equal(new[] { GameA }, launched);
        }
        await artworkUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        await decodeCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(fallback.Title);
        Assert.Same(ArtworkIconLoader.FallbackIcon, fallback.Icon);
    }

    private static ArtworkIconLoader CreateArtworkLoader(
        Func<string, string?> resolve,
        Func<string, CancellationToken, Task<bool>> decode) =>
        new(resolve, decode, fileExists: _ => true);

    private async Task<PlayniteCatalogAdapter> CreateCatalogAsync(params SnapshotGame[] games)
    {
        Directory.CreateDirectory(_directory);
        var schemaVersion = games.Any(game => game.IconArtworkId is not null || game.CoverArtworkId is not null)
            ? SnapshotLimits.SchemaVersion : SnapshotLimits.LegacySchemaVersion;
        await SnapshotFilePublisher.PublishAsync(SnapshotPath,
            new CatalogSnapshot(_time.GetUtcNow(), games, schemaVersion),
            CancellationToken.None, schemaVersion, includeLastActivity: false);
        return new PlayniteCatalogAdapter(SnapshotPath, _time);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class StubCatalog : IReadOnlyPlayniteCatalog
    {
        public StubCatalog(CatalogReadResult result)
        {
            Read = _ => Task.FromResult(result);
            Refresh = _ => Task.FromResult(result);
        }

        public Func<CancellationToken, Task<CatalogReadResult>> Read { get; init; }
        public Func<CancellationToken, Task<CatalogReadResult>> Refresh { get; init; }
        public Task<CatalogReadResult> GetAsync(CancellationToken cancellationToken = default) => Read(cancellationToken);
        public Task<CatalogReadResult> RefreshAsync(CancellationToken cancellationToken = default) => Refresh(cancellationToken);
    }
}
