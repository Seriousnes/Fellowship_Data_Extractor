using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    private const string Mappings = "FELLOWSHIP_USMAP";

    private static readonly Lazy<Export> Instance = new(Load, isThreadSafe: true);

    public static Export Loaded => Instance.Value;

    private static readonly Lazy<TooltipResolver> ResolverInstance = new(Build, isThreadSafe: true);

    /// <summary>
    /// A resolver over the cooked build, built once for the whole run. Mounting the containers is
    /// the expensive part of every one of these tests and the state it produces is read only.
    /// </summary>
    public static TooltipResolver Resolver => ResolverInstance.Value;

    /// <summary>
    /// The resolver, with the mappings it needs named before it is built. Without them the build
    /// reads as a build that ships none of the tables these tests are about, so every test touching
    /// the resolver would report the game having moved when the environment is what is missing.
    /// </summary>
    private static TooltipResolver Build()
    {
        if (Environment.GetEnvironmentVariable(Mappings) is not { Length: > 0 } mappings)
        {
            throw new InvalidOperationException(
                $"No mappings to read the build with. Set {Mappings} to a .usmap for this build, which " +
                "'dotnet run --project src/FellowshipExtractor -- usmap' writes to output/fellowship.usmap.");
        }

        if (!File.Exists(mappings))
        {
            throw new FileNotFoundException($"{Mappings} names {mappings}, which does not exist.", mappings);
        }

        return new TooltipResolver(
            ProviderFactory.CreateAsync(new ExtractorOptions
            {
                MappingsFile = mappings,
                Game = ExtractorOptions.ParseGame(Environment.GetEnvironmentVariable("FELLOWSHIP_UE_VERSION")),
            }).GetAwaiter().GetResult());
    }

    public required VocabularyFile Vocabulary { get; init; }
    public required IReadOnlyList<(EntityType Type, int Id, string? Name, JsonObject Body)> Documents { get; init; }
    public required string Folder { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The route back from the slug a line carries to the type it names, built from
    /// <see cref="EntityTypes.Slug"/> itself rather than a second table that could drift from it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, EntityType> TypeBySlug =
        Enum.GetValues<EntityType>().ToDictionary(EntityTypes.Slug, type => type, StringComparer.Ordinal);

    private static Export Load()
    {
        var folder = Folder_();

        var documents = new List<(EntityType Type, int Id, string? Name, JsonObject Body)>();
        foreach (var line in File.ReadLines(Path.Combine(folder, "entities.jsonl")))
        {
            if (line.Length == 0) continue;

            var body = JsonNode.Parse(line)!.AsObject();
            var slug = body["type"]!.GetValue<string>();

            if (!TypeBySlug.TryGetValue(slug, out var type))
            {
                throw new InvalidOperationException($"'{slug}' in entities.jsonl names no EntityType. The export is corrupt.");
            }

            documents.Add((type, body["id"]!.GetValue<int>(), body["name"]?.GetValue<string>(), body));
        }

        var vocabulary = JsonSerializer.Deserialize<VocabularyFile>(
            File.ReadAllText(Path.Combine(folder, "vocabulary.json")), Options)
            ?? throw new InvalidOperationException($"vocabulary.json in {folder} deserialized to nothing.");

        return new Export { Vocabulary = vocabulary, Documents = documents, Folder = folder };
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
}
