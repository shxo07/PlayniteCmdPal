namespace PlayniteCmdPal.Core;

/// <summary>Exact-title installed-game matching policy for the global direct-launch fallback.</summary>
public static class InstalledGameMatch
{
    /// <summary>
    /// Returns the unique installed game whose title exactly matches the trimmed query,
    /// ignoring case. Identical repeated records count once; invalid IDs, invalid game
    /// records, or conflicting records for an ID fail closed. Uninstalled games do not
    /// appear and do not make an installed same-title game ambiguous.
    /// </summary>
    public static GameEntry? Find(IEnumerable<GameEntry> catalog, string? query) =>
        DirectLaunchGames.FindInstalledExactTitle(catalog, query);
}
