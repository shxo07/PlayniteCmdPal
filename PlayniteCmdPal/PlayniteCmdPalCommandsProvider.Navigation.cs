// Copyright (c) Microsoft Corporation. All rights reserved.
// The Microsoft Corporation licenses this file to you under the MIT license.
// See third-party/Microsoft-PowerToys-LICENSE.txt for the license text.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

public partial class PlayniteCmdPalCommandsProvider
{
    public PlayniteCmdPalCommandsProvider()
        : this(new PlayniteCatalogAdapter(), CreateNavigationCommands())
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    private static ICommandItem[] CreateNavigationCommands()
    {
        var recentlyPlayed = new PlayniteCmdPalRecentlyPlayedPage();
        return [
            new CommandItem(new PlayniteCmdPalPage()) { Title = "Search games" },
            new CommandItem(new FavoritesPage()) { Title = "Favorites" },
            new CommandItem(recentlyPlayed)
            {
                Title = "Recently played games",
                MoreCommands = [recentlyPlayed.CreateRefreshContextItem()],
            },
        ];
    }
}
