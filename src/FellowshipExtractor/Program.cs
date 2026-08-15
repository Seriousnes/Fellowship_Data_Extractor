using CUE4Parse.UE4.Exceptions;
using FellowshipExtractor;
using FellowshipExtractor.Commands;
using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;
using FellowshipExtractor.Tooltip;
using FellowshipExtractor.Usmap;

ReadFailureLog.EnableIfRequested();

const string OutputFlag = "--output";
const string ConnectionStringFlag = "--connection-string";
const string UsmapFlag = "--usmap";
const string ConnectionStringVariable = "ConnectionStrings__icons";
const string ExportFolderName = "export";

var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "export";
var options = new ExtractorOptions
{
    MappingsFile = Environment.GetEnvironmentVariable("FELLOWSHIP_USMAP"),
    Game = ExtractorOptions.ParseGame(Environment.GetEnvironmentVariable("FELLOWSHIP_UE_VERSION")),
};

Directory.CreateDirectory(options.ResolvedOutputDir);

try
{
    return await RunAsync();
}
catch (MappingException)
{
    Console.Error.WriteLine(
        """
        This package uses unversioned properties and no mappings are loaded.
        Run with --usmap to recover them, or set FELLOWSHIP_USMAP to a .usmap file for this build.
        Commands that need no mappings: list, cat, raw, registry, locres.
        """);
    return 2;
}

async Task<int> RunAsync()
{
switch (command)
{
    case "export":
    {
        var connectionString = Argument(args, ConnectionStringFlag)
            ?? Environment.GetEnvironmentVariable(ConnectionStringVariable);

        var folder = Argument(args, OutputFlag)
            ?? (connectionString is null
                ? Path.Combine(options.ResolvedOutputDir, ExportFolderName)
                : null);

        if (Array.Exists(args, a => a.Equals(UsmapFlag, StringComparison.OrdinalIgnoreCase)))
        {
            if (!await UsmapGenerateCommand.RunAsync(options)) return 1;

            await FunctionsCommand.RunAsync(options);
        }

        List<IExportTarget> destinations = [];
        if (folder is not null) destinations.Add(new FolderTarget(folder));
        if (connectionString is not null) destinations.Add(new BlobTarget(connectionString));

        await using (IExportTarget target = destinations.Count == 1
            ? destinations[0]
            : new CompositeTarget(destinations))
        {
            await ExportAllCommand.RunAsync(options, target);
        }

        if (folder is not null) await EnumsCommand.WriteAsync(folder);

        break;
    }
    case "list":
        await ListCommand.RunAsync(options);
        break;
    case "cat":
        await RawCommands.CatAsync(options, args[1], args.Length > 2 ? int.Parse(args[2]) : 20);
        break;
    case "raw":
        await RawCommands.ExtractAsync(options, args[1]);
        break;
    case "registry":
        await RegistryCommand.RunAsync(options);
        break;
    case "dump":
        await DumpCommand.RunAsync(options, args[1], args.Length > 2 ? int.Parse(args[2]) : 1);
        break;
    case "locres":
        await LocresCommand.RunAsync(options, args.Length > 1 ? args[1] : "en");
        break;
    case "usmap":
        if (!await UsmapGenerateCommand.RunAsync(options)) return 1;
        break;
    case "usmap-scan":
        await UsmapScanCommand.RunAsync(options);
        break;
    case "usmap-diff":
        await UsmapDiffCommand.RunAsync(options, args[1]);
        break;
    case "usmap-debug":
        await UsmapDebugCommand.RunAsync(options, args[1]);
        break;
    case "probe":
        await ProbeCommand.RunAsync(options, args.Length > 1 ? int.Parse(args[1]) : 300);
        break;
    case "texture":
    case "textures":
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: textures <filter> [png|jpeg|tga|dds]   e.g. textures ui/icons/hero_abilities");
            return 1;
        }
        await TextureCommand.RunAsync(options, args[1], args.Length > 2 ? args[2] : null);
        break;
    case "functions":
        await FunctionsCommand.RunAsync(options);
        break;
    case "tooltip":
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: tooltip <table> <id|name>   e.g. tooltip ItemID 5224");
            return 1;
        }
        await TooltipCommand.RunAsync(options, args[1], args[2]);
        break;
    default:
        Console.Error.WriteLine(
            $"""
             Unknown command '{command}'.

             With no command, the extractor writes the whole export: every entity as its type's
             document, the settings, every image, tags.jsonl, ability-constants.jsonl and the enums
             the documents are read against.

               --usmap                      recover the mappings and the Blueprint function index
                                            first, and stop before exporting if they come back short
               --output <path>              a folder laid out like the container, output/{ExportFolderName}
                                            unless a connection string names blob storage
               --connection-string <value>  blob storage, container '{BlobTarget.ContainerName}', also read from
                                            the {ConnectionStringVariable} variable

             Commands:
               list                  mount containers, write the file listing
               cat <filter> [lines]  print matching container files
               raw <filter>          extract matching container files verbatim
               registry              parse AssetRegistry.bin to JSONL
               locres [culture]      export the localization table to JSONL
               dump <filter>         serialize a package's exports to JSON
               textures <filter> [f] decode matching textures to png (or jpeg, tga, dds)
               tooltip <table> <id>  reconstruct one entity's tooltip from name, text and constants
               functions             index every Blueprint function by name, signature and callers
               usmap                 recover the mappings without exporting
               usmap-scan            report what the scanner recovers
               usmap-diff <ref>      grade recovered schemas against another UE5 game's usmap
               usmap-debug <prop>    trace one property record back to its owning params record
               probe [sample]        report how many packages parse with current mappings
             """);
        return 1;
}

return 0;
}

static string? Argument(string[] args, string name)
{
    var at = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
