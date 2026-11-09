using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal;
using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Command.Tests;

public sealed class ToggleFavoriteCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Command.Tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "favorites-v1.json");

    [Fact]
    public void SuccessfulPinShowsToastAndKeepsPaletteOpen()
    {
        var command = new ToggleFavoriteCommand(new FavoritesStore(FilePath), "game-id");

        var result = command.Invoke();

        AssertToastKeepsPaletteOpen(result, "Game pinned to Favorites.");
        Assert.True(new FavoritesStore(FilePath).IsFavorite("game-id"));
    }

    [Fact]
    public void FailedPinShowsToastAndKeepsPaletteOpen()
    {
        Directory.CreateDirectory(FilePath);
        var command = new ToggleFavoriteCommand(new FavoritesStore(FilePath), "game-id");

        var result = command.Invoke();

        AssertToastKeepsPaletteOpen(result, "Favorites could not be changed. Check the local preferences file.");
    }

    [Fact]
    public void RefreshFailureReportsSuccessfulChangeAndKeepsPaletteOpen()
    {
        var store = new FavoritesStore(FilePath);
        var command = new ToggleFavoriteCommand(
            store,
            "game-id",
            () => throw new InvalidOperationException("Refresh failed."));

        var result = command.Invoke();

        Assert.True(store.IsFavorite("game-id"));
        AssertToastKeepsPaletteOpen(
            result,
            "Game pinned to Favorites, but the list could not refresh.");
    }

    private static void AssertToastKeepsPaletteOpen(CommandResult result, string expectedMessage)
    {
        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        var toast = Assert.IsType<ToastArgs>(result.Args);
        Assert.Equal(expectedMessage, toast.Message);
        var followUp = Assert.IsType<CommandResult>(toast.Result);
        Assert.Equal(CommandResultKind.KeepOpen, followUp.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
