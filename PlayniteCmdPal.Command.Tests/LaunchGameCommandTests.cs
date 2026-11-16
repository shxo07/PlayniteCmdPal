using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using PlayniteCmdPal;
using Xunit;

namespace PlayniteCmdPal.Command.Tests;

public sealed class LaunchGameCommandTests
{
    private const string GameId = "11111111-1111-4111-8111-111111111111";

    [Fact]
    public void LaunchUsesTheExactStableIdThroughTheExistingShellUriPath()
    {
        ProcessStartInfo? dispatched = null;
        var command = new LaunchGameCommand(GameId, startInfo => dispatched = startInfo);

        var result = command.Invoke();

        Assert.Equal(CommandResultKind.Dismiss, result.Kind);
        Assert.NotNull(dispatched);
        Assert.Equal($"playnite://playnite/start/{GameId}", dispatched.FileName);
        Assert.True(dispatched.UseShellExecute);
        Assert.Empty(dispatched.Arguments);
    }

    [Fact]
    public void InvalidIdentityNeverReachesWindows()
    {
        var dispatched = false;
        var result = new LaunchGameCommand("Orbit Rally 16", _ => dispatched = true).Invoke();

        Assert.False(dispatched);
        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        Assert.Contains("invalid Playnite game ID", Assert.IsAssignableFrom<IToastArgs>(result.Args).Message);
    }

    [Fact]
    public void LaunchFailureReturnsActionableFeedbackRatherThanSuccess()
    {
        var result = new LaunchGameCommand(GameId, _ => throw new Win32Exception("Synthetic missing URI handler")).Invoke();

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        var toast = Assert.IsAssignableFrom<IToastArgs>(result.Args);
        Assert.Contains("Windows couldn't open Playnite", toast.Message);
        Assert.Contains("link handler", toast.Message);
    }
}
