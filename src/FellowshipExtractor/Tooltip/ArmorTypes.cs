using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What an armour type value means. The usmap carries no enum tables, so ECRItemArmorType reaches
/// an asset property as a bare ordinal; the same enum used as a map key survives as its full name.
/// Both readings are answered here.
///
/// DT_ArmorTypeWeights is the shipped decode for the ordinal: it names each row after the armour
/// type and repeats the ordinal inside the row. Its zero row carries no armour at all - zero
/// weight, zero flat armour - so it is not an armour type anything can be, and a value naming it
/// answers null rather than a label nothing wears.
/// </summary>
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

    /// <summary>The armour type a stored value names, or null when it names none.</summary>
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
