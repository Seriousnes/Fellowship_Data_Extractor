using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public readonly record struct SpreadReading(double? Band, bool Overridden);

public static class Spread
{
    public static SpreadReading For(
        string coefficientKey,
        string? tuning,
        ConstantsTable constants,
        EffectSpreads effects,
        EffectBand? entityBand,
        EffectBand? tokenBand)
    {
        var row = Absolute(coefficientKey, tuning, constants);
        var band = effects.For(row) ?? tokenBand ?? entityBand;

        return new SpreadReading(band is { } known ? Banded(known.Spread) : null, band?.Overridden ?? false);
    }

    private static double? Banded(double? spread) => spread is { } band && band != 0 ? band : null;

    private static string Absolute(string key, string? tuning, ConstantsTable constants)
    {
        if (tuning is null) return key;

        var prefixed = $"{tuning}.{key}";
        return constants.Lookup(prefixed).Record is not null ? prefixed : key;
    }
}

public readonly record struct EffectBand(double Spread, bool Overridden);

public sealed class EffectSpreads
{
    private readonly Dictionary<string, EffectBand> _byRow = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _contested = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Contested => _contested;

    public void Add(IEnumerable<CurveBinding> bindings, double spread, bool overridden)
    {
        foreach (var binding in bindings)
        {
            if (_byRow.TryGetValue(binding.RowName, out var existing))
            {
                if (existing.Spread != spread) _contested.Add(binding.RowName);
                continue;
            }

            _byRow[binding.RowName] = new EffectBand(spread, overridden);
        }
    }

    public EffectBand? For(string rowName) =>
        !_contested.Contains(rowName) && _byRow.TryGetValue(rowName, out var band) ? band : null;
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

    public double? DefaultSpread(string? rowName) =>
        rowName is not null && _byRow.TryGetValue(rowName, out var spread) ? spread : null;

    public double? Of(UObject effect) =>
        PropertyWalk.GetStruct(effect, PresetField) is { } handle
            ? DefaultSpread(PropertyWalk.GetString(handle, RowNameField))
            : null;

    public EffectBand? Own(UObject asset, ConstantsTable constants, ICollection<string> unbindableSpreadRows)
    {
        var (band, unbindable) = GameplayEffects.SpreadOverride(asset, constants);

        if (unbindable is not null) unbindableSpreadRows.Add(unbindable);
        if (band is { } overridden) return new EffectBand(overridden, true);

        return Of(asset) is { } preset ? new EffectBand(preset, false) : null;
    }
}
