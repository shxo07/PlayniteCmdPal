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
        var toastMessages = new List<string>();
        var command = new ToggleFavoriteCommand(
            new FavoritesStore(FilePath),
            "game-id",
            showToast: toastMessages.Add);

        var result = command.Invoke();

        AssertKeepOpen(result);
        Assert.Equal(new[] { "Game pinned to Favorites." }, toastMessages);
        Assert.True(new FavoritesStore(FilePath).IsFavorite("game-id"));
    }

    [Fact]
    public void FailedPinShowsToastAndKeepsPaletteOpen()
    {
        Directory.CreateDirectory(FilePath);
        var toastMessages = new List<string>();
        var command = new ToggleFavoriteCommand(
            new FavoritesStore(FilePath),
            "game-id",
            showToast: toastMessages.Add);

        var result = command.Invoke();

        AssertKeepOpen(result);
        Assert.Equal(new[] { "Favorites could not be changed. Check the local preferences file." }, toastMessages);
    }

    [Fact]
    public void RefreshFailureReportsSuccessfulChangeAndKeepsPaletteOpen()
    {
        var store = new FavoritesStore(FilePath);
        var toastMessages = new List<string>();
        var command = new ToggleFavoriteCommand(
            store,
            "game-id",
            () => throw new InvalidOperationException("Refresh failed."),
            toastMessages.Add);

        var result = command.Invoke();

        Assert.True(store.IsFavorite("game-id"));
        AssertKeepOpen(result);
        Assert.Equal(
            new[] { "Game pinned to Favorites, but the list could not refresh." },
            toastMessages);
    }

    private static void AssertKeepOpen(CommandResult result)
    {
        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
