using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal sealed partial class ShowGameDetailsCommand : InvokableCommand
{
    private readonly string _gameId;

    public ShowGameDetailsCommand(string gameId)
    {
        _gameId = gameId;
    }

    public override string Name => "Show game details in Playnite";

    public override CommandResult Invoke()
    {
        if (!PlayniteLaunchUri.TryCreateShowGame(_gameId, out var detailsUri) || detailsUri is null)
        {
            return CommandResult.ShowToast("This catalog entry has an invalid Playnite game ID. Refresh the catalog and try again.");
        }

        try
        {
            // Playnite documents showgame as a URI command. Shell dispatch can start or reuse
            // Playnite, but it does not expose whether the details view finished navigating.
            using var process = Process.Start(new ProcessStartInfo(detailsUri.AbsoluteUri)
            {
                UseShellExecute = true,
            });

            return process is null
                ? CommandResult.ShowToast("Windows submitted the game-details URI, but did not return a process handle. Playnite navigation could not be confirmed.")
                : CommandResult.ShowToast("Windows accepted the game-details request. This confirms URI dispatch only, not that Playnite finished navigating to the game.");
        }
        catch (Exception)
        {
            return CommandResult.ShowToast("Windows couldn't dispatch the game-details request to Playnite. Check that Playnite is installed and its playnite:// link handler is registered, then try again.");
        }
    }
}
