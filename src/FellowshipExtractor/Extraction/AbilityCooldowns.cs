using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The cooldown <c>UCRGameplayAbility::ApplyCooldown</c> starts for a character holding no talents. The rule is in
/// <c>docs/cooldown-haste.md</c>.
/// </summary>
public sealed class AbilityCooldowns(AbstractFileProvider provider, ConstantsTable constants, DataRegistries registries)
{
    private const string DurationField = "CooldownDuration";
    private const string TagsField = "CooldownTags";
    private const string CooldownClassField = "CooldownEffectBaseClass";
    private const string ChargeClassField = "ChargeGameplayEffectClass";
    private const string DurationPolicyField = "DurationPolicy";
    private const string HasDuration = "EGameplayEffectDurationType::HasDuration";
    private const string NativeChargeEffect = "/Script/fellowship.CRChargeEffect";
    private const double SingleCharge = 1;

    public bool Declares(UObject ability) => ClassDefaults.Inherited(ability, DurationField, provider) is not null;

    public double? Of(UObject ability, ChargeCounts charges) => Read(ability, charges).Seconds;

    /// <summary>The cooldown, with the row its <c>CooldownDuration</c> reads.</summary>
    public (double? Seconds, FieldSource? Source) Read(UObject ability, ChargeCounts charges)
    {
        if (ClassDefaults.Inherited(ability, DurationField, provider)?.GenericValue
            is not FScriptStruct { StructType: IPropertyHolder scalable })
        {
            return (null, null);
        }

        if (Evaluated(scalable) is not (> 0 and var seconds)) return (null, null);
        if (PropertyWalk.AsTags(ClassDefaults.Inherited(ability, TagsField, provider)?.GenericValue) is not { Length: > 0 }) return (null, null);

        return OnChargeEffect(ability, charges) || Class(ability, CooldownClassField) is not null
            ? (seconds, Source(scalable))
            : (null, null);
    }

    /// <summary>The row times the <c>Value</c>, where a <c>Value</c> of 1 leaves the row's value.</summary>
    private static FieldSource? Source(IPropertyHolder scalable)
    {
        var row = PropertyWalk.GetStruct(scalable, "Curve") is { } curve ? PropertyWalk.GetString(curve, "RowName") : null;
        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue) ?? 0;

        return FieldSources.Scalable(row, value);
    }

    /// <summary>
    /// <c>FScalableFloat</c> at 0x144AA85B0. A row reached through neither a table nor a registry is invalid, and the
    /// constructor's <c>Value</c> of 0 stands for a <c>Value</c> the asset omits.
    /// </summary>
    private double? Evaluated(IPropertyHolder scalable)
    {
        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue) ?? 0;
        var curve = PropertyWalk.GetStruct(scalable, "Curve");

        if (curve is null || PropertyWalk.GetString(curve, "RowName") is not { } row) return value;

        return (PropertyWalk.GetString(curve, "CurveTable") is { } table
            ? TableRow(table, row)
            : CurveBindings.RegistryName(scalable) is { } registry
                ? constants.In(row, file => registries.Scans(registry, file))?.Value
                : null) * value;
    }

    private double? TableRow(string table, string row)
    {
        if (!provider.TryLoadPackageObject<UCurveTable>(AbilityPresentationIndex.PackagePath(table), out var curves))
        {
            return null;
        }

        var found = curves.RowMap.FirstOrDefault(entry => entry.Key.Text.Equals(row, StringComparison.Ordinal)).Value;

        return found is not null && CurveRows.Keys(found) is { Count: > 0 } keys ? keys[0].Value : null;
    }

    private bool OnChargeEffect(UObject ability, ChargeCounts charges) =>
        (charges.Charges > SingleCharge || charges.ByLevelType?.Values.Any(count => count > SingleCharge) is true) &&
        Class(ability, ChargeClassField) is { } charge &&
        HasDurationPolicy(charge);

    private string? Class(UObject ability, string field) =>
        PropertyWalk.AsString(ClassDefaults.Inherited(ability, field, provider)?.GenericValue);

    private bool HasDurationPolicy(string classPath)
    {
        if (classPath == NativeChargeEffect) return true;
        if (ClassDefaults.Load(classPath, provider) is not { } effect) return false;

        return PropertyWalk.AsString(ClassDefaults.Inherited(effect, DurationPolicyField, provider)?.GenericValue) is { } policy
            ? policy == HasDuration
            : ClassDefaults.NativeParent(effect, provider) == NativeChargeEffect;
    }
}
