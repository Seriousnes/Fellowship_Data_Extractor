using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipExtractor.Api;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The export the tests read, loaded once for the whole run.
///
/// These tests exist to notice when the game's own numbers move, so they read what the extractor
/// actually produced rather than a fixture checked in beside them. A fixture would keep passing
/// through the patch it was meant to catch.
///
/// An export that is not there is a failure and not a skip. A skipped test reports green, which is
/// the one outcome that would let a drifted build through unremarked.
/// </summary>
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

    /// <summary>
    /// Where the export is. The environment names it when a caller has one somewhere of their own;
    /// otherwise the repository's own output folder is found by walking up from the test binary.
    /// </summary>
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

    /// <summary>One entity by the table and id the game knows it by.</summary>
    public Entity Entity(string table, int netId) =>
        Entities.FirstOrDefault(e =>
            string.Equals(e.Table, table, StringComparison.OrdinalIgnoreCase) && e.NetId == netId)
        ?? throw new InvalidOperationException(
            $"{table} {netId} is not in the export. Either the id moved or the entity is gone.");

    /// <summary>Every entity in the export and every one nested inside it.</summary>
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

        // A variant is one hero's reading of an item rather than an entity of its own, and the
        // ability it grants is where the tokens live.
        foreach (var variant in entity.Variants ?? [])
        {
            if (variant.Grants is { } granted_) yield return granted_;
        }
    }
}
