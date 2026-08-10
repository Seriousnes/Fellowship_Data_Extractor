using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed class VendorInventories
{
    private const string InventoryClass = "CRVendorInventory";
    private const string ItemClass = "CRVendorItemDataAsset";
    private const string VendorNameField = "VendorName";
    private const string DropTableField = "DropTable";
    private const string InventoryPrefix = "VI_";

    private readonly Dictionary<string, string> _byDroptable = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byDroptable.Count;

    public VendorInventories(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var byShelf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(InventoryClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (asset.AssetName.Text is not { } named) continue;
            if (!named.StartsWith(InventoryPrefix, StringComparison.Ordinal)) continue;
            if (DungeonNames.Load(provider, asset.ObjectPath) is not { } inventory) continue;
            if (PropertyWalk.GetText(inventory, VendorNameField) is not { Length: > 0 } vendor) continue;

            byShelf[named[InventoryPrefix.Length..]] = vendor;
        }

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(ItemClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (DungeonNames.Load(provider, asset.ObjectPath) is not { } offer) continue;
            if (PropertyWalk.GetString(offer, DropTableField) is not { } droptable) continue;

            var segments = asset.PackageName.Text.Split('/');
            if (segments.Length < 2) continue;
            if (!byShelf.TryGetValue(segments[^2], out var vendor)) continue;

            _byDroptable.TryAdd(AbilityPresentationIndex.AssetName(droptable), vendor);
        }
    }

    public string? VendorOf(string droptableName) => _byDroptable.GetValueOrDefault(droptableName);
}
