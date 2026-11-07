namespace PlayniteCmdPal.Core;

/// <summary>Creates documented Playnite URIs from canonical stable game GUIDs.</summary>
public static class PlayniteLaunchUri
{
    /// <summary>
    /// Returns the Playnite launch URI only for a non-empty, lowercase canonical D-format GUID.
    /// Game paths and display titles are never accepted as launch identifiers.
    /// </summary>
    public static bool TryCreate(string? gameId, out Uri? launchUri) =>
        TryCreateForCommand(gameId, "start", out launchUri);

    /// <summary>
    /// Returns the Playnite game-details URI only for a non-empty, lowercase canonical D-format GUID.
    /// </summary>
    public static bool TryCreateShowGame(string? gameId, out Uri? detailsUri) =>
        TryCreateForCommand(gameId, "showgame", out detailsUri);

    private static bool TryCreateForCommand(string? gameId, string command, out Uri? uri)
    {
        uri = null;
        if (!Guid.TryParseExact(gameId, "D", out var parsedId) ||
            parsedId == Guid.Empty ||
            !string.Equals(gameId, parsedId.ToString("D"), StringComparison.Ordinal))
        {
            return false;
        }

        return Uri.TryCreate($"playnite://playnite/{command}/{parsedId:D}", UriKind.Absolute, out uri);
    }
}
