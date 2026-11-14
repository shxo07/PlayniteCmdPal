using System;
using System.IO;
using System.Text.Json;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal static class CatalogFilterPreferences
{
    private static readonly CatalogFilterPreferencesStore Store = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlayniteCmdPal",
        "catalog-filters-v1.json"));

    public static CatalogFilterSelection Load() => Store.Load();

    public static void Save(CatalogFilterSelection selection) => Store.Save(selection);
}

/// <summary>
/// File-backed preferences store with an injectable path so serialization, recovery,
/// and atomic replacement can be exercised without touching the user's profile.
/// </summary>
internal sealed class CatalogFilterPreferencesStore
{
    private static readonly CatalogFilterSelection DefaultSelection = new([true], [], []);
    private readonly string _path;

    public CatalogFilterPreferencesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public CatalogFilterSelection Load()
    {
        try
        {
            if (!File.Exists(_path))
                return CloneDefault();

            var selection = JsonSerializer.Deserialize<CatalogFilterSelection>(File.ReadAllText(_path));
            if (selection is null)
                return CloneDefault();

            // Normalize null arrays from older, partial, or hand-edited JSON documents.
            return new CatalogFilterSelection(
                selection.InstalledStatuses ?? [],
                selection.Sources ?? [],
                selection.Platforms ?? []);
        }
        catch (Exception) when (File.Exists(_path) || !Directory.Exists(Path.GetDirectoryName(_path)))
        {
            // A damaged or temporarily inaccessible preferences file must not prevent
            // the library page from opening.
            return CloneDefault();
        }
    }

    public bool Save(CatalogFilterSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        string? temporaryPath = null;
        try
        {
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("The preferences path has no parent directory.");
            Directory.CreateDirectory(directory);
            temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var normalized = new CatalogFilterSelection(
                selection.InstalledStatuses ?? [],
                selection.Sources ?? [],
                selection.Platforms ?? []);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized));
            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception) { }
            }

            return false;
        }
    }

    private static CatalogFilterSelection CloneDefault() =>
        new(DefaultSelection.InstalledStatuses.ToArray(), [], []);
}
