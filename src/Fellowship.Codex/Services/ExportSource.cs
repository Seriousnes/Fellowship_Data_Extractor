using System.Text.Json;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Services;

public sealed record ExportSnapshot(SettingsFile Settings, IReadOnlyList<EntityDocument> Entities);

public readonly record struct DocumentKey(string Slug, int Id);

public sealed record ExportVersion(
    string Build,
    IReadOnlyDictionary<DocumentKey, IReadOnlyList<string>> Documents,
    string Settings);

public sealed record ExportHistory(
    string Latest,
    ExportSnapshot Snapshot,
    IReadOnlyDictionary<string, ExportVersion> Older);

public interface IExportSource
{
    Task<ExportHistory> LoadAsync(CancellationToken cancellationToken);
}

public sealed class ExportHistoryHolder
{
    private ExportHistory? loaded;

    public ExportHistory History
    {
        get => loaded ?? throw new InvalidOperationException(
            "The export history is not loaded yet. Program.cs awaits IExportSource.LoadAsync after " +
            "builder.Build() and before app.Run(), so everything the store is built from reads a loaded one.");
        set => loaded = value;
    }
}

public static class ExportReader
{
    public const string DataFolder = "data";

    public const string EntitiesName = "entities.jsonl";

    public const string SettingsName = "settings.json";

    private static readonly JsonSerializerOptions Options = EntityDocuments.Options;

    private static readonly string IdName =
        Options.PropertyNamingPolicy!.ConvertName(nameof(EntityDocument.Id));

    public static string Data(string name) => $"{DataFolder}/{name}";

    public static string Data(string build, string name) => Data($"{ExportFolders.Name(build)}/{name}");

    public static InvalidOperationException Missing(string name) =>
        new($"The export carries no {name}. Run 'dotnet run --project src/FellowshipExtractor' to " +
            "write it: with no destination it writes the output/export-<build> folder the codex and the " +
            "MCP server read, '--bucket <name>' fills an S3 bucket, and '--connection-string' fills an " +
            "Azure Blob container, which is what the FellowshipExtractor.Publisher app host runs. The " +
            "codex reads whichever of the two its own configuration names.");

    public static SettingsFile ReadSettings(string? json)
    {
        if (json is null) throw Missing(SettingsName);

        return JsonSerializer.Deserialize<SettingsFile>(json, Options)
            ?? throw new InvalidOperationException($"{SettingsName} deserialised to nothing.");
    }

    public static IReadOnlyList<EntityDocument> ReadEntities(IEnumerable<string> lines)
    {
        var documents = new List<EntityDocument>();
        var number = 0;

        foreach (var line in lines)
        {
            number++;
            if (line.Length == 0) continue;

            documents.Add(Document(line, number));
        }

        return documents;
    }

    public static IReadOnlyDictionary<DocumentKey, IReadOnlyList<string>> Group(IEnumerable<string> lines)
    {
        var grouped = new Dictionary<DocumentKey, List<string>>();

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;
            if (Key(line) is not { } key) continue;

            if (!grouped.TryGetValue(key, out var group)) grouped[key] = group = [];

            group.Add(line);
        }

        return grouped.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value);
    }

    private static DocumentKey? Key(string line)
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

    public static IEnumerable<string> Lines(string text)
    {
        using var reading = new StringReader(text);

        while (reading.ReadLine() is { } line) yield return line;
    }

    private static EntityDocument Document(string line, int number)
    {
        try
        {
            return JsonSerializer.Deserialize<EntityDocument>(line, Options)
                ?? throw new InvalidOperationException(
                    $"Line {number} of {EntitiesName} deserialised to nothing.");
        }
        catch (Exception failure) when (failure is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Line {number} of {EntitiesName} does not read as a document: {failure.Message} An " +
                $"export written before a document carried the '{EntityDocuments.Discriminator}' it is read " +
                "back as reads this way. Write it again with 'dotnet run --project " +
                "src/FellowshipExtractor'.",
                failure);
        }
    }
}
