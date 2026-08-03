using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The width of the band a rolled magnitude lands in, shipped per effect and overridable per
/// ability.
///
/// Spread is the whole band, not a plus or minus: min is mean x (1 - spread / 2) and max is
/// mean x (1 + spread / 2), so 0.2 is 90 to 110 percent of the mean and 0.4 is 80 to 120. The
/// discriminator is DR_SlaughtervineArchephyte.AutoAttackMelee.Spread = 1, which under a
/// plus-or-minus reading would let a basic melee swing roll anywhere from nothing to double.
///
/// Two layers answer for a coefficient. A tuning row named Spread sitting beside the coefficient
/// row is the ability's own override, and 456 of the 12,928 shipped rows are one. Everything else
/// falls back to the DefaultSpread of the calculation preset the gameplay effect names, which is
/// why Fire Ball has a band at all: the whole Firemage namespace has zero rows whose leaf is
/// Spread, so all of its numbers come from the presets.
/// </summary>
public static class Spread
{
    private const string Leaf = ".Spread";

    /// <summary>
    /// The band a coefficient rolls in, or null when nothing in the build says.
    ///
    /// The row the coefficient came from arrives with the entity's own tuning prefix already
    /// stripped, so the prefix is put back before the sibling is looked for: Searing Blaze's
    /// coefficient keeps its full name while Fire Ball's is a bare leaf, and only the tables can
    /// say which of the two a given key is.
    /// </summary>
    public static double? For(
        string coefficientKey,
        string? tuning,
        ConstantsTable constants,
        EffectSpreads effects,
        double? entityPreset,
        double? tokenPreset,
        out bool overridden)
    {
        var row = Absolute(coefficientKey, tuning, constants);
        overridden = false;

        var separator = row.LastIndexOf('.');
        if (separator > 0 && constants.Lookup(row[..separator] + Leaf).Record?.Value is { } declared)
        {
            overridden = true;
            return declared;
        }

        return effects.For(row) ?? tokenPreset ?? entityPreset;
    }

    /// <summary>
    /// The row name as the shipped tables spell it. The prefixed spelling is tried first because
    /// that is the one <see cref="TuningNamespace.Relative"/> produced, and a bare leaf that also
    /// happens to name a row elsewhere would otherwise win over the entity's own row.
    /// </summary>
    private static string Absolute(string key, string? tuning, ConstantsTable constants)
    {
        if (tuning is null) return key;

        var prefixed = $"{tuning}.{key}";
        return constants.Lookup(prefixed).Record is not null ? prefixed : key;
    }
}

/// <summary>
/// Which band each tuning row an entity's gameplay effects bind rolls in.
///
/// A coefficient row is bound by the effect that multiplies it, and that effect names the preset,
/// so the row is the join between a number and its band. Two effects binding one row under
/// different presets contest it and it answers with nothing, on the same principle
/// <see cref="Coefficients"/> already applies to rival rows: the data no longer identifies one
/// band and the wrong one is worse than none.
/// </summary>
public sealed class EffectSpreads
{
    private readonly Dictionary<string, double> _byRow = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _contested = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rows two of this entity's effects claim under different bands.</summary>
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

/// <summary>
/// The shipped calculation presets, read for the one field a tooltip needs from them.
///
/// A gameplay effect names a preset row rather than restating how it resolves, and the row carries
/// the whole ruleset: whether the hit can crit, which mitigation applies, and DefaultSpread. Only
/// the spread is read here, because it is the only one of the twenty fields that changes a printed
/// number. 27 rows ship, 19 at 0.2 and 8 at 0.
/// </summary>
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
                // An unversioned property left at its default is not serialized, and a float's
                // default is zero, which is a real band width here rather than a missing one. All
                // 27 shipped rows write the field, so this reading has never had to be exercised.
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

    /// <summary>The band a preset row declares, or null when no row of that name ships.</summary>
    public double? DefaultSpread(string? rowName) =>
        rowName is not null && _byRow.TryGetValue(rowName, out var spread) ? spread : null;

    /// <summary>The band a gameplay effect rolls in, read off the preset row it names.</summary>
    public double? Of(UObject effect) =>
        PropertyWalk.GetStruct(effect, PresetField) is { } handle
            ? DefaultSpread(PropertyWalk.GetString(handle, RowNameField))
            : null;
}
