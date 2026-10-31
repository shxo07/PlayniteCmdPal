namespace PlayniteCmdPal.Snapshot;

/// <summary>Version 1 of the read-only catalog exchanged with the Playnite companion plugin.</summary>
public sealed class CatalogSnapshot
{
    public CatalogSnapshot(DateTimeOffset generatedAtUtc, IReadOnlyList<SnapshotGame> games)
    {
        if (generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The snapshot timestamp must be UTC.", nameof(generatedAtUtc));
        }

        if (games is null) throw new ArgumentNullException(nameof(games));
        if (games.Count > SnapshotLimits.MaxGames)
        {
            throw new ArgumentException("The snapshot contains too many games.", nameof(games));
        }

        var copy = new SnapshotGame[games.Count];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < games.Count; i++)
        {
            var game = games[i] ?? throw new ArgumentException("Snapshot games cannot contain null records.", nameof(games));
            if (!ids.Add(game.Id))
            {
                throw new ArgumentException("Snapshot game IDs must be unique.", nameof(games));
            }

            copy[i] = game;
        }

        GeneratedAtUtc = generatedAtUtc;
        Games = Array.AsReadOnly(copy);
    }

    public int SchemaVersion => SnapshotLimits.SchemaVersion;
    public DateTimeOffset GeneratedAtUtc { get; }
    public IReadOnlyList<SnapshotGame> Games { get; }
}

/// <summary>The allowlisted fields exported for one Playnite game. Identity is never its name.</summary>
public sealed class SnapshotGame
{
    public SnapshotGame(string id, string name, bool isInstalled, string? source = null, string? platform = null)
    {
        if (!SnapshotValidation.IsCanonicalGameId(id))
        {
            throw new ArgumentException("A canonical Playnite GUID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A game name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        IsInstalled = isInstalled;
        Source = NormalizeOptional(source);
        Platform = NormalizeOptional(platform);
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsInstalled { get; }
    public string? Source { get; }
    public string? Platform { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class SnapshotLimits
{
    public const int SchemaVersion = 1;
    public const int MaxBytes = 32 * 1024 * 1024;
    public const int MaxGames = 100_000;
    public const int MaxJsonDepth = 16;
}

public static class SnapshotValidation
{
    public static bool IsCanonicalGameId(string? value) =>
        Guid.TryParseExact(value, "D", out var id) &&
        id != Guid.Empty &&
        string.Equals(value, id.ToString("D"), StringComparison.Ordinal);
}
