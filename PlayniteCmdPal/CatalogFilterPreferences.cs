using System;
using System.IO;
using System.Text.Json;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal static class CatalogFilterPreferences
{
    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlayniteCmdPal",
        "catalog-filters-v1.json");

    private static readonly CatalogFilterSelection DefaultSelection = new([true], [], []);

    public static CatalogFilterSelection Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
                return DefaultSelection;

            return JsonSerializer.Deserialize<CatalogFilterSelection>(File.ReadAllText(PreferencesPath))
                ?? DefaultSelection;
        }
        catch (Exception)
        {
            // A damaged preferences file must never prevent the library page from opening.
            return DefaultSelection;
        }
    }

    public static void Save(CatalogFilterSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        string? temporaryPath = null;
        try
        {
            var directory = Path.GetDirectoryName(PreferencesPath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = PreferencesPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(selection));
            File.Move(temporaryPath, PreferencesPath, overwrite: true);
        }
        catch (Exception)
        {
            // Filter persistence is best-effort; it must not break filtering or navigation.
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception) { }
            }
        }
    }
}
