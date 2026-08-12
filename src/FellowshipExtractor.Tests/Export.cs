using System.Text.Json;
using System.Text.Json.Nodes;

using CUE4Parse.FileProvider;

using Fellowship.Codex.Services;
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

    private static readonly Lazy<AbstractFileProvider> ProviderInstance = new(Mount, isThreadSafe: true);

    public static AbstractFileProvider Provider => ProviderInstance.Value;

    private static TooltipResolver Build() => new(Provider);

    private static AbstractFileProvider Mount()
    {
        var options = new ExtractorOptions
        {
            MappingsFile = Environment.GetEnvironmentVariable(Mappings),
            Game = ExtractorOptions.ParseGame(Environment.GetEnvironmentVariable("FELLOWSHIP_UE_VERSION")),
        };

        if (options.ResolvedMappingsFile is null)
        {
            throw new InvalidOperationException(
                $"No mappings to read the build with. Set {Mappings} to a .usmap for this build, or write " +
                $"output/{ExtractorOptions.MappingsName} with " +
                "'dotnet run --project src/FellowshipExtractor -- usmap'.");
        }

        return ProviderFactory.CreateAsync(options).GetAwaiter().GetResult();
    }

    public required SettingsFile Settings { get; init; }
    public required IReadOnlyList<(EntityType Type, int Id, string? Name, JsonObject Body)> Documents { get; init; }
    public required string Folder { get; init; }

    private static Export Load()
    {
        var folder = Folder_();

        var snapshot = new FolderExportSource(folder)
            .LoadAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return new Export
        {
            Settings = snapshot.Settings,
            Documents = [.. snapshot.Entities.Select(Row)],
            Folder = folder,
        };
    }

    private static (EntityType Type, int Id, string? Name, JsonObject Body) Row(EntityDocument document) =>
        (EntityTypes.TypeOf(document),
            document.Id,
            document.Name,
            JsonSerializer.SerializeToNode(document, EntityDocuments.Options)!.AsObject());

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
            if (File.Exists(Path.Combine(candidate, ExportReader.Data, ExportReader.EntitiesName)))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "No export to test against. Run 'dotnet run --project src/FellowshipExtractor -- export-all " +
            $"--output output/export' or point {Variable} at a folder holding " +
            $"{ExportReader.Data}/{ExportReader.EntitiesName} and " +
            $"{ExportReader.Data}/{ExportReader.SettingsName}.");
    }
}
