using System.Diagnostics;
using Xunit;

namespace PlayniteCmdPal.Playnite.Tests;

public sealed class ArtworkExportLockTests
{
    [Fact]
    public async Task ExclusiveLockFileIsReleasedWhenOwningProcessTerminates()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(Path.GetTempPath(), "PlayniteCmdPal.LockTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var lockPath = Path.Combine(directory, "artwork-v2.export.lock");
        var escapedPath = lockPath.Replace("'", "''");
        var command = "$stream = [System.IO.File]::Open('" + escapedPath +
            "', [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None); " +
            "[Console]::WriteLine('LOCKED'); [Threading.Thread]::Sleep([Threading.Timeout]::Infinite)";
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -Command \"" + command.Replace("\"", "\\\"") + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var owner = Process.Start(startInfo);
            Assert.NotNull(owner);
            var ready = await owner!.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal("LOCKED", ready);

            Assert.Throws<IOException>(() =>
            {
                using var competing = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            });

            owner.Kill(entireProcessTree: true);
            await owner.WaitForExitAsync();

            using var recovered = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Assert.True(recovered.CanWrite);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
