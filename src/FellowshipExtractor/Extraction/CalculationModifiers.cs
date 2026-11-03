using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The attribute-based modifiers an effect's executions apply. Each execution in <c>Executions</c>, inherited from the
/// nearest class declaring it, names its calculation class and lists its <c>CalculationModifiers</c>. A modifier whose
/// magnitude is <c>AttributeBased</c> and evaluates the backing attribute's magnitude is read, onto a captured
/// attribute or a transient aggregator. Seething Poison's Creeping Death modifier is one.
/// </summary>
public static class CalculationModifiers
{
    private const string ExecutionsField = "Executions";
    private const string ClassField = "CalculationClass";
    private const string ModifiersField = "CalculationModifiers";
    private const string CapturedField = "CapturedAttribute";
    private const string CaptureField = "AttributeToCapture";
    private const string AttributeNameField = "AttributeName";
    private const string SourceField = "AttributeSource";
    private const string SnapshotField = "bSnapshot";
    private const string AggregatorTypeField = "AggregatorType";
    private const string TransientAggregator = "Transient";
    private const string TransientIdentifierField = "TransientAggregatorIdentifier";
    private const string TagNameField = "TagName";
    private const string OpField = "ModifierOp";
    private const string MagnitudeField = "ModifierMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string AttributeBased = "AttributeBased";
    private const string AttributeBasedField = "AttributeBasedMagnitude";
    private const string AttributeCalculationTypeField = "AttributeCalculationType";
    private const string AttributeMagnitude = "AttributeMagnitude";
    private const string CoefficientField = "Coefficient";
    private const string PreMultiplyField = "PreMultiplyAdditiveValue";
    private const string PostMultiplyField = "PostMultiplyAdditiveValue";
    private const string BackingField = "BackingAttribute";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string CurveTableField = "CurveTable";
    private const string RowField = "RowName";
    private const string NoName = "None";

    public static IReadOnlyList<CalculationModifier> Of(UObject effect, AbstractFileProvider provider, ConstantsTable constants)
    {
        if (ClassDefaults.Inherited(effect, ExecutionsField, provider)?.GenericValue is not UScriptArray executions) return [];

        var found = new List<CalculationModifier>();

        foreach (var element in executions.Properties)
        {
            if (element?.GenericValue is not FScriptStruct { StructType: IPropertyHolder execution }) continue;
            if (Leaf(PropertyWalk.GetString(execution, ClassField)) is not { } calculation) continue;
            if (PropertyWalk.Get(execution, ModifiersField)?.GenericValue is not UScriptArray modifiers) continue;

            foreach (var entry in modifiers.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;

                if (Read(calculation, modifier, provider, constants, effect.Name) is { } read && !found.Contains(read))
                {
                    found.Add(read);
                }
            }
        }

        return found;
    }

    private static CalculationModifier? Read(
        string calculation, IPropertyHolder modifier, AbstractFileProvider provider, ConstantsTable constants, string effect)
    {
        if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) return null;
        if (Member(PropertyWalk.GetString(magnitude, CalculationTypeField)) != AttributeBased) return null;
        if (PropertyWalk.GetStruct(magnitude, AttributeBasedField) is not { } based) return null;
        if (Member(PropertyWalk.GetString(based, AttributeCalculationTypeField)) is { } type && type != AttributeMagnitude) return null;
        if (Captured(PropertyWalk.GetStruct(based, BackingField)) is not { Name: { } backing } capture) return null;
        if (Member(PropertyWalk.GetString(modifier, OpField)) is not { } op) return null;

        var transient = Member(PropertyWalk.GetString(modifier, AggregatorTypeField)) == TransientAggregator &&
                        PropertyWalk.GetStruct(modifier, TransientIdentifierField) is { } identifier
            ? PropertyWalk.GetString(identifier, TagNameField)
            : null;
        var attribute = transient is null ? Captured(PropertyWalk.GetStruct(modifier, CapturedField)).Name : null;

        if (attribute is null && transient is null) return null;

        var coefficient = Scalable(PropertyWalk.GetStruct(based, CoefficientField), provider, constants, effect, 1);
        var pre = Scalable(PropertyWalk.GetStruct(based, PreMultiplyField), provider, constants, effect, 0);
        var post = Scalable(PropertyWalk.GetStruct(based, PostMultiplyField), provider, constants, effect, 0);

        if (pre.Curve is not null || post.Curve is not null)
        {
            throw new InvalidOperationException(
                $"{effect} reads an additive value of an attribute-based calculation modifier from a curve that " +
                "varies with level.");
        }

        return new CalculationModifier(calculation, op, backing)
        {
            Attribute = attribute,
            Transient = transient,
            BackingSource = capture.Source,
            BackingSnapshot = capture.Snapshot ? true : null,
            Row = coefficient.Row,
            Coefficient = coefficient.Value,
            CoefficientCurve = coefficient.Curve,
            PreMultiplyAdditiveValue = pre.Value,
            PostMultiplyAdditiveValue = post.Value,
        };
    }

    private static (string? Name, string? Source, bool Snapshot) Captured(IPropertyHolder? definition)
    {
        if (definition is null) return (null, null, false);

        var name = PropertyWalk.GetStruct(definition, CaptureField) is { } capture
            ? PropertyWalk.GetString(capture, AttributeNameField)
            : null;
        var snapshot = PropertyWalk.Get(definition, SnapshotField)?.GenericValue is true;

        return (name, Member(PropertyWalk.GetString(definition, SourceField)), snapshot);
    }

    /// <summary>
    /// A scalable float's value: its scale times its row. A curve table row whose keys all hold one value reads as that
    /// value, and one whose keys differ is returned as the curve beside the scale.
    /// </summary>
    private static (double Value, string? Row, Curve? Curve) Scalable(
        IPropertyHolder? scalable, AbstractFileProvider provider, ConstantsTable constants, string effect, double absent)
    {
        if (scalable is null) return (absent, null, null);

        var scale = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? absent;

        if (PropertyWalk.GetStruct(scalable, CurveField) is not { } handle) return (scale, null, null);
        if (PropertyWalk.GetString(handle, RowField) is not { } row || row == NoName) return (scale, null, null);

        if (PropertyWalk.GetString(handle, CurveTableField) is { } table && table != NoName)
        {
            var curve = CurveReader.Read(provider, table, row);
            var values = curve.Points.Select(point => point.Value).Distinct().ToArray();

            return values.Length == 1 ? (Math.Round(scale * values[0], 10), row, null) : (scale, row, curve);
        }

        return constants.Lookup(row).Record?.Value is { } declared
            ? (Math.Round(scale * declared, 10), row, null)
            : throw new InvalidOperationException($"{effect} reads the tuning row {row}, which no constants table has.");
    }

    /// <summary>The member of an enum value written as <c>Enum::Member</c>.</summary>
    private static string? Member(string? value) =>
        value is null ? null : value[(value.LastIndexOf(':') + 1)..];

    /// <summary>The class name of a class path, without the package or the <c>_C</c> suffix.</summary>
    private static string? Leaf(string? path)
    {
        if (path is null) return null;

        var name = path[(path.LastIndexOfAny(['.', '/']) + 1)..];

        return name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name;
    }
}
