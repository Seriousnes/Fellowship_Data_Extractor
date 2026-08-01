using System.Text.Json;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor;

/// <summary>
/// AssetRegistry.bin lists every cooked asset with its class and cooked tags, and it parses
/// without mappings. This is how we find where items, spells and abilities actually live.
/// </summary>
public static class RegistryCommand
{
    public const string RegistryPath = "fellowship/AssetRegistry.bin";

    public static FAssetData[] ReadAssets(AbstractFileProvider provider)
    {
        if (!provider.Files.TryGetValue(RegistryPath, out var file))
        {
            throw new FileNotFoundException($"{RegistryPath} not found in the mounted containers.");
        }

        using var archive = new FByteArchive(file.Path, file.Read(), provider.Versions);
        return new FAssetRegistryState(archive).PreallocatedAssetDataBuffers;
    }

    public static async Task RunAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var assets = ReadAssets(provider);
        Console.WriteLine($"AssetRegistry: {assets.Length} assets");

        var jsonlPath = Path.Combine(options.ResolvedOutputDir, "asset-registry.jsonl");
        await using (var writer = new StreamWriter(jsonlPath))
        {
            foreach (var asset in assets)
            {
                var record = new
                {
                    objectPath = asset.ObjectPath,
                    packageName = asset.PackageName.Text,
                    assetName = asset.AssetName.Text,
                    assetClass = asset.AssetClass.Text,
                    tags = asset.TagsAndValues.ToDictionary(kv => kv.Key.Text, kv => kv.Value),
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(record));
            }
        }
        Console.WriteLine($"Wrote {jsonlPath}");

        var classesPath = Path.Combine(options.ResolvedOutputDir, "asset-classes.txt");
        var classes = assets
            .GroupBy(a => a.AssetClass.Text)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count(),8}  {g.Key}");
        await File.WriteAllLinesAsync(classesPath, classes);
        Console.WriteLine($"Wrote {classesPath}");
    }
}
