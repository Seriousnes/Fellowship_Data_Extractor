using System.Text.Json;
using CUE4Parse.UE4.AssetRegistry.Objects;

namespace FellowshipExtractor;

public static class IndexCommand
{
    public static async Task RunAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var assets = RegistryCommand.ReadAssets(provider);

        var byPrimaryAssetName = new Dictionary<string, FAssetData>(StringComparer.OrdinalIgnoreCase);
        var byAssetName = new Dictionary<string, FAssetData?>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            if (TagValue(asset, "PrimaryAssetName") is { Length: > 0 } primary)
            {
                byPrimaryAssetName.TryAdd(primary, asset);
            }

            var name = asset.AssetName.Text;
            byAssetName[name] = byAssetName.ContainsKey(name) ? null : asset;
        }

        var target = Path.Combine(options.ResolvedOutputDir, "index.jsonl");
        var json = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        int total = 0, viaPrimary = 0, viaName = 0;
        await using var writer = new StreamWriter(target);

        foreach (var tag in CsvExportCommands.ReadTags(provider))
        {
            FAssetData? asset = null;
            string? matchedBy = null;

            if (byPrimaryAssetName.TryGetValue(tag.Name, out var primaryMatch))
            {
                (asset, matchedBy) = (primaryMatch, "PrimaryAssetName");
                viaPrimary++;
            }
            else if (byAssetName.TryGetValue(tag.Name, out var nameMatch) && nameMatch is not null)
            {
                (asset, matchedBy) = (nameMatch, "AssetName");
                viaName++;
            }

            var record = new
            {
                table = tag.Table,
                name = tag.Name,
                netId = tag.NetId,
                matchedBy,
                assetPath = asset?.ObjectPath,
                assetClass = asset?.AssetClass.Text,
                primaryAssetType = asset is null ? null : TagValue(asset, "PrimaryAssetType"),
                nativeClass = asset is null ? null : TagValue(asset, "NativeParentClass") ?? TagValue(asset, "NativeClass"),
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(record, json));
            total++;
        }

        Console.WriteLine($"Wrote {total} index records to {target}");
        Console.WriteLine($"  matched via PrimaryAssetName: {viaPrimary}");
        Console.WriteLine($"  matched via AssetName:        {viaName}");
        Console.WriteLine($"  no cooked asset:              {total - viaPrimary - viaName}");
    }

    private static string? TagValue(FAssetData asset, string key) =>
        asset.TagsAndValues.FirstOrDefault(kv => kv.Key.Text.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}
