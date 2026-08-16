using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

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

    public static double? Of(UObject? cdo, AbstractFileProvider provider, ConstantsTable constants, string? tuning)
    {
        var stated = FromCostEffect(cdo, provider, constants) ?? FromNamespace(constants, tuning);

        return Adjusted(cdo, constants, stated);
    }

    private static double? FromCostEffect(UObject? cdo, AbstractFileProvider provider, ConstantsTable constants)
    {
        if (cdo is null) return null;
        if (PropertyWalk.GetString(cdo, CostEffectField) is not { } classPath) return null;
        if (Effect(provider, classPath) is not { } effect) return null;
        if (PropertyWalk.Get(effect, ModifiersField)?.GenericValue is not UScriptArray modifiers) return null;

        foreach (var entry in modifiers.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
            if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

            var spent = Scalable(magnitude, constants)
                ?? MagnitudeCalculations.Custom(magnitude, provider, constants);

            if (spent is { } cost) return Math.Abs(cost);
        }

        return null;
    }

    private static UObject? Effect(AbstractFileProvider provider, string classPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package)) return null;

        return package.GetExports()
            .FirstOrDefault(export => export.Name.StartsWith("Default__", StringComparison.Ordinal));
    }

    private static double? Scalable(IPropertyHolder magnitude, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(magnitude, ScalableField) is not { } scalable) return null;
        if (PropertyWalk.GetStruct(scalable, CurveField) is not { } curve) return null;
        if (PropertyWalk.GetString(curve, RowField) is not { } row) return null;
        if (row.Equals(NoRow, StringComparison.Ordinal)) return null;
        if (constants.Lookup(row).Record?.Value is not { } value) return null;

        return (PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1) * value;
    }

    private static double? FromNamespace(ConstantsTable constants, string? tuning)
    {
        if (tuning is null) return null;

        double? stated = null;

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
        }

        return stated;
    }

    private static double? Adjusted(UObject? cdo, ConstantsTable constants, double? stated)
    {
        if (cdo is null) return stated;
        if (BareStateWalk.Compiled(cdo, Function) is not { ScriptBytecode.Length: > 0 } body) return stated;

        return new BareStateWalk(body, cdo, constants, null).Answer() ?? stated;
    }
}
