// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See third-party/Microsoft-PowerToys-LICENSE.txt for the license text.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PlayniteCmdPal;

public partial class PlayniteCmdPalCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public PlayniteCmdPalCommandsProvider()
    {
        DisplayName = "Playnite";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        var recentlyPlayedPage = new PlayniteCmdPalRecentlyPlayedPage();
        _commands = [
            new CommandItem(new PlayniteCmdPalPage()) { Title = "Search games" },
            new CommandItem(new FavoritesPage()) { Title = "Favorites" },
            new CommandItem(recentlyPlayedPage)
            {
                Title = "Recently played games",
                MoreCommands = [new CommandContextItem(new RefreshRecentlyPlayedCommand(recentlyPlayedPage))],
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    private sealed class RefreshRecentlyPlayedCommand : InvokableCommand
    {
        private readonly PlayniteCmdPalRecentlyPlayedPage _page;

        public RefreshRecentlyPlayedCommand(PlayniteCmdPalRecentlyPlayedPage page)
        {
            _page = page;
            Name = "Refresh Playnite catalog";
        }

        public override CommandResult Invoke()
        {
            _page.RefreshCatalog();
            return CommandResult.KeepOpen();
        }
    }
}
