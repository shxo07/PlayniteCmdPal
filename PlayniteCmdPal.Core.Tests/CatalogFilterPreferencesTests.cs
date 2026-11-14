using PlayniteCmdPal;
using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class CatalogFilterPreferencesTests
{
    [Fact]
    public void SaveAndLoadRoundTripsEveryFilterDimension()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new CatalogFilterPreferencesStore(Path.Combine(directory, "filters.json"));
            var expected = new CatalogFilterSelection([false, true], ["Steam", "Epic"], ["PC", "Linux"]);

            Assert.True(store.Save(expected));

            var actual = store.Load();
            Assert.Equal(expected.InstalledStatuses, actual.InstalledStatuses);
            Assert.Equal(expected.Sources, actual.Sources);
            Assert.Equal(expected.Platforms, actual.Platforms);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingFileReturnsFirstRunInstalledDefault()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new CatalogFilterPreferencesStore(Path.Combine(directory, "missing.json"));

            var actual = store.Load();

            Assert.Equal(new[] { true }, actual.InstalledStatuses);
            Assert.Empty(actual.Sources);
            Assert.Empty(actual.Platforms);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CorruptFileFallsBackToInstalledDefault()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "filters.json");
            File.WriteAllText(path, "{ definitely not valid json");
            var store = new CatalogFilterPreferencesStore(path);

            var actual = store.Load();

            Assert.Equal(new[] { true }, actual.InstalledStatuses);
            Assert.Empty(actual.Sources);
            Assert.Empty(actual.Platforms);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PartialJsonNormalizesNullArraysWithoutLosingValidSelections()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "filters.json");
            File.WriteAllText(path, """{"InstalledStatuses":[false],"Sources":null,"Platforms":["PC"]}""");
            var store = new CatalogFilterPreferencesStore(path);

            var actual = store.Load();

            Assert.Equal(new[] { false }, actual.InstalledStatuses);
            Assert.Empty(actual.Sources);
            Assert.Equal(new[] { "PC" }, actual.Platforms);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FailedSaveReturnsFalseAndDoesNotThrow()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            // A directory at the destination path forces the atomic replacement to fail.
            var path = Path.Combine(directory, "filters.json");
            Directory.CreateDirectory(path);
            var store = new CatalogFilterPreferencesStore(path);

            Assert.False(store.Save(new CatalogFilterSelection([false], ["Steam"], ["PC"])));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
