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
        var page = new PlayniteCmdPalPage();
        return
        [
            new CommandItem(page)
            {
                Title = "Playnite",
                MoreCommands = [page.CreateRefreshContextItem()],
            },
        ];
    }
}
