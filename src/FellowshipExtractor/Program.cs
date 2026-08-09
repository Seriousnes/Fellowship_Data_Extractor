using CUE4Parse.UE4.Exceptions;
using FellowshipExtractor;
using FellowshipExtractor.Commands;
using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;
using FellowshipExtractor.Usmap;

ReadFailureLog.EnableIfRequested();

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
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
        Set FELLOWSHIP_USMAP to a .usmap file for this build, then retry.
        Commands that need no mappings: list, cat, raw, registry, tags, constants, locres, webdb.
        """);
    return 2;
}

async Task<int> RunAsync()
{
switch (command)
{
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
    case "tags":
        await CsvExportCommands.ExportTagsAsync(options);
        break;
    case "constants":
        await CsvExportCommands.ExportConstantsAsync(options);
        break;
    case "locres":
        await LocresCommand.RunAsync(options, args.Length > 1 ? args[1] : "en");
        break;
    case "index":
        await IndexCommand.RunAsync(options);
        break;
    case "usmap-scan":
        await UsmapScanCommand.RunAsync(options);
        break;
    case "usmap":
        await UsmapGenerateCommand.RunAsync(options);
        break;
    case "usmap-diff":
        await UsmapDiffCommand.RunAsync(options, args[1]);
        break;
    case "usmap-array":
        await UsmapArrayCommand.RunAsync(options, args[1]);
        break;
    case "usmap-count":
        await UsmapCountProbeCommand.RunAsync(options, args[1]);
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
    case "ui":
        await UiCommand.RunAsync(options);
        break;
    case "functions":
        await FellowshipExtractor.Tooltip.FunctionsCommand.RunAsync(options);
        break;
    case "tooltip":
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: tooltip <table> <id|name>   e.g. tooltip ItemID 5224");
            return 1;
        }
        await FellowshipExtractor.Tooltip.TooltipCommand.RunAsync(options, args[1], args[2]);
        break;
    case "export-all":
    {
        var format = Argument(args, "--output-format") ?? "jsonl";
        var folder = Path.Combine(options.ResolvedOutputDir, "export");

        if (format != "jsonl")
        {
            Console.Error.WriteLine($"Unknown output format '{format}'. Formats: jsonl.");
            return 1;
        }

        await using IOutputFormat output = new JsonlOutputFormat(folder);

        var rest = args[1..];
        var tables = rest.Where((a, i) =>
            !a.StartsWith('-') &&
            (i == 0 || !rest[i - 1].Equals("--output-format", StringComparison.OrdinalIgnoreCase)));

        await FellowshipExtractor.Tooltip.ExportAllCommand.RunAsync(options, [.. tables], output);
        break;
    }
    case "enums":
        await FellowshipExtractor.Tooltip.EnumsCommand.RunAsync(options);
        break;
    case "webdb":
        await FellowshipExtractor.Tooltip.WebDbCommand.RunAsync(options);
        break;
    default:
        Console.Error.WriteLine(
            $"""
             Unknown command '{command}'. Available:
               list                  mount containers, write the file listing
               cat <filter> [lines]  print matching container files
               raw <filter>          extract matching container files verbatim
               registry              parse AssetRegistry.bin to JSONL
               tags                  export Content/guid tag tables to JSONL
               constants             export RawDataTables tuning constants to JSONL
               locres [culture]      export the localization table to JSONL
               index                 join the tag tables to the asset registry
               dump <filter>         serialize a package's exports to JSON
               textures <filter> [f] decode matching textures to png (or jpeg, tga, dds)
               ui                    write the item tooltip's chrome textures to output/export/ui
               tooltip <table> <id>  reconstruct one entity's tooltip from name, text and constants
               export-all [table...] [--output-format jsonl]  every entity as its type's document
               enums                 regenerate the enumerated types from the exported settings
               webdb                 that export as one self-contained, searchable HTML page
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
