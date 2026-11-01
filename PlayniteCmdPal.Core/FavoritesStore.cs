using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayniteCmdPal.Core;

/// <summary>
/// Stores extension-owned favorite game IDs in a versioned JSON file. The store never accesses
/// Playnite data and replaces the file through a same-directory temporary file.
/// </summary>
public sealed partial class FavoritesStore
{
    public const int CurrentSchemaVersion = 1;
    private readonly string _filePath;
    private readonly object _gate = new();
    private HashSet<string>? _favoriteIds;

    public FavoritesStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    /// <summary>Raised after a favorite has been durably added or removed.</summary>
    public event EventHandler? FavoritesChanged;

    public IReadOnlyList<string> GetFavoriteIds()
    {
        lock (_gate)
        {
            EnsureLoaded();
            return _favoriteIds!.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }
    }

    public bool IsFavorite(string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        lock (_gate)
        {
            EnsureLoaded();
            return _favoriteIds!.Contains(gameId);
        }
    }

    /// <returns>True when the game is favorited after the operation.</returns>
    public bool Toggle(string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        bool isFavorite;
        lock (_gate)
        {
            EnsureLoaded();
            var wasFavorite = _favoriteIds!.Contains(gameId);
            if (wasFavorite)
            {
                _favoriteIds.Remove(gameId);
            }
            else
            {
                _favoriteIds.Add(gameId);
            }

            try
            {
                Persist();
            }
            catch
            {
                // Keep the in-memory view consistent with the last durable file contents.
                if (wasFavorite) _favoriteIds.Add(gameId);
                else _favoriteIds.Remove(gameId);
                throw;
            }

            isFavorite = !wasFavorite;
        }

        // Notify only after persistence succeeds and the store lock has been released.
        // Observer failures must not make a durable preference write look like a failed
        // command. Each page refresh is isolated from the command invocation.
        var handlers = FavoritesChanged;
        if (handlers is not null)
        {
            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch
                {
                    // The persisted preference remains authoritative; observers can retry.
                }
            }
        }

        return isFavorite;
    }

    private void EnsureLoaded()
    {
        if (_favoriteIds is not null)
        {
            return;
        }

        if (!File.Exists(_filePath))
        {
            _favoriteIds = new HashSet<string>(StringComparer.Ordinal);
            return;
        }

        try
        {
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var envelope = JsonDocument.Parse(stream);

            // Inspect the version before deserializing the v1 payload. A newer schema may
            // intentionally change favoriteGameIds' shape, and must never be mistaken for
            // corrupt v1 data (which could otherwise be overwritten on the next mutation).
            // JSON permits scalar and array roots as well as objects. Only object
            // documents can carry our schema envelope; recover every other root kind as
            // malformed preferences before calling TryGetProperty (which throws otherwise).
            if (envelope.RootElement.ValueKind != JsonValueKind.Object)
            {
                _favoriteIds = new HashSet<string>(StringComparer.Ordinal);
                return;
            }

            if (!envelope.RootElement.TryGetProperty("schemaVersion", out var schemaVersion)
                || !schemaVersion.TryGetInt32(out var version)
                || version < CurrentSchemaVersion)
            {
                _favoriteIds = new HashSet<string>(StringComparer.Ordinal);
                return;
            }

            if (version > CurrentSchemaVersion)
            {
                throw new InvalidDataException("The favorites preferences use an unsupported future schema version.");
            }

            stream.Position = 0;
            var document = JsonSerializer.Deserialize(stream, PreferencesJsonContext.Default.PreferencesDocument);
            if (document is null)
            {
                _favoriteIds = new HashSet<string>(StringComparer.Ordinal);
                return;
            }

            if (document.FavoriteGameIds is null)
            {
                _favoriteIds = new HashSet<string>(StringComparer.Ordinal);
                return;
            }

            _favoriteIds = document.FavoriteGameIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Treat malformed data as an empty preference set. The next user mutation writes
            // a fresh valid document atomically; the corrupt source is never partially parsed.
            _favoriteIds = new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The favorites file must have a parent directory.");
        Directory.CreateDirectory(directory);

        var document = new PreferencesDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            FavoriteGameIds = _favoriteIds!.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, PreferencesJsonContext.Default.PreferencesDocument);
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            // Both paths share a directory, so replacement stays on the same volume.
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed class PreferencesDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("favoriteGameIds")]
        public string[]? FavoriteGameIds { get; init; }
    }

    [JsonSerializable(typeof(PreferencesDocument))]
    private partial class PreferencesJsonContext : JsonSerializerContext
    {
    }
}
