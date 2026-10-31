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
        _commands = [
            new CommandItem(new PlayniteCmdPalPage()) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
