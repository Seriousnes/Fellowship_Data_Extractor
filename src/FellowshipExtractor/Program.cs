using CUE4Parse.UE4.Exceptions;
using FellowshipExtractor;
using FellowshipExtractor.Dump;
using FellowshipExtractor.Extraction;
using FellowshipExtractor.Output;
using FellowshipExtractor.Usmap;

using Fellowship.Storage;

ReadFailureLog.EnableIfRequested();

const string OutputFlag = "--output";
const string ConnectionStringFlag = "--connection-string";
const string BucketFlag = "--bucket";
const string UsmapFlag = "--usmap";
const string ConnectionStringVariable = "ConnectionStrings__icons";
const string BucketVariable = "S3__Bucket";

var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "export";
var options = new ExtractorOptions
{
    MappingsFile = Environment.GetEnvironmentVariable("FELLOWSHIP_USMAP"),
    Game = ExtractorOptions.ParseGame(Environment.GetEnvironmentVariable("FELLOWSHIP_UE_VERSION")),
};

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

        var bucket = Argument(args, BucketFlag)
            ?? Environment.GetEnvironmentVariable(BucketVariable);

        string? read = null;
        string Build() => read ??= GameBuild.Read(options);

        var remote = connectionString is not null || bucket is not null;

        var folder = Argument(args, OutputFlag)
            ?? (remote ? null : options.ResolvedOutputDir);

        if (Array.Exists(args, a => a.Equals(UsmapFlag, StringComparison.OrdinalIgnoreCase)))
        {
            if (!await UsmapGenerateCommand.RunAsync(options)) return 1;
        }

        List<IExportTarget> destinations = [];
        if (folder is not null) destinations.Add(new FolderTarget(folder, Build()));
        if (connectionString is not null)
            destinations.Add(new StorageTarget(AzureStorage.FromConnectionString(connectionString), Build()));
        if (bucket is not null)
            destinations.Add(new StorageTarget(S3Storage.FromBucket(bucket), Build()));

        await using (IExportTarget target = new VariantTarget(destinations.Count == 1
            ? destinations[0]
            : new CompositeTarget(destinations)))
        {
            await ExportRun.RunAsync(options, target);
        }

        if (folder is not null) SettingsEnums.Publish(folder, Build());

        return 0;
    }
    case "usmap":
        return await UsmapGenerateCommand.RunAsync(options) ? 0 : 1;
    case "dump":
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("dump needs a path filter, e.g. dump BP_DS_ButtonStyle_TalentTreeNode");
            return 2;
        }

        var limit = args.Length > 2 && int.TryParse(args[2], out var many) ? many : 1;

        await DumpCommand.RunAsync(options, args[1], limit);
        return 0;
    }
    case "dump-all":
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("dump-all needs a destination folder, which it must not share with output/.");
            return 2;
        }

        return await DumpAll.RunAsync(options, Path.GetFullPath(args[1]));
    }
    default:
        Console.Error.WriteLine(
            $"""
             Unknown command '{command}'.

             With no command, the extractor writes the whole export: every entity as its type's
             document under export-<steam build id>/, and every image under ui/.

               --usmap                      recover the mappings first, and stop before exporting if
                                            they come back short
               --output <path>              a folder laid out like the store, holding
                                            export-<steam build id>/ and ui/, output/ unless a
                                            remote store is named
               --connection-string <value>  an Azure Blob container, '{StorageDefaults.Name}' unless the
                                            string names another, also read from the
                                            {ConnectionStringVariable} variable
               --bucket <name>              an S3 bucket, its region and credentials from the AWS SDK's
                                            own configuration chain, also read from the {BucketVariable}
                                            variable

             Commands:
               usmap                 recover the mappings without exporting
               dump <filter> [count] write every export of the matching cooked assets as JSON under
                                     output/dump, for reading an asset the export does not carry
               dump-all <folder>     write the whole evidence base for every ability and gameplay
                                     effect the export carries into the named folder: property trees,
                                     expanded modifier magnitudes, Blueprint bytecode, the constants
                                     and tag tables, every curve table, the locres and the config

             Environment:
               FELLOWSHIP_USMAP      a .usmap for this build, otherwise the one the usmap command wrote
               FELLOWSHIP_BUILD      names the build the export folder is filed under, otherwise the
                                     Steam appmanifest for the install supplies it
               FELLOWSHIP_UE_VERSION engine version override, e.g. 5.6 or GAME_UE5_6
             """);
        return 1;
}
}

static string? Argument(string[] args, string name)
{
    var at = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
