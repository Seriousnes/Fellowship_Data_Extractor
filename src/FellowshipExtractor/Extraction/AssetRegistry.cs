using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Extraction;

public static class AssetRegistry
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

    public static string? TagValue(FAssetData asset, string key) =>
        asset.TagsAndValues.FirstOrDefault(kv => kv.Key.Text.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}
