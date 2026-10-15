using System.Text.Json;
using System.Text.Json.Nodes;

using CUE4Parse.FileProvider;

using Fellowship.Codex.Services;
using FellowshipExtractor.Extraction;
using FellowshipExtractor.Output;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class Export
{
    private const string Variable = FolderExportSource.Variable;

    private static readonly Lazy<Export> Instance = new(Load, isThreadSafe: true);

    public static Export Loaded => Instance.Value;

    private const string Mappings = "FELLOWSHIP_USMAP";

    private static readonly Lazy<AbstractFileProvider> ProviderInstance = new(Mount, isThreadSafe: true);

    public static AbstractFileProvider Provider => ProviderInstance.Value;

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
                $"mappings/{ExtractorOptions.MappingsPattern} with " +
                "'dotnet run --project src/FellowshipExtractor -- usmap'.");
        }

        return ProviderFactory.CreateAsync(options).GetAwaiter().GetResult();
    }

    public required SettingsFile Settings { get; init; }
    public required IReadOnlyList<(EntityType Type, int Id, string? Name, JsonObject Body)> Documents { get; init; }
    public required string Folder { get; init; }

    public string Ui => Path.Combine(Directory.GetParent(Folder)!.FullName, ExportLayout.UiFolder);

    private static Export Load()
    {
        var folder = Folder_();

        var snapshot = new FolderExportSource(folder)
            .LoadAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Snapshot;

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
            return FolderExportSource.Carries(named)
                ? named
                : throw new DirectoryNotFoundException(
                    $"{Variable} names {named}, which carries no {ExportReader.EntitiesName}.");
        }

        return FolderExportSource.Above(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException(
                $"No output/{ExportFolders.Pattern} above the test binary carries " +
                $"{ExportReader.EntitiesName} and {ExportReader.SettingsName}. Run " +
                $"'dotnet run --project src/FellowshipExtractor' or point {Variable} at one.");
    }
}
