using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal sealed partial class LaunchGameCommand : InvokableCommand
{
    private readonly string _gameId;

    public LaunchGameCommand(string gameId)
    {
        _gameId = gameId;
    }

    public override string Name => "Launch with Playnite";

    public override CommandResult Invoke()
    {
        if (!PlayniteLaunchUri.TryCreate(_gameId, out var launchUri) || launchUri is null)
        {
            return CommandResult.ShowToast("This catalog entry has an invalid Playnite game ID. Refresh the catalog and try again.");
        }

        try
        {
            // Let Playnite resolve the game and apply its configured launch actions.
            // Never execute an executable path from catalog data.
            Process.Start(new ProcessStartInfo(launchUri.AbsoluteUri)
            {
                UseShellExecute = true,
            });

            return CommandResult.Dismiss();
        }
        catch (Exception)
        {
            return CommandResult.ShowToast("Windows couldn't open Playnite. Check that Playnite is installed and its playnite:// link handler is registered, then try again.");
        }
    }
}
