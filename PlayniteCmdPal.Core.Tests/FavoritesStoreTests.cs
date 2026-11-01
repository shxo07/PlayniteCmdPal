using System.Text.Json;
using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class FavoritesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Favorites.Tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "favorites-v1.json");

    [Fact]
    public void MissingFileStartsWithNoFavorites()
    {
        var store = new FavoritesStore(FilePath);
        Assert.Empty(store.GetFavoriteIds());
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void TogglePinsAndUnpinsByStableGameId()
    {
        var store = new FavoritesStore(FilePath);
        Assert.True(store.Toggle("game-id"));
        Assert.True(store.IsFavorite("game-id"));
        Assert.False(store.Toggle("game-id"));
        Assert.False(store.IsFavorite("game-id"));
    }

    [Fact]
    public void ToggleRaisesFavoritesChangedAfterEachSuccessfulMutation()
    {
        var store = new FavoritesStore(FilePath);
        var notifications = 0;
        store.FavoritesChanged += (_, _) => notifications++;

        Assert.True(store.Toggle("game-id"));
        Assert.Equal(1, notifications);

        Assert.False(store.Toggle("game-id"));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void ObserverFailureDoesNotFailMutationOrUndoDurablePreference()
    {
        var store = new FavoritesStore(FilePath);
        store.FavoritesChanged += (_, _) => throw new InvalidOperationException("Simulated page refresh failure.");

        Assert.True(store.Toggle("game-id"));
        Assert.True(store.IsFavorite("game-id"));
        Assert.True(new FavoritesStore(FilePath).IsFavorite("game-id"));
    }

    [Fact]
    public void FavoritesSurviveStoreRecreation()
    {
        var first = new FavoritesStore(FilePath);
        first.Toggle("game-b");
        first.Toggle("game-a");

        var reopened = new FavoritesStore(FilePath);

        Assert.Equal(new[] { "game-a", "game-b" }, reopened.GetFavoriteIds());
    }

    [Fact]
    public void WritesVersionedJsonAndReplacesThePreviousDocument()
    {
        var store = new FavoritesStore(FilePath);
        store.Toggle("game-a");
        store.Toggle("game-b");

        using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
        Assert.Equal(FavoritesStore.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(new[] { "game-a", "game-b" }, document.RootElement.GetProperty("favoriteGameIds").EnumerateArray().Select(value => value.GetString()));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void MalformedPreferencesRecoverOnNextMutation()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ definitely not json");
        var store = new FavoritesStore(FilePath);

        Assert.Empty(store.GetFavoriteIds());
        Assert.True(store.Toggle("recovered-game"));

        var reopened = new FavoritesStore(FilePath);
        Assert.Equal(new[] { "recovered-game" }, reopened.GetFavoriteIds());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"preferences\"")]
    [InlineData("42")]
    public void NonObjectJsonRootsRecoverOnReadAndAreReplacedByValidVersionOneDocument(string malformedRoot)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, malformedRoot);
        var store = new FavoritesStore(FilePath);

        Assert.Empty(store.GetFavoriteIds());
        Assert.False(store.IsFavorite("existing-game"));

        Assert.True(store.Toggle("recovered-game"));

        using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal(FavoritesStore.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(new[] { "recovered-game" }, document.RootElement.GetProperty("favoriteGameIds").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "recovered-game" }, new FavoritesStore(FilePath).GetFavoriteIds());
    }

    [Fact]
    public void MissingGamesCanBeFilteredWithoutDeletingTheirSavedIds()
    {
        var store = new FavoritesStore(FilePath);
        store.Toggle("present-game");
        store.Toggle("removed-game");
        var currentCatalogIds = new HashSet<string>(["present-game"], StringComparer.Ordinal);

        var visibleIds = store.GetFavoriteIds().Where(currentCatalogIds.Contains).ToArray();

        Assert.Equal(new[] { "present-game" }, visibleIds);
        Assert.Equal(new[] { "present-game", "removed-game" }, new FavoritesStore(FilePath).GetFavoriteIds());
    }

    [Fact]
    public void MissingSchemaVersionIsRecoveredAsMalformedData()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, """{"favoriteGameIds":["stale-game"]}""");
        var store = new FavoritesStore(FilePath);

        Assert.Empty(store.GetFavoriteIds());
        Assert.True(store.Toggle("recovered-game"));
        Assert.Equal(new[] { "recovered-game" }, new FavoritesStore(FilePath).GetFavoriteIds());
    }

    [Fact]
    public void RejectsFutureSchemaBeforeDeserializingItsChangedPayloadAndPreservesOriginalBytes()
    {
        Directory.CreateDirectory(_directory);
        const string futureDocument = """{"schemaVersion":2,"favoriteGameIds":{"ids":["future-game"]}}""";
        File.WriteAllText(FilePath, futureDocument);
        var store = new FavoritesStore(FilePath);

        Assert.Throws<InvalidDataException>(() => store.Toggle("new-game"));

        Assert.Equal(futureDocument, File.ReadAllText(FilePath));
    }

    [Fact]
    public void FailedWriteRollsBackMemoryAndCleansTemporaryFile()
    {
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(FilePath);
        var store = new FavoritesStore(FilePath);

        var exception = Record.Exception(() => store.Toggle("game-id"));

        Assert.NotNull(exception);
        Assert.True(
            exception is IOException or UnauthorizedAccessException,
            $"Expected a file-system write failure, but got {exception.GetType().Name}.");

        Assert.False(store.IsFavorite("game-id"));
        Assert.Empty(Directory.GetFiles(_directory, "favorites-v1.json.*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
