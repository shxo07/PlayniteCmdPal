using System;
using System.Linq;

namespace PlayniteCmdPal.Snapshot;

/// <summary>Validation for Playnite's database-managed relative artwork references.</summary>
public static class ArtworkReferenceValidator
{
    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    public static bool TryValidate(string gameId, string? reference, out string fileName)
    {
        fileName = string.Empty;
        if (!SnapshotValidation.IsCanonicalGameId(gameId) || string.IsNullOrWhiteSpace(reference) ||
            reference.IndexOf(':') >= 0)
        {
            return false;
        }

        var normalized = reference.Replace('/', '\\');
        var parts = normalized.Split('\\');
        if (parts.Length != 2 || !string.Equals(parts[0], gameId, StringComparison.Ordinal))
        {
            return false;
        }

        var dot = parts[1].LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        var fileId = parts[1].Substring(0, dot);
        var extension = parts[1].Substring(dot).ToLowerInvariant();
        if (!Guid.TryParseExact(fileId, "D", out var parsed) ||
            !string.Equals(fileId, parsed.ToString("D"), StringComparison.Ordinal) ||
            !AllowedExtensions.Contains(extension, StringComparer.Ordinal))
        {
            return false;
        }

        fileName = parts[1];
        return true;
    }
}

/// <summary>Checks bounded image containers using signatures and terminal markers only.</summary>
public static class ArtworkFileSignature
{
    public static bool TryGetExtension(byte[] data, out string extension)
    {
        extension = string.Empty;
        if (data is null)
        {
            return false;
        }

        if (data.Length >= 24 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A &&
            data[data.Length - 8] == 0x49 && data[data.Length - 7] == 0x45 &&
            data[data.Length - 6] == 0x4E && data[data.Length - 5] == 0x44)
        {
            extension = ".png";
            return true;
        }

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8 &&
            data[data.Length - 2] == 0xFF && data[data.Length - 1] == 0xD9)
        {
            extension = ".jpg";
            return true;
        }

        if (data.Length >= 14 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[data.Length - 1] == 0x3B)
        {
            extension = ".gif";
            return true;
        }

        if (data.Length >= 16 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
            data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P' &&
            BitConverter.ToUInt32(data, 4) == data.Length - 8)
        {
            extension = ".webp";
            return true;
        }

        return false;
    }
}