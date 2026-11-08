namespace PlayniteCmdPal.Snapshot;

/// <summary>Versioned read-only catalog exchanged with the Playnite companion plugin.</summary>
public sealed class CatalogSnapshot
{
    public CatalogSnapshot(DateTimeOffset generatedAtUtc, IReadOnlyList<SnapshotGame> games, int schemaVersion = SnapshotLimits.LegacySchemaVersion)
    {
        if (generatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("The snapshot timestamp must be UTC.", nameof(generatedAtUtc));
        if (games is null) throw new ArgumentNullException(nameof(games));
        if (schemaVersion != SnapshotLimits.LegacySchemaVersion && schemaVersion != SnapshotLimits.SchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Only catalog schema versions 1 and 2 are supported.");
        if (games.Count > SnapshotLimits.MaxGames) throw new ArgumentException("The snapshot contains too many games.", nameof(games));

        var copy = new SnapshotGame[games.Count];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < games.Count; i++)
        {
            var game = games[i] ?? throw new ArgumentException("Snapshot games cannot contain null records.", nameof(games));
            if (!ids.Add(game.Id)) throw new ArgumentException("Snapshot game IDs must be unique.", nameof(games));
            if (schemaVersion == SnapshotLimits.LegacySchemaVersion && (game.CoverArtworkId is not null || game.IconArtworkId is not null))
                throw new ArgumentException("Schema v1 cannot contain artwork references.", nameof(games));
            copy[i] = game;
        }

        SchemaVersion = schemaVersion;
        GeneratedAtUtc = generatedAtUtc;
        Games = Array.AsReadOnly(copy);
    }

    public int SchemaVersion { get; }
    public DateTimeOffset GeneratedAtUtc { get; }
    public IReadOnlyList<SnapshotGame> Games { get; }
}

public sealed class SnapshotGame
{
    public SnapshotGame(string id, string name, bool isInstalled, string? source = null, string? platform = null,
        string? coverArtworkId = null, string? iconArtworkId = null)
    {
        if (!SnapshotValidation.IsCanonicalGameId(id)) throw new ArgumentException("A canonical Playnite GUID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A game name is required.", nameof(name));
        if (!SnapshotValidation.IsArtworkIdOrNull(coverArtworkId)) throw new ArgumentException("Cover artwork ID must be a lowercase SHA-256 digest.", nameof(coverArtworkId));
        if (!SnapshotValidation.IsArtworkIdOrNull(iconArtworkId)) throw new ArgumentException("Icon artwork ID must be a lowercase SHA-256 digest.", nameof(iconArtworkId));
        Id = id;
        Name = name.Trim();
        IsInstalled = isInstalled;
        Source = NormalizeOptional(source);
        Platform = NormalizeOptional(platform);
        CoverArtworkId = coverArtworkId;
        IconArtworkId = iconArtworkId;
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsInstalled { get; }
    public string? Source { get; }
    public string? Platform { get; }
    public string? CoverArtworkId { get; }
    public string? IconArtworkId { get; }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class SnapshotLimits
{
    public const int LegacySchemaVersion = 1;
    public const int SchemaVersion = 2;
    public const int MaxBytes = 32 * 1024 * 1024;
    public const int MaxGames = 100_000;
    public const int MaxJsonDepth = 16;
    public const int MaxArtworkBytes = 5 * 1024 * 1024;
    public const long MaxArtworkCacheBytes = 256L * 1024 * 1024;
}

public static class SnapshotValidation
{
    public static bool IsCanonicalGameId(string? value) =>
        Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty &&
        string.Equals(value, id.ToString("D"), StringComparison.Ordinal);

    public static bool IsArtworkIdOrNull(string? value) =>
        value is null || (value.Length == 64 && value.All(c =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
}