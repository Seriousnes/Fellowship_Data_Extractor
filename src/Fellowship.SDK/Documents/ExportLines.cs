using System.Text.Json;

namespace Fellowship.SDK.Documents;

/// <summary>Reads an entities.jsonl held as UTF-8. Its lines are slices of the one buffer.</summary>
public static class ExportLines
{
    private const byte LineFeed = (byte)'\n';

    private const byte CarriageReturn = (byte)'\r';

    private static readonly string IdName = EntityDocuments.NamingPolicy.ConvertName(nameof(EntityDocument.Id));

    public static IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> Group(ReadOnlyMemory<byte> file)
    {
        var grouped = new Dictionary<DocumentKey, List<ReadOnlyMemory<byte>>>();

        foreach (var line in Lines(file))
        {
            if (Key(line) is not { } key) continue;

            if (!grouped.TryGetValue(key, out var group)) grouped[key] = group = [];

            group.Add(line);
        }

        return grouped.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<ReadOnlyMemory<byte>>)[.. entry.Value]);
    }

    private static IEnumerable<ReadOnlyMemory<byte>> Lines(ReadOnlyMemory<byte> file)
    {
        while (file.Length > 0)
        {
            var end = file.Span.IndexOf(LineFeed);
            var line = end < 0 ? file : file[..end];

            file = end < 0 ? ReadOnlyMemory<byte>.Empty : file[(end + 1)..];

            if (line.Length > 0 && line.Span[^1] == CarriageReturn) line = line[..^1];
            if (line.Length > 0) yield return line;
        }
    }

    private static DocumentKey? Key(ReadOnlyMemory<byte> line)
    {
        try
        {
            using var reading = JsonDocument.Parse(line);

            return reading.RootElement is { ValueKind: JsonValueKind.Object } root &&
                root.TryGetProperty(EntityDocuments.Discriminator, out var slug) &&
                slug.ValueKind == JsonValueKind.String &&
                root.TryGetProperty(IdName, out var id) &&
                id.TryGetInt32(out var number)
                ? new DocumentKey(slug.GetString()!, number)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
