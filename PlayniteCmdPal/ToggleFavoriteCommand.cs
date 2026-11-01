using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal sealed partial class ToggleFavoriteCommand : InvokableCommand
{
    private readonly FavoritesStore _store;
    private readonly string _gameId;
    private readonly Action? _onChanged;

    public ToggleFavoriteCommand(FavoritesStore store, string gameId, Action? onChanged = null)
    {
        _store = store;
        _gameId = gameId;
        _onChanged = onChanged;
    }

    // Command item construction reads Name. Keep an unreadable or future-version preferences
    // file from breaking construction of otherwise valid game search results.
    public override string Name
    {
        get
        {
            try
            {
                return _store.IsFavorite(_gameId) ? "Unpin from favorites" : "Pin to favorites";
            }
            catch (Exception)
            {
                return "Favorites unavailable";
            }
        }
    }

    public override CommandResult Invoke()
    {
        try
        {
            var isFavorite = _store.Toggle(_gameId);
            _onChanged?.Invoke();
            return ShowToast(isFavorite ? "Game pinned to Favorites." : "Game removed from Favorites.");
        }
        catch (Exception)
        {
            return ShowToast("Favorites could not be changed. Check the local preferences file.");
        }
    }

    private static CommandResult ShowToast(string message)
    {
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = message,
            Result = CommandResult.KeepOpen(),
        });
    }
}
