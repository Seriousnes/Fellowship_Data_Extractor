using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

public sealed class VendorInventories
{
    private const string InventoryClass = "CRVendorInventory";
    private const string ItemClass = "CRVendorItemDataAsset";
    private const string VendorIdField = "VendorID";
    private const string VendorNameField = "VendorName";
    private const string DisabledField = "IsDisabled";
    private const string EnabledField = "EnabledInGame";
    private const string DropTableField = "DropTable";

    private readonly Dictionary<string, string> _byDroptable = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byDroptable.Count;

    public VendorInventories(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var byVendor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(InventoryClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (Exports(provider, asset).FirstOrDefault(export => export.ExportType == InventoryClass) is not { } inventory)
            {
                continue;
            }

            if (PropertyWalk.Get(inventory, DisabledField)?.GenericValue is true) continue;
            if (PropertyWalk.GetString(inventory, VendorIdField) is not { } vendorId) continue;
            if (PropertyWalk.GetText(inventory, VendorNameField) is not { Length: > 0 } vendor) continue;

            if (!byVendor.TryAdd(vendorId, vendor))
            {
                throw new InvalidOperationException(
                    $"Two live {InventoryClass} assets have {VendorIdField} {vendorId}, so an offer naming it has two " +
                    "vendors.");
            }
        }

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(ItemClass, StringComparison.OrdinalIgnoreCase)) continue;

            var exports = Exports(provider, asset);
            if (exports.FirstOrDefault(export => export.ExportType == ItemClass) is not { } offer) continue;
            if (PropertyWalk.Get(offer, EnabledField)?.GenericValue is false) continue;
            if (PropertyWalk.GetString(offer, VendorIdField) is not { } vendorId) continue;
            if (!byVendor.TryGetValue(vendorId, out var vendor)) continue;

            foreach (var export in exports)
            {
                if (PropertyWalk.GetString(export, DropTableField) is not { } droptable) continue;

                _byDroptable.TryAdd(AbilityPresentationIndex.AssetName(droptable), vendor);
            }
        }
    }

    private static UObject[] Exports(AbstractFileProvider provider, FAssetData asset) =>
        provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(asset.ObjectPath), out var package)
            ? [.. package.GetExports()]
            : [];

    public string? VendorOf(string droptableName) => _byDroptable.GetValueOrDefault(droptableName);
}
