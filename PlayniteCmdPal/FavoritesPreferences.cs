using System;
using System.IO;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal static class FavoritesPreferences
{
    private static readonly Lazy<FavoritesStore> SharedStore = new(() =>
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlayniteCmdPal");
        return new FavoritesStore(Path.Combine(directory, "favorites-v1.json"));
    });

    public static FavoritesStore Store => SharedStore.Value;
}
