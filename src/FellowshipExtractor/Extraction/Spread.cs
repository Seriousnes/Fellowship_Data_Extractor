using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

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

    private const string StructureName = "CREffectCalculationPreset";
    private const string BoolType = "BoolProperty";
    private const string ExecutionsField = "Executions";
    private const string CalculationClassField = "CalculationClass";
    private const string DamageCalculation = "CRDamageCalculationBase";
    private const string HealCalculation = "CRHealCalculationBase";
    private const string ScriptRoot = "/Script/";
    private const string NoRow = "None";
    private const int MaxDepth = 16;

    private readonly Dictionary<string, double> _byRow = new(StringComparer.OrdinalIgnoreCase);

    private readonly AbstractFileProvider _provider;
    private readonly CUE4Parse.MappingsProvider.TypeMappings? _mappings;

    public int Count => _byRow.Count;

    /// <summary>Each row of the preset table with each of its flags, by the row structure's field name, and its spread.</summary>
    public IReadOnlyDictionary<string, Fellowship.SDK.EffectCalculationPresetEntry> Rows { get; }

    public EffectCalculationPresets(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        _provider = provider;
        _mappings = provider.MappingsContainer?.MappingsForGame;

        var flags = _mappings is not null && _mappings.Types.TryGetValue(StructureName, out var structure)
            ? structure.Properties.OrderBy(property => property.Key)
                .Where(property => property.Value.MappingType.Type == BoolType)
                .Select(property => property.Value.Name)
                .ToArray()
            : [];

        var rows = new Dictionary<string, Fellowship.SDK.EffectCalculationPresetEntry>(StringComparer.Ordinal);

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                var spread = PropertyWalk.AsNumber(PropertyWalk.Get(row, SpreadField)?.GenericValue) ?? 0;

                _byRow[rowName.Text] = spread;

                var stated = new Dictionary<string, bool>(StringComparer.Ordinal);

                foreach (var flag in flags)
                {
                    stated[flag] = PropertyWalk.Get(row, flag)?.GenericValue as bool?
                        ?? throw new InvalidOperationException(
                            $"{table.Name}'s row {rowName.Text} states no {flag}, so it would read back as a " +
                            $"constructor default of {StructureName} that no shipped file states.");
                }

                rows[rowName.Text] = new Fellowship.SDK.EffectCalculationPresetEntry(stated, spread);
            }
        }

        Rows = rows;

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

    /// <summary>
    /// The preset row an effect with a damage or healing execution names, read from the nearest class of its Blueprint
    /// chain that declares <c>EffectCalculationPreset</c>. An execution is damage or healing when its calculation class
    /// derives from <c>CRDamageCalculationBase</c> or <c>CRHealCalculationBase</c>.
    /// </summary>
    public string? Executed(UObject effect)
    {
        if (!Executes(effect)) return null;

        return ClassDefaults.Inherited(effect, PresetField, _provider)?.GenericValue is FScriptStruct { StructType: IPropertyHolder handle } &&
               PropertyWalk.GetString(handle, RowNameField) is { } row && row != NoRow
            ? row
            : null;
    }

    /// <summary>Whether the effect has a damage or healing execution.</summary>
    public bool Executes(UObject effect)
    {
        if (ClassDefaults.Inherited(effect, ExecutionsField, _provider)?.GenericValue is not UScriptArray executions)
        {
            return false;
        }

        foreach (var element in executions.Properties)
        {
            if (element?.GenericValue is not FScriptStruct { StructType: IPropertyHolder execution }) continue;
            if (PropertyWalk.GetString(execution, CalculationClassField) is not { } path) continue;

            if (Lineage(path).Overlaps([DamageCalculation, HealCalculation])) return true;
        }

        return false;
    }

    /// <summary>The calculation class's native class and the native ancestors the mappings declare.</summary>
    private HashSet<string> Lineage(string classPath)
    {
        var lineage = new HashSet<string>(StringComparer.Ordinal);
        var path = classPath;

        for (var depth = 0; depth < MaxDepth && !path.StartsWith(ScriptRoot, StringComparison.Ordinal); depth++)
        {
            if (ClassDefaults.Load(path, _provider) is not { } loaded ||
                ClassDefaults.NativeParent(loaded, _provider) is not { } native)
            {
                return lineage;
            }

            path = native;
        }

        for (var name = path[(path.LastIndexOf('.') + 1)..]; name is { Length: > 0 } && lineage.Add(name);)
        {
            name = _mappings is not null && _mappings.Types.TryGetValue(name, out var structure) ? structure.SuperType : null;
        }

        return lineage;
    }

    public EffectBand? Own(UObject asset, ConstantsTable constants, ICollection<string> unbindableSpreadRows)
    {
        var (band, unbindable) = GameplayEffects.SpreadOverride(asset, constants);

        if (unbindable is not null) unbindableSpreadRows.Add(unbindable);
        if (band is { } overridden) return new EffectBand(overridden, true);

        return Of(asset) is { } preset ? new EffectBand(preset, false) : null;
    }
}
