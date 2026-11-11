// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See third-party/Microsoft-PowerToys-LICENSE.txt for the license text.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PlayniteCmdPal;

public partial class PlayniteCmdPalCommandsProvider : CommandProvider, IDisposable
{
    private readonly ICommandItem[] _commands;
    private readonly PlayniteCmdPalRecentlyPlayedPage _recentlyPlayedPage;

    public PlayniteCmdPalCommandsProvider()
    {
        DisplayName = "Playnite";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _recentlyPlayedPage = new PlayniteCmdPalRecentlyPlayedPage();
        _commands = [
            new CommandItem(new PlayniteCmdPalPage()) { Title = "Search games" },
            new CommandItem(new FavoritesPage()) { Title = "Favorites" },
            new CommandItem(_recentlyPlayedPage)
            {
                Title = "Recently played games",
                MoreCommands = [_recentlyPlayedPage.CreateRefreshContextItem()],
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public void Dispose() => _recentlyPlayedPage.Dispose();

}
