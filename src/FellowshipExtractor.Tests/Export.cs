using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipExtractor.Api;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class Export
{
    private const string Variable = "FELLOWSHIP_EXPORT";

    private static readonly Lazy<Export> Instance = new(Load, isThreadSafe: true);

    public static Export Loaded => Instance.Value;

    public required VocabularyFile Vocabulary { get; init; }
    public required IReadOnlyList<Entity> Entities { get; init; }
    public required string Folder { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static Export Load()
    {
        var folder = Folder_();

        var entities = new List<Entity>();
        foreach (var line in File.ReadLines(Path.Combine(folder, "entities.jsonl")))
        {
            if (line.Length == 0) continue;
            if (JsonSerializer.Deserialize<Entity>(line, Options) is { } entity) entities.Add(entity);
        }

        var vocabulary = JsonSerializer.Deserialize<VocabularyFile>(
            File.ReadAllText(Path.Combine(folder, "vocabulary.json")), Options)
            ?? throw new InvalidOperationException($"vocabulary.json in {folder} deserialized to nothing.");

        return new Export { Vocabulary = vocabulary, Entities = entities, Folder = folder };
    }

    private static string Folder_()
    {
        if (Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } named)
        {
            return Directory.Exists(named)
                ? named
                : throw new DirectoryNotFoundException($"{Variable} names {named}, which does not exist.");
        }

        for (var at = new DirectoryInfo(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            var candidate = Path.Combine(at.FullName, "output", "export");
            if (File.Exists(Path.Combine(candidate, "entities.jsonl"))) return candidate;
        }

        throw new DirectoryNotFoundException(
            "No export to test against. Run 'dotnet run --project src/FellowshipExtractor -- export-all' " +
            $"or point {Variable} at a folder holding entities.jsonl and vocabulary.json.");
    }

    public Entity Entity(string table, int netId) =>
        Entities.FirstOrDefault(e =>
            string.Equals(e.Table, table, StringComparison.OrdinalIgnoreCase) && e.NetId == netId)
        ?? throw new InvalidOperationException(
            $"{table} {netId} is not in the export. Either the id moved or the entity is gone.");

    public IEnumerable<Entity> All()
    {
        foreach (var entity in Entities)
        {
            foreach (var reached in Reachable(entity)) yield return reached;
        }
    }

    private static IEnumerable<Entity> Reachable(Entity entity)
    {
        yield return entity;

        foreach (var nested in Nested(entity))
        {
            foreach (var reached in Reachable(nested)) yield return reached;
        }
    }

    private static IEnumerable<Entity> Nested(Entity entity)
    {
        if (entity.Grants is { } granted) yield return granted;

        foreach (var stat in entity.LegendaryStats ?? []) yield return stat;

        foreach (var variant in entity.Variants ?? [])
        {
            if (variant.Grants is { } granted_) yield return granted_;
        }
    }
}
