using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record EffectMagnitudes(
    IReadOnlyList<EffectModifier> Modifiers,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Talents)
{
    public static readonly EffectMagnitudes None =
        new([], new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal));
}

public static class EffectModifiers
{
    private const string ModifiersField = "Modifiers";
    private const string AttributeField = "Attribute";
    private const string AttributeNameField = "AttributeName";
    private const string ModifierOpField = "ModifierOp";
    private const string MagnitudeField = "ModifierMagnitude";
    private const string DurationField = "DurationMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string AttributeBasedField = "AttributeBasedMagnitude";
    private const string CoefficientField = "Coefficient";
    private const string BackingField = "BackingAttribute";
    private const string CapturedField = "AttributeToCapture";
    private const string ComponentSuffix = "EffectComponent";
    private const string ExecutionsField = "Executions";
    private const string ScopedField = "CalculationModifiers";
    private const string ScopedCaptureField = "CapturedAttribute";
    private const string CaptureSourceField = "AttributeSource";
    private const string SnapshotField = "bSnapshot";
    private const string TransientIdentifierField = "TransientAggregatorIdentifier";
    private const string TagNameField = "TagName";
    private const string SetByCallerField = "SetByCallerMagnitude";
    private const string DataTagField = "DataTag";
    private const string SourceTagsField = "SourceTags";
    private const string TargetTagsField = "TargetTags";
    private const string RequireTagsField = "RequireTags";

    private const string OpEnum = GameplayEffects.OpEnum;
    private const string KindEnum = "EGameplayEffectMagnitudeCalculation";
    private const string CaptureSourceEnum = "EGameplayEffectAttributeCaptureSource";

    private const string ScalableFloat = "ScalableFloat";
    private const string AttributeBased = "AttributeBased";
    private const string CustomCalculationClass = "CustomCalculationClass";
    private const string SetByCaller = "SetByCaller";

    private const string TalentSegment = "Talent";
    private const string TalentsSegment = "Talents";
    private const int MaxGates = 16;

    private readonly record struct Slot(
        string Attribute, string? Op, IPropertyHolder Magnitude, bool Declared, IPropertyHolder? Modifier = null);

    public static EffectMagnitudes Of(
        UObject? cdo, AbstractFileProvider provider, ConstantsTable constants, TypeMappings mappings,
        Func<string, double?>? capturedBy)
    {
        if (cdo is null) return EffectMagnitudes.None;

        var slots = Slots(cdo, mappings);
        if (slots.Count == 0) return EffectMagnitudes.None;

        var effect = EffectContext.For(cdo, capturedBy);
        var modifiers = new List<EffectModifier>();
        var talents = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            var declared = PropertyWalk.GetString(slot.Magnitude, CalculationTypeField);
            var kind = EnumOrdinals.Member(mappings, KindEnum, declared) ?? ScalableFloat;
            var custom = kind.Equals(CustomCalculationClass, StringComparison.Ordinal);

            var gates = custom ? new List<string>() : null;
            var read = Read(slot.Magnitude, kind, provider, constants, gates, effect);
            var scaling = kind.Equals(AttributeBased, StringComparison.Ordinal)
                ? Captured(slot.Magnitude, constants)
                : null;

            if (Emits(slot, read, scaling))
            {
                modifiers.Add(new EffectModifier(slot.Attribute)
                {
                    Op = slot.Op,
                    Value = read.Value,
                    ValueLow = read.Low,
                    ValueHigh = read.High,
                    Coefficient = scaling?.Coefficient,
                    ScalesOff = scaling?.Attribute is { } captured ? [captured] : null,
                    SetByCaller = CallerTag(slot.Magnitude, kind),
                    SourceRequires = slot.Modifier is { } source ? Requires(source, SourceTagsField) : null,
                    TargetRequires = slot.Modifier is { } target ? Requires(target, TargetTagsField) : null,
                });
            }

            if (gates is { Count: > 0 }) Vary(slot, read, gates, provider, constants, talents, effect);
        }

