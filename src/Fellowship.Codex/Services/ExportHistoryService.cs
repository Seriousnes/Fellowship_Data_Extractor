using System.Text.Json;
using System.Text.Json.Nodes;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Services;

public sealed record VersionValue(string Build, JsonNode? Value);

public interface IExportHistoryService
{
    IReadOnlyList<string> Builds { get; }

    IReadOnlyList<VersionValue> Of(EntityDocument document);
}

public sealed class ExportHistoryService : IExportHistoryService
{
    private static readonly string[] Variant =
    [
        Named(nameof(ItemDocument.ArmorType)),
        Named(nameof(ItemDocument.PrimaryStat)),
        Named(nameof(ItemDocument.Hero)),
        Named(nameof(ItemDocument.Heroes)),
    ];

    private readonly ExportHistory history;

    public IReadOnlyList<string> Builds { get; }

    public ExportHistoryService(ExportHistory history)
    {
        this.history = history;

        Builds =
        [
            history.Latest,
            .. history.Older.Keys
                .OrderByDescending(ExportFolders.Number)
                .ThenByDescending(build => build, StringComparer.Ordinal),
        ];
    }

    public IReadOnlyList<VersionValue> Of(EntityDocument document)
    {
        var live = JsonSerializer.SerializeToNode(document, EntityDocuments.Options);
        var key = new DocumentKey(EntityTypes.Slug(EntityTypes.TypeOf(document)), document.Id);

        var readings = new List<VersionValue>(Builds.Count) { new(history.Latest, live) };

        foreach (var build in Builds.Skip(1))
        {
            readings.Add(new VersionValue(build, Matching(history.Older[build], key, live)));
        }

        return readings;
    }

    private static JsonNode? Matching(ExportVersion version, DocumentKey key, JsonNode? live)
    {
        if (!version.Documents.TryGetValue(key, out var lines) || lines.Count == 0) return null;

        if (lines.Count == 1) return JsonNode.Parse(lines[0]);

        foreach (var line in lines)
        {
            var reading = JsonNode.Parse(line);

            if (Variant.All(field => JsonNode.DeepEquals(Field(reading, field), Field(live, field))))
            {
                return reading;
            }
        }

        return null;
    }

    private static JsonNode? Field(JsonNode? node, string name) =>
        node is JsonObject carried && carried.TryGetPropertyValue(name, out var value) ? value : null;

    private static string Named(string property) =>
        EntityDocuments.Options.PropertyNamingPolicy!.ConvertName(property);
}
