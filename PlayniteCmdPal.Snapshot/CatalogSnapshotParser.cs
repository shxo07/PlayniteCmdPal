using System.Globalization;
using System.Text.Json;

namespace PlayniteCmdPal.Snapshot;

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

/// <summary>Strict, bounded parser for the v1 snapshot contract. It never touches Playnite storage.</summary>
public static class CatalogSnapshotParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = SnapshotLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    // Accept ISO-8601 UTC with or without fractional seconds. A non-zero offset is still rejected below.
    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    };

    public static CatalogSnapshotParseResult Parse(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.Length > SnapshotLimits.MaxBytes)
        {
            return CatalogSnapshotParseResult.Failure(CatalogErrorCode.TooLarge, "The Playnite catalog snapshot exceeds the 32 MiB limit.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json, DocumentOptions);
        }
        catch (JsonException)
        {
            return CatalogSnapshotParseResult.Failure(CatalogErrorCode.Malformed, "The Playnite catalog snapshot is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root) ||
                HasUnexpectedProperties(root, "schemaVersion", "generatedAtUtc", "games"))
            {
                return InvalidEnvelope();
            }

            if (!TryGetProperty(root, "schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var schemaVersion))
            {
                return InvalidEnvelope();
            }

            if (schemaVersion != SnapshotLimits.SchemaVersion)
            {
                return CatalogSnapshotParseResult.Failure(CatalogErrorCode.UnsupportedSchema, "The Playnite catalog snapshot uses an unsupported schema version.");
            }

            if (!TryGetProperty(root, "generatedAtUtc", out var timestamp) ||
                timestamp.ValueKind != JsonValueKind.String ||
                !TryParseTimestamp(timestamp.GetString(), out var generatedAt))
            {
                return InvalidEnvelope();
            }

            if (!TryGetProperty(root, "games", out var gamesElement) || gamesElement.ValueKind != JsonValueKind.Array)
            {
                return InvalidEnvelope();
            }

            var gameCount = gamesElement.GetArrayLength();
            if (gameCount > SnapshotLimits.MaxGames)
            {
                return CatalogSnapshotParseResult.Failure(CatalogErrorCode.TooManyGames, "The Playnite catalog snapshot exceeds the 100,000 game limit.");
            }

            var games = new List<SnapshotGame>(gameCount);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var skipped = 0;
            foreach (var element in gamesElement.EnumerateArray())
            {
                if (TryParseGame(element, out var game) && ids.Add(game!.Id))
                {
                    games.Add(game);
                }
                else
                {
                    skipped++;
                }
            }

            return new CatalogSnapshotParseResult(new CatalogSnapshot(generatedAt, games), null, skipped);
        }
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset generatedAt)
    {
        generatedAt = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value!;
        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text.Substring(0, text.Length - 1) + "+00:00";
        }

        return DateTimeOffset.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out generatedAt) &&
               generatedAt.Offset == TimeSpan.Zero;
    }

    private static bool TryParseGame(JsonElement element, out SnapshotGame? game)
    {
        game = null;
        if (element.ValueKind != JsonValueKind.Object || HasDuplicateProperties(element) ||
            HasUnexpectedProperties(element, "id", "name", "isInstalled", "source", "platform"))
        {
            return false;
        }

        if (!TryGetProperty(element, "id", out var id) || id.ValueKind != JsonValueKind.String ||
            !TryGetProperty(element, "name", out var name) || name.ValueKind != JsonValueKind.String ||
            !TryGetProperty(element, "isInstalled", out var installed) ||
            (installed.ValueKind != JsonValueKind.True && installed.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        string? source = null;
        if (TryGetProperty(element, "source", out var sourceElement) && !TryReadOptionalString(sourceElement, ref source))
        {
            return false;
        }

        string? platform = null;
        if (TryGetProperty(element, "platform", out var platformElement) && !TryReadOptionalString(platformElement, ref platform))
        {
            return false;
        }

        try
        {
            game = new SnapshotGame(id.GetString()!, name.GetString()!, installed.GetBoolean(), source, platform);
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
            var found = false;
            foreach (var name in allowed)
            {
                if (property.NameEquals(name))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
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
        CatalogSnapshotParseResult.Failure(CatalogErrorCode.Malformed, "The Playnite catalog snapshot has an invalid v1 envelope.");
}
