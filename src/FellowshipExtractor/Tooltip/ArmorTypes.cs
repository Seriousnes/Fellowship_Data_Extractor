using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public sealed class ArmorTypes
{
    public const string RowStructure = "/Script/fellowship.CRArmorTypeWeightRow";
    private const string OrdinalField = "ArmorType";
    private const string WeightField = "ArmorWeight";
    private const string EnumSeparator = "::";

    private readonly Dictionary<int, string> _byOrdinal = [];
    private readonly HashSet<string> _labels = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byOrdinal.Count;

    public ArmorTypes(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, OrdinalField)?.GenericValue) is not { } ordinal) continue;
                if (PropertyWalk.AsNumber(PropertyWalk.Get(row, WeightField)?.GenericValue) is not { } weight) continue;
                if (weight <= 0) continue;

                _byOrdinal.TryAdd(ordinal, rowName.Text);
                _labels.Add(rowName.Text);
            }
        }
    }

    public string? Label(object? value)
    {
        if (PropertyWalk.AsOrdinal(value) is { } ordinal) return _byOrdinal.GetValueOrDefault(ordinal);

        var text = PropertyWalk.AsString(value);
        if (text is null) return null;

        var separator = text.LastIndexOf(EnumSeparator, StringComparison.Ordinal);
        var leaf = separator < 0 ? text : text[(separator + EnumSeparator.Length)..];

        return _labels.TryGetValue(leaf, out var label) ? label : null;
    }
}
