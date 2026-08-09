using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FellowshipExtractor.Tooltip;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class Export
{
    private const string Variable = "FELLOWSHIP_EXPORT";
    private const string Mappings = "FELLOWSHIP_USMAP";

    private static readonly Lazy<Export> Instance = new(Load, isThreadSafe: true);

    public static Export Loaded => Instance.Value;

    private static readonly Lazy<TooltipResolver> ResolverInstance = new(Build, isThreadSafe: true);

    public static TooltipResolver Resolver => ResolverInstance.Value;

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

    public required SettingsFile Settings { get; init; }
    public required IReadOnlyList<(EntityType Type, int Id, string? Name, JsonObject Body)> Documents { get; init; }
    public required string Folder { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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
            var slug = body[EntityDocuments.Discriminator]!.GetValue<string>();

            if (!TypeBySlug.TryGetValue(slug, out var type))
            {
                throw new InvalidOperationException($"'{slug}' in entities.jsonl names no EntityType. The export is corrupt.");
            }

            documents.Add((type, body["id"]!.GetValue<int>(), body["name"]?.GetValue<string>(), body));
        }

        var settings = JsonSerializer.Deserialize<SettingsFile>(
            File.ReadAllText(Path.Combine(folder, "settings.json")), Options)
            ?? throw new InvalidOperationException($"settings.json in {folder} deserialized to nothing.");

        return new Export { Settings = settings, Documents = documents, Folder = folder };
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
            $"or point {Variable} at a folder holding entities.jsonl and settings.json.");
    }
}
