using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public static class Spread
{
    private const string Leaf = ".Spread";

    public static double? For(
        string coefficientKey,
        string? tuning,
        ConstantsTable constants,
        EffectSpreads effects,
        EffectCalculationPresets presets,
        double? entityPreset,
        double? tokenPreset,
        out bool overridden)
    {
        var row = Absolute(coefficientKey, tuning, constants);
        overridden = false;

        if ((effects.For(row) ?? tokenPreset ?? entityPreset) is { } known) return known;

        var separator = row.LastIndexOf('.');
        if (separator < 0) return null;
        if (constants.Lookup(row[..separator] + Leaf).Record?.Value is not { } declared) return null;

        overridden = true;
        return declared == 0 ? null : presets.Band;
    }

    private static string Absolute(string key, string? tuning, ConstantsTable constants)
    {
        if (tuning is null) return key;

        var prefixed = $"{tuning}.{key}";
        return constants.Lookup(prefixed).Record is not null ? prefixed : key;
    }
}

public sealed class EffectSpreads
{
    private readonly Dictionary<string, double> _byRow = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _contested = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Contested => _contested;

    public void Add(IEnumerable<CurveBinding> bindings, double spread)
    {
        foreach (var binding in bindings)
        {
            if (_byRow.TryGetValue(binding.RowName, out var existing))
            {
                if (existing != spread) _contested.Add(binding.RowName);
                continue;
            }

            _byRow[binding.RowName] = spread;
        }
    }

    public double? For(string rowName) =>
        !_contested.Contains(rowName) && _byRow.TryGetValue(rowName, out var spread) ? spread : null;
}

public sealed class EffectCalculationPresets
{
    public const string RowStructure = "/Script/fellowship.CREffectCalculationPreset";

    private const string PresetField = "EffectCalculationPreset";
    private const string RowNameField = "RowName";
    private const string SpreadField = "DefaultSpread";

    private readonly Dictionary<string, double> _byRow = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byRow.Count;

    public EffectCalculationPresets(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                _byRow[rowName.Text] =
                    PropertyWalk.AsNumber(PropertyWalk.Get(row, SpreadField)?.GenericValue) ?? 0;
            }
        }

        if (_byRow.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so no gameplay effect can say what band it rolls in and every " +
                "coefficient would export without one.");
        }
    }

    public double Band
    {
        get
        {
            var banded = _byRow.Values.Where(spread => spread != 0).Distinct().ToArray();

            if (banded.Length == 1) return banded[0];

            throw new InvalidOperationException(
                $"{RowStructure} declares {banded.Length} distinct bands ({string.Join(", ", banded)}), so there " +
                "is no single band a coefficient rolls in and one has to be chosen per effect instead.");
        }
    }

    public double? DefaultSpread(string? rowName) =>
        rowName is not null && _byRow.TryGetValue(rowName, out var spread) ? spread : null;

    public double? Of(UObject effect) =>
        PropertyWalk.GetStruct(effect, PresetField) is { } handle
            ? DefaultSpread(PropertyWalk.GetString(handle, RowNameField))
            : null;
}
