using System.Globalization;
using System.Text.Json;
using PlayniteCmdPal.Snapshot;

namespace PlayniteCmdPal.Playnite;

public enum CatalogErrorCode
{
    Missing,
    Malformed,
    UnsupportedSchema,
    TooLarge,
    TooManyGames,
    Unavailable,
}

public sealed class CatalogIssue
{
    public CatalogIssue(CatalogErrorCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public CatalogErrorCode Code { get; }
    public string Message { get; }
}

public sealed class CatalogSnapshotParseResult
{
    internal CatalogSnapshotParseResult(CatalogSnapshot? snapshot, CatalogIssue? issue, int skippedRecordCount)
    {
        Snapshot = snapshot;
        Issue = issue;
        SkippedRecordCount = skippedRecordCount;
    }

    public CatalogSnapshot? Snapshot { get; }
    public CatalogIssue? Issue { get; }
    public int SkippedRecordCount { get; }

    public static CatalogSnapshotParseResult Failure(CatalogErrorCode code, string message) =>
        new(null, new CatalogIssue(code, message), 0);
}

/// <summary>Strict bounded parser for immutable v1 and artwork-enabled v2 catalog contracts.</summary>
public static class CatalogSnapshotParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = SnapshotLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    };

    public static CatalogSnapshotParseResult Parse(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.Length > SnapshotLimits.MaxBytes)
        {
            return CatalogSnapshotParseResult.Failure(
                CatalogErrorCode.TooLarge,
                "The Playnite catalog snapshot exceeds the 32 MiB limit.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json, DocumentOptions);
        }
        catch (JsonException)
        {
            return CatalogSnapshotParseResult.Failure(
                CatalogErrorCode.Malformed,
                "The Playnite catalog snapshot is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                HasDuplicateProperties(root) ||
                HasUnexpectedProperties(root, "schemaVersion", "generatedAtUtc", "games"))
            {
                return InvalidEnvelope();
            }

            if (!TryGetProperty(root, "schemaVersion", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var schemaVersion))
            {
                return InvalidEnvelope();
            }

            if (schemaVersion != SnapshotLimits.LegacySchemaVersion &&
                schemaVersion != SnapshotLimits.SchemaVersion)
            {
                return CatalogSnapshotParseResult.Failure(
                    CatalogErrorCode.UnsupportedSchema,
                    "The Playnite catalog snapshot uses an unsupported schema version.");
            }

            if (!TryGetProperty(root, "generatedAtUtc", out var timestampElement) ||
                timestampElement.ValueKind != JsonValueKind.String ||
                !TryParseTimestamp(timestampElement.GetString(), out var generatedAtUtc))
            {
                return InvalidEnvelope();
            }

            if (!TryGetProperty(root, "games", out var gamesElement) ||
                gamesElement.ValueKind != JsonValueKind.Array)
            {
                return InvalidEnvelope();
            }

            var gameCount = gamesElement.GetArrayLength();
            if (gameCount > SnapshotLimits.MaxGames)
            {
                return CatalogSnapshotParseResult.Failure(
                    CatalogErrorCode.TooManyGames,
                    "The Playnite catalog snapshot exceeds the 100,000 game limit.");
            }

            var games = new List<SnapshotGame>(gameCount);
            var gameIds = new HashSet<string>(StringComparer.Ordinal);
            var skippedRecordCount = 0;
            foreach (var element in gamesElement.EnumerateArray())
            {
                if (TryParseGame(element, schemaVersion, out var game) && gameIds.Add(game!.Id))
                {
                    games.Add(game);
                }
                else
                {
                    skippedRecordCount++;
                }
            }

            return new CatalogSnapshotParseResult(
                new CatalogSnapshot(generatedAtUtc, games, schemaVersion),
                null,
                skippedRecordCount);
        }
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset generatedAtUtc)
    {
        generatedAtUtc = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var timestamp = value!;
        if (timestamp.EndsWith("Z", StringComparison.Ordinal))
        {
            timestamp = timestamp.Substring(0, timestamp.Length - 1) + "+00:00";
        }

        return DateTimeOffset.TryParseExact(
                   timestamp,
                   TimestampFormats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out generatedAtUtc) &&
               generatedAtUtc.Offset == TimeSpan.Zero;
    }

    private static bool TryParseGame(JsonElement element, int schemaVersion, out SnapshotGame? game)
    {
        game = null;
        var allowedProperties = schemaVersion == SnapshotLimits.LegacySchemaVersion
            ? new[] { "id", "name", "isInstalled", "source", "platform" }
            : new[] { "id", "name", "isInstalled", "source", "platform", "coverArtworkId", "iconArtworkId" };

        if (element.ValueKind != JsonValueKind.Object ||
            HasDuplicateProperties(element) ||
            HasUnexpectedProperties(element, allowedProperties))
        {
            return false;
        }

        if (!TryGetProperty(element, "id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.String ||
            !TryGetProperty(element, "name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            !TryGetProperty(element, "isInstalled", out var installedElement) ||
            (installedElement.ValueKind != JsonValueKind.True &&
             installedElement.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        string? source = null;
        string? platform = null;
        string? coverArtworkId = null;
        string? iconArtworkId = null;

        if (TryGetProperty(element, "source", out var sourceElement) &&
            !TryReadOptionalString(sourceElement, ref source))
        {
            return false;
        }

        if (TryGetProperty(element, "platform", out var platformElement) &&
            !TryReadOptionalString(platformElement, ref platform))
        {
            return false;
        }

        if (schemaVersion == SnapshotLimits.SchemaVersion)
        {
            if (TryGetProperty(element, "coverArtworkId", out var coverElement) &&
                !TryReadOptionalArtworkId(coverElement, ref coverArtworkId))
            {
                return false;
            }

            if (TryGetProperty(element, "iconArtworkId", out var iconElement) &&
                !TryReadOptionalArtworkId(iconElement, ref iconArtworkId))
            {
                return false;
            }
        }

        try
        {
            game = new SnapshotGame(
                idElement.GetString()!,
                nameElement.GetString()!,
                installedElement.GetBoolean(),
                source,
                platform,
                coverArtworkId,
                iconArtworkId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadOptionalString(JsonElement element, ref string? value)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static bool TryReadOptionalArtworkId(JsonElement element, ref string? value) =>
        TryReadOptionalString(element, ref value) && SnapshotValidation.IsArtworkIdOrNull(value);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        var found = false;
        foreach (var property in element.EnumerateObject())
        {
            if (!property.NameEquals(name))
            {
                continue;
            }

            if (found)
            {
                return false;
            }

            value = property.Value;
            found = true;
        }

        return found;
    }

    private static bool HasUnexpectedProperties(JsonElement element, params string[] allowed)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Any(name => property.NameEquals(name)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static CatalogSnapshotParseResult InvalidEnvelope() =>
        CatalogSnapshotParseResult.Failure(
            CatalogErrorCode.Malformed,
            "The Playnite catalog snapshot has an invalid envelope.");
}