namespace PlayniteCmdPal.Core;

/// <summary>Creates the documented Playnite launch URI from a canonical stable game GUID.</summary>
public static class PlayniteLaunchUri
{
    /// <summary>
    /// Returns the Playnite URI only for a non-empty, lowercase canonical D-format GUID.
    /// Game paths and display titles are never accepted as launch identifiers.
    /// </summary>
    public static bool TryCreate(string? gameId, out Uri? launchUri)
    {
        launchUri = null;
        if (!Guid.TryParseExact(gameId, "D", out var parsedId) ||
            parsedId == Guid.Empty ||
            !string.Equals(gameId, parsedId.ToString("D"), StringComparison.Ordinal))
        {
            return false;
        }

        return Uri.TryCreate($"playnite://playnite/start/{parsedId:D}", UriKind.Absolute, out launchUri);
    }
}
