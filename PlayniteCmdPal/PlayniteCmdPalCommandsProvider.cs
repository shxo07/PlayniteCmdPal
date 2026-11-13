using System;
using System.Linq;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Playnite;

namespace PlayniteCmdPal;

public partial class PlayniteCmdPalCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _navigationCommands;
    private readonly IFallbackCommandItem[] _fallbackCommands;
    private readonly PlayniteGameFallback _gameFallback;
    private int _disposed;

    // The pinned SDK has a query-aware fallback callback but no provider-level search
    // injection hook. Keep the ordinary root list small/static and expose one dynamic,
    // query-specific installed-game fallback instead of registering every game at root.
    internal PlayniteCmdPalCommandsProvider(
        IReadOnlyPlayniteCatalog catalog,
        ICommandItem[] navigationCommands,
        ArtworkIconLoader? artwork = null,
        Func<string, CommandResult>? launch = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(navigationCommands);
        DisplayName = "Playnite";
        _navigationCommands = navigationCommands.ToArray();
        _gameFallback = new PlayniteGameFallback(catalog, launch, artwork);
        _fallbackCommands = [_gameFallback];
    }

    public override ICommandItem[] TopLevelCommands() => _navigationCommands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;

    public override ICommandItem? GetCommandItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _navigationCommands.FirstOrDefault(item => item.Command?.Id == id);
    }

    public override ICommand? GetCommand(string id) => GetCommandItem(id)?.Command;

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _gameFallback.Dispose();
        foreach (var item in _navigationCommands)
        {
            if (item.Command is IDisposable disposable) disposable.Dispose();
        }
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
