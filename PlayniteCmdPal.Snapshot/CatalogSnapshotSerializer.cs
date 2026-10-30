using System.Text.Json;

namespace PlayniteCmdPal.Snapshot;

/// <summary>Writes only the allowlisted v1 fields; no SDK models or launch configuration are serialized.</summary>
public static class CatalogSnapshotSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false };

    public static async Task SerializeAsync(Stream destination, CatalogSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Games.Count > SnapshotLimits.MaxGames)
        {
            throw new InvalidDataException("The snapshot contains too many games.");
        }

        using var writer = new Utf8JsonWriter(destination, WriterOptions);
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", snapshot.SchemaVersion);
        writer.WriteString("generatedAtUtc", snapshot.GeneratedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteStartArray("games");
        foreach (var game in snapshot.Games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteStartObject();
            writer.WriteString("id", game.Id);
            writer.WriteString("name", game.Name);
            writer.WriteBoolean("isInstalled", game.IsInstalled);
            if (game.Source is not null)
            {
                writer.WriteString("source", game.Source);
            }

            if (game.Platform is not null)
            {
                writer.WriteString("platform", game.Platform);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