        return new EffectMagnitudes(
            modifiers,
            talents.ToDictionary(
                talent => talent.Key,
                talent => (IReadOnlyDictionary<string, double>)talent.Value,
                StringComparer.Ordinal));
    }

    private static bool Emits(Slot slot, MagnitudeReading read, (double? Coefficient, string? Attribute)? scaling)
    {
        if (slot.Declared) return true;

        return slot.Attribute.Equals(DurationField, StringComparison.Ordinal)
            ? read.Low is not null
            : read.HasValue || scaling?.Attribute is not null;
    }

    private static List<Slot> Slots(UObject cdo, TypeMappings mappings)
    {
        var slots = new List<Slot>();

        if (PropertyWalk.Get(cdo, ModifiersField)?.GenericValue is UScriptArray declared)
        {
            foreach (var entry in declared.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
                if (PropertyWalk.GetStruct(modifier, AttributeField) is not { } attribute) continue;
                if (PropertyWalk.GetString(attribute, AttributeNameField) is not { } named) continue;
                if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

                slots.Add(new Slot(
                    named,
                    EnumOrdinals.Member(mappings, OpEnum, PropertyWalk.GetString(modifier, ModifierOpField)),
                    magnitude,
                    true,
                    modifier));
            }
        }

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (!export.ExportType.EndsWith(ComponentSuffix, StringComparison.Ordinal)) continue;

            foreach (var property in export.Properties)
            {
                if (property.Tag?.GenericValue is not FScriptStruct { StructType: IPropertyHolder held }) continue;
                if (!IsMagnitude(held)) continue;

                slots.Add(new Slot(property.Name.Text, null, held, false));
            }
        }

        if (PropertyWalk.GetStruct(cdo, DurationField) is { } duration)
        {
            slots.Add(new Slot(DurationField, null, duration, false));
        }

        return slots;
    }

    public static IReadOnlyList<EffectModifier> Scoped(UObject? cdo, ConstantsTable constants, TypeMappings mappings)
    {
        if (cdo is null) return [];
        if (PropertyWalk.Get(cdo, ExecutionsField)?.GenericValue is not UScriptArray executions) return [];

        var modifiers = new List<EffectModifier>();

        foreach (var execution in executions.Properties)
        {
            if (execution?.GenericValue is not FScriptStruct { StructType: IPropertyHolder held }) continue;
            if (PropertyWalk.Get(held, ScopedField)?.GenericValue is not UScriptArray scoped) continue;

            foreach (var entry in scoped.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
                if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

                var capture = PropertyWalk.GetStruct(modifier, ScopedCaptureField);
                var backing = capture is not null && PropertyWalk.GetStruct(capture, CapturedField) is { } captured
                    ? PropertyWalk.GetString(captured, AttributeNameField)
                    : null;
                var transient = PropertyWalk.GetStruct(modifier, TransientIdentifierField) is { } identifier
                    ? PropertyWalk.GetString(identifier, TagNameField)
                    : null;

                if ((backing ?? transient) is not { } attribute) continue;

                var kind = EnumOrdinals.Member(
                    mappings, KindEnum, PropertyWalk.GetString(magnitude, CalculationTypeField)) ?? ScalableFloat;
                double? value = kind.Equals(ScalableFloat, StringComparison.Ordinal) &&
                                PropertyWalk.GetStruct(magnitude, ScalableField) is { } scalable &&
                                MagnitudeCalculations.Scalable(scalable, constants) is { } number
                    ? Math.Round(number, 10)
                    : null;

                modifiers.Add(new EffectModifier(attribute)
                {
                    Op = EnumOrdinals.Member(mappings, OpEnum, PropertyWalk.GetString(modifier, ModifierOpField)),
                    Value = value,
                    SetByCaller = CallerTag(magnitude, kind),
                    SourceRequires = Requires(modifier, SourceTagsField),
                    TargetRequires = Requires(modifier, TargetTagsField),
                    CaptureSource = backing is null
                        ? null
                        : EnumOrdinals.Member(mappings, CaptureSourceEnum, PropertyWalk.GetString(capture!, CaptureSourceField)),
                    Snapshot = backing is null ? null : PropertyWalk.Get(capture!, SnapshotField)?.GenericValue is true,
                });
            }
        }

        return modifiers;
    }

    private static string? CallerTag(IPropertyHolder magnitude, string kind) =>
        kind.Equals(SetByCaller, StringComparison.Ordinal) &&
        PropertyWalk.GetStruct(magnitude, SetByCallerField) is { } caller
            ? PropertyWalk.GetString(caller, DataTagField)
            : null;

    private static IReadOnlyList<string>? Requires(IPropertyHolder modifier, string field) =>
        PropertyWalk.GetStruct(modifier, field) is { } requirements &&
        PropertyWalk.GetTags(requirements, RequireTagsField) is { Length: > 0 } tags
            ? tags
            : null;

    private static bool IsMagnitude(IPropertyHolder held) =>
        held.Properties.Any(property =>
            property.Name.Text.Equals(CalculationTypeField, StringComparison.Ordinal) ||
            property.Name.Text.Equals(ScalableField, StringComparison.Ordinal));

    private static MagnitudeReading Read(
        IPropertyHolder magnitude,
        string kind,
        AbstractFileProvider provider,
        ConstantsTable constants,
        ICollection<string>? gates,
        EffectContext? effect)
    {
        if (kind.Equals(ScalableFloat, StringComparison.Ordinal))
        {
            return PropertyWalk.GetStruct(magnitude, ScalableField) is { } scalable &&
                   MagnitudeCalculations.Scalable(scalable, constants) is { } value
                ? new MagnitudeReading(Math.Round(value, 10), null, null)
                : MagnitudeReading.None;
        }

        return kind.Equals(CustomCalculationClass, StringComparison.Ordinal)
            ? MagnitudeCalculations.Reduced(magnitude, provider, constants, null, gates, effect)
            : MagnitudeReading.None;
    }

    private static (double? Coefficient, string? Attribute)? Captured(
        IPropertyHolder magnitude, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(magnitude, AttributeBasedField) is not { } based) return null;

        var coefficient = PropertyWalk.GetStruct(based, CoefficientField) is { } scalable
            ? MagnitudeCalculations.Scalable(scalable, constants)
            : null;

        var captured = PropertyWalk.GetStruct(based, BackingField) is { } backing &&
                       PropertyWalk.GetStruct(backing, CapturedField) is { } attribute
            ? PropertyWalk.GetString(attribute, AttributeNameField)
            : null;

        return (coefficient is { } scaled ? Math.Round(scaled, 10) : null, captured);
    }

    private static void Vary(
        Slot slot,
        MagnitudeReading bare,
        IReadOnlyList<string> bareGates,
        AbstractFileProvider provider,
        ConstantsTable constants,
        Dictionary<string, Dictionary<string, double>> talents,
        EffectContext? effect)
    {
        var seen = new HashSet<string>(bareGates, StringComparer.Ordinal);
        var pending = new Queue<string>(seen);

        while (pending.Count > 0 && seen.Count <= MaxGates)
        {
            var gate = pending.Dequeue();
            var found = new List<string>();

            var read = MagnitudeCalculations.Reduced(
                slot.Magnitude, provider, constants, new HashSet<string>([gate], StringComparer.Ordinal), found, effect);

            foreach (var next in found)
            {
                if (seen.Add(next)) pending.Enqueue(next);
            }

            if (read.Value is not { } value || value == bare.Value) continue;
            if (Talent(gate) is not { } named) continue;

            if (!talents.TryGetValue(named, out var fields)) talents[named] = fields = new(StringComparer.Ordinal);

            fields[Field(slot.Attribute)] = value;
        }
    }

    private static string Field(string attribute) =>
        attribute.Equals(DurationField, StringComparison.Ordinal) ? Numbers.Duration : attribute;

    private static string? Talent(string tag)
    {
        var segments = tag.Split('.');

        var at = Array.FindIndex(
            segments,
            segment => segment.Equals(TalentSegment, StringComparison.Ordinal) ||
                       segment.Equals(TalentsSegment, StringComparison.Ordinal));

        return at < 0 || at + 1 >= segments.Length ? null : segments[^1];
    }
}
