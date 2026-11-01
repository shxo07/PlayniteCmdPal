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
    public void RejectsUnsupportedSchemaWithoutOverwritingIt()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, """{"schemaVersion":2,"favoriteGameIds":["future-game"]}""");
        var store = new FavoritesStore(FilePath);

        Assert.Throws<InvalidDataException>(() => store.GetFavoriteIds());
        Assert.Contains("future-game", File.ReadAllText(FilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
