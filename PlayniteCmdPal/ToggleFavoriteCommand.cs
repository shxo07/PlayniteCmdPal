using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal sealed partial class ToggleFavoriteCommand : InvokableCommand
{
    private readonly FavoritesStore _store;
    private readonly string _gameId;
    private readonly Action _onChanged;

    public ToggleFavoriteCommand(FavoritesStore store, string gameId, Action onChanged)
    {
        _store = store;
        _gameId = gameId;
        _onChanged = onChanged;
    }

    public override string Name => _store.IsFavorite(_gameId) ? "Unpin from favorites" : "Pin to favorites";

    public override CommandResult Invoke()
    {
        var isFavorite = _store.Toggle(_gameId);
        _onChanged();
        return CommandResult.ShowToast(isFavorite ? "Game pinned to Favorites." : "Game removed from Favorites.");
    }
}
