using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace PlayniteCmdPal.Snapshot;

/// <summary>Writes allowlisted catalog fields for the requested snapshot schema without depending on System.Text.Json in the Playnite host.</summary>
public static class CatalogSnapshotSerializer
{
    public static async Task SerializeAsync(Stream destination, CatalogSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Games.Count > SnapshotLimits.MaxGames) throw new InvalidDataException("The snapshot contains too many games.");
        cancellationToken.ThrowIfCancellationRequested();

        using var limited = new LimitedOutputStream(destination, cancellationToken);
        var writer = JsonReaderWriterFactory.CreateJsonWriter(limited, Encoding.UTF8, ownsStream: false);
        try
        {
            writer.WriteStartElement("root");
            writer.WriteAttributeString("type", "object");
            WriteNumber(writer, "schemaVersion", snapshot.SchemaVersion);
            WriteString(writer, "generatedAtUtc", snapshot.GeneratedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
            writer.WriteStartElement("games");
            writer.WriteAttributeString("type", "array");
            foreach (var game in snapshot.Games)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteStartElement("item");
                writer.WriteAttributeString("type", "object");
                WriteString(writer, "id", game.Id, cancellationToken);
                WriteString(writer, "name", game.Name, cancellationToken);
                WriteBoolean(writer, "isInstalled", game.IsInstalled);
                if (game.Source is not null) WriteString(writer, "source", game.Source, cancellationToken);
                if (game.Platform is not null) WriteString(writer, "platform", game.Platform, cancellationToken);
                if (snapshot.SchemaVersion >= SnapshotLimits.SchemaVersion)
                {
                    if (game.CoverArtworkId is not null) WriteString(writer, "coverArtworkId", game.CoverArtworkId, cancellationToken);
                    if (game.IconArtworkId is not null) WriteString(writer, "iconArtworkId", game.IconArtworkId, cancellationToken);
                    if (game.LastActivity is { } lastActivity)
                        WriteString(writer, "lastActivity", lastActivity.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();
        }
        catch
        {
            try { writer.Dispose(); } catch { }
            throw;
        }
        writer.Dispose();
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static Task SerializeAsync(
        Stream destination,
        CatalogSnapshot snapshot,
        CancellationToken cancellationToken = default,
        int schemaVersion = SnapshotLimits.LegacySchemaVersion,
        bool includeLastActivity = false)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        var games = snapshot.Games.Select(game => new SnapshotGame(
            game.Id, game.Name, game.IsInstalled, game.Source, game.Platform,
            schemaVersion >= SnapshotLimits.SchemaVersion ? game.CoverArtworkId : null,
            schemaVersion >= SnapshotLimits.SchemaVersion ? game.IconArtworkId : null,
            includeLastActivity && schemaVersion >= SnapshotLimits.SchemaVersion ? game.LastActivity : null)).ToArray();
        var versioned = new CatalogSnapshot(snapshot.GeneratedAtUtc, games, schemaVersion);
        return SerializeAsync(destination, versioned, cancellationToken);
    }

    private static void WriteString(XmlDictionaryWriter writer, string name, string value, CancellationToken cancellationToken)
    {
        if (value.Length > SnapshotLimits.MaxBytes) throw new InvalidDataException("The serialized snapshot exceeds the 32 MiB limit.");
        writer.WriteStartElement(name);
        writer.WriteString(ReplaceInvalidSurrogates(value, cancellationToken));
        writer.WriteEndElement();
    }

    private static void WriteNumber(XmlDictionaryWriter writer, string name, int value)
    {
        writer.WriteStartElement(name);
        writer.WriteAttributeString("type", "number");
        writer.WriteValue(value);
        writer.WriteEndElement();
    }

    private static void WriteBoolean(XmlDictionaryWriter writer, string name, bool value)
    {
        writer.WriteStartElement(name);
        writer.WriteAttributeString("type", "boolean");
        writer.WriteValue(value);
        writer.WriteEndElement();
    }

    private static string ReplaceInvalidSurrogates(string value, CancellationToken cancellationToken)
    {
        StringBuilder? repaired = null;
        var nextCancellationCheck = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (i >= nextCancellationCheck) { cancellationToken.ThrowIfCancellationRequested(); nextCancellationCheck = i + 4096; }
            var current = value[i];
            if (char.IsHighSurrogate(current) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                if (repaired is not null) { repaired.Append(current); repaired.Append(value[i + 1]); }
                i++;
            }
            else if (char.IsSurrogate(current))
            {
                if (repaired is null) { repaired = new StringBuilder(value.Length); repaired.Append(value, 0, i); }
                repaired.Append('\uFFFD');
            }
            else if (repaired is not null) repaired.Append(current);
        }
        return repaired?.ToString() ?? value;
    }

    private sealed class LimitedOutputStream : Stream
    {
        private readonly Stream _destination;
        private readonly CancellationToken _cancellationToken;
        private long _written;
        public LimitedOutputStream(Stream destination, CancellationToken cancellationToken) { _destination = destination; _cancellationToken = cancellationToken; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _destination.CanWrite;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }
        public override void Flush() { _cancellationToken.ThrowIfCancellationRequested(); _destination.Flush(); }
        public override void Write(byte[] buffer, int offset, int count)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (count > SnapshotLimits.MaxBytes - _written) throw new InvalidDataException("The serialized snapshot exceeds the 32 MiB limit.");
            _destination.Write(buffer, offset, count);
            _written += count;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}