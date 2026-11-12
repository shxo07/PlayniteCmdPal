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
        Assert.Same(fallback, item.FallbackHandler);
        item.FallbackHandler.UpdateQuery("  ORBIT RALLY 16  ");

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
    public async Task WeakNoMatchAndAmbiguousQueriesHaveNoGenericOrDuplicateRow(string query, bool ambiguous)
    {
        var games = new List<SnapshotGame> { new(GameA, Title, true) };
        if (ambiguous)
        {
            games.Add(new SnapshotGame(GameB, Title, false));
        }

        using var fallback = new PlayniteGameFallback(await CreateCatalogAsync(games.ToArray()));

        await fallback.UpdateQueryAsync(query);

        Assert.Empty(fallback.Title);
        Assert.Empty(fallback.Subtitle);
        Assert.IsType<NoOpCommand>(fallback.Command);
    }

    [Fact]
    public async Task RetainedCommandNeverLaunchesANewerQuerysMatch()
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
            default:
                await CreateCatalogAsync(new SnapshotGame(GameA, Title, true), new SnapshotGame(GameB, Title, false));
                break;
        }

        var result = command.Invoke();

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        Assert.Contains("refresh the catalog", Assert.IsAssignableFrom<IToastArgs>(result.Args).Message);
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

    private async Task<PlayniteCatalogAdapter> CreateCatalogAsync(params SnapshotGame[] games)
    {
        Directory.CreateDirectory(_directory);
        await SnapshotFilePublisher.PublishAsync(SnapshotPath, new CatalogSnapshot(_time.GetUtcNow(), games));
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
