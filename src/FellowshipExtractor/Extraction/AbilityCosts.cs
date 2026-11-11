using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record AbilityCost(double Amount, bool ReadsPool)
{
    public FieldSource? Source { get; init; }
}

public static class AbilityCosts
{
    private const string CostEffectField = "CostGameplayEffectClass";
    private const string ModifiersField = "Modifiers";
    private const string MagnitudeField = "ModifierMagnitude";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";
    private const string Function = "ModifyAbilityCostForUI";
    private const string CostLeaf = "cost";

    private static readonly string[] TalentSegments = ["Talent", "Talents"];

    public static AbilityCost? Of(
        UObject? cdo,
        AbstractFileProvider provider,
        ConstantsTable constants,
        string? tuning,
        Func<string, double?>? captured)
    {
        var spent = FromCostEffect(cdo, provider, constants, captured);
        var stated = spent ?? FromNamespace(constants, tuning);

        if (Adjusted(cdo, constants, stated) is not { } adjusted || adjusted.Amount == 0) return null;

        return adjusted with { ReadsPool = spent is { ReadsPool: true } && adjusted.Amount == spent.Amount };
    }

    private static AbilityCost? FromCostEffect(
        UObject? cdo, AbstractFileProvider provider, ConstantsTable constants, Func<string, double?>? captured)
    {
        if (cdo is null) return null;
        if (PropertyWalk.AsString(ClassDefaults.Inherited(cdo, CostEffectField, provider)?.GenericValue) is not { } classPath)
        {
            return null;
        }
        if (Effect(provider, classPath) is not { } effect) return null;
        if (PropertyWalk.Get(effect, ModifiersField)?.GenericValue is not UScriptArray modifiers) return null;

        var context = EffectContext.For(effect, captured);

        foreach (var entry in modifiers.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
            if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

            if (Scalable(magnitude, constants) is { } stated) return stated;

            var custom = MagnitudeCalculations.Custom(magnitude, provider, constants, context);

            if (custom.Value is { } cost)
            {
                return new AbilityCost(Math.Abs(cost), custom.ReadsCapture)
                {
                    Source = FieldSources.As(custom.Source, custom.ReadsCapture ? ResourceGains.PoolFraction : ResourceGains.RowProduct),
                };
            }
        }

        return null;
    }

    private static UObject? Effect(AbstractFileProvider provider, string classPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package)) return null;

        return package.GetExports()
            .FirstOrDefault(export => export.Name.StartsWith("Default__", StringComparison.Ordinal));
    }

    private static AbilityCost? Scalable(IPropertyHolder magnitude, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(magnitude, ScalableField) is not { } scalable) return null;
        if (PropertyWalk.GetStruct(scalable, CurveField) is not { } curve) return null;
        if (PropertyWalk.GetString(curve, RowField) is not { } row) return null;
        if (row.Equals(NoRow, StringComparison.Ordinal)) return null;
        if (constants.Lookup(row).Record?.Value is not { } value) return null;

        var multiplier = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue);

        return new AbilityCost(Math.Abs((multiplier ?? 1) * value), false)
        {
            Source = FieldSources.As(
                FieldSources.Row(row),
                multiplier is null or 1 && value >= 0 ? ResourceGains.ConstantsRow : ResourceGains.RowProduct),
        };
    }

    private static AbilityCost? FromNamespace(ConstantsTable constants, string? tuning)
    {
        if (tuning is null) return null;

        double? stated = null;
        var rows = new List<string>();

        foreach (var record in constants.DirectlyUnder(tuning))
        {
            var relative = TuningNamespace.Relative(record.Key, tuning);

            if (Array.Exists(
                relative.Split('.'), segment => TalentSegments.Contains(segment, StringComparer.Ordinal)))
            {
                continue;
            }

            if (!Numbers.Normalize(relative).EndsWith(CostLeaf, StringComparison.Ordinal)) continue;
            if (record.Value is not { } value) continue;
            if (stated is { } agreed && agreed != value) return null;

            stated = value;
            rows.Add(record.Key);
        }

        return stated is { } amount
            ? new AbilityCost(amount, false)
            {
                Source = FieldSources.Of(rows.Count == 1 ? ResourceGains.ConstantsRow : ResourceGains.RowProduct, rows),
            }
            : null;
    }

    /// <summary>The cost <c>ModifyAbilityCostForUI</c> returns, where it returns one.</summary>
    private static AbilityCost? Adjusted(UObject? cdo, ConstantsTable constants, AbilityCost? stated)
    {
        if (cdo is null) return stated;
        if (BareStateWalk.Compiled(cdo, Function) is not { ScriptBytecode.Length: > 0 } body) return stated;

        var walk = new BareStateWalk(body, cdo, constants, null);

        return walk.Answer() is { } answer ? new AbilityCost(answer, false) { Source = walk.Answered } : stated;
    }
}
