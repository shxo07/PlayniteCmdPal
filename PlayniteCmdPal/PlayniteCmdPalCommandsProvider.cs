// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See third-party/Microsoft-PowerToys-LICENSE.txt for the license text.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PlayniteCmdPal;

public partial class PlayniteCmdPalCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbacks;
    private readonly PlayniteGameFallback _gameFallback;
    private readonly PlayniteCmdPalRecentlyPlayedPage _recentlyPlayedPage;

    public PlayniteCmdPalCommandsProvider()
    {
        DisplayName = "Playnite";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _recentlyPlayedPage = new PlayniteCmdPalRecentlyPlayedPage();
        _gameFallback = new PlayniteGameFallback();
        _fallbacks = [_gameFallback];
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

    public override IFallbackCommandItem[] FallbackCommands() => _fallbacks;

    public override void Dispose()
    {
        _gameFallback.Dispose();
        _recentlyPlayedPage.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}
