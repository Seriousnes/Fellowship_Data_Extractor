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

    private const string OpEnum = GameplayEffects.OpEnum;
    private const string KindEnum = "EGameplayEffectMagnitudeCalculation";

    private const string ScalableFloat = "ScalableFloat";
    private const string AttributeBased = "AttributeBased";
    private const string CustomCalculationClass = "CustomCalculationClass";

    private const string TalentSegment = "Talent";
    private const string TalentsSegment = "Talents";
    private const int MaxGates = 16;

    private readonly record struct Slot(string Attribute, string? Op, IPropertyHolder Magnitude, bool Declared);

    public static EffectMagnitudes Of(
        UObject? cdo, AbstractFileProvider provider, ConstantsTable constants, TypeMappings mappings)
    {
        if (cdo is null) return EffectMagnitudes.None;

        var slots = Slots(cdo, mappings);
        if (slots.Count == 0) return EffectMagnitudes.None;

        var modifiers = new List<EffectModifier>();
        var talents = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            var written = PropertyWalk.GetString(slot.Magnitude, CalculationTypeField);
            var kind = EnumOrdinals.Member(mappings, KindEnum, written) ?? ScalableFloat;
            var custom = kind.Equals(CustomCalculationClass, StringComparison.Ordinal);

            var gates = custom ? new List<string>() : null;
            var read = Read(slot.Magnitude, kind, provider, constants, gates);
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
                });
            }

            if (gates is { Count: > 0 }) Vary(slot, read, gates, provider, constants, talents);
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
            : read.Answered || scaling?.Attribute is not null;
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
                    true));
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

    private static bool IsMagnitude(IPropertyHolder held) =>
        held.Properties.Any(property =>
            property.Name.Text.Equals(CalculationTypeField, StringComparison.Ordinal) ||
            property.Name.Text.Equals(ScalableField, StringComparison.Ordinal));

    private static MagnitudeReading Read(
        IPropertyHolder magnitude,
        string kind,
        AbstractFileProvider provider,
        ConstantsTable constants,
        ICollection<string>? gates)
    {
        if (kind.Equals(ScalableFloat, StringComparison.Ordinal))
        {
            return PropertyWalk.GetStruct(magnitude, ScalableField) is { } scalable &&
                   MagnitudeCalculations.Scalable(scalable, constants) is { } value
                ? new MagnitudeReading(Math.Round(value, 10), null, null)
                : MagnitudeReading.None;
        }

        return kind.Equals(CustomCalculationClass, StringComparison.Ordinal)
            ? MagnitudeCalculations.Reduced(magnitude, provider, constants, null, gates)
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
        Dictionary<string, Dictionary<string, double>> talents)
    {
        var seen = new HashSet<string>(bareGates, StringComparer.Ordinal);
        var pending = new Queue<string>(seen);

        while (pending.Count > 0 && seen.Count <= MaxGates)
        {
            var gate = pending.Dequeue();
            var found = new List<string>();

            var read = MagnitudeCalculations.Reduced(
                slot.Magnitude, provider, constants, new HashSet<string>([gate], StringComparer.Ordinal), found);

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
