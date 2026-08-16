using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

public sealed record CustomMagnitudes(CurveBinding? Duration, IReadOnlyList<string> Captures)
{
    public static readonly CustomMagnitudes None = new(null, []);
}

public sealed record MagnitudeReading(double? Value, double? Low, double? High)
{
    public static readonly MagnitudeReading None = new(null, null, null);

    public bool Answered => Value is not null || Low is not null;
}

public static class MagnitudeCalculations
{
    private const string Function = "CalculateBaseMagnitude";
    private const string DurationField = "DurationMagnitude";
    private const string CustomField = "CustomMagnitude";
    private const string ClassField = "CalculationClassMagnitude";
    private const string DurationSlot = "DurationMagnitude.CustomMagnitude.CalculationClassMagnitude";

    public static CustomMagnitudes Of(UObject cdo, AbstractFileProvider provider, ConstantsTable constants)
    {
        CurveBinding? duration = null;
        List<string>? captures = null;

        foreach (var property in PropertyWalk.Walk(cdo))
        {
            if (!property.Name.Equals(ClassField, StringComparison.Ordinal)) continue;
            if (PropertyWalk.AsString(property.Value) is not { } classPath) continue;
            if (Calculation(provider, classPath) is not { } calculation) continue;

            foreach (var attribute in GameplayEffects.Captures(calculation) ?? [])
            {
                if (!(captures ??= []).Contains(attribute)) captures.Add(attribute);
            }

            if (duration is not null) continue;
            if (!property.Path.Equals(DurationSlot, StringComparison.Ordinal)) continue;
            if (PropertyWalk.GetStruct(cdo, DurationField) is not { } magnitude) continue;
            if (PropertyWalk.GetStruct(magnitude, CustomField) is not { } custom) continue;
            var body = BareStateWalk.Compiled(calculation, Function);
            if (body?.ScriptBytecode is not { Length: > 0 } bytecode) continue;
            if (new BareStateWalk(body, calculation, constants, null).Answer() is not { } computed) continue;

            duration = new CurveBinding(
                "DurationMagnitude.ScalableFloatMagnitude",
                AbilityPresentationIndex.AssetName(classPath))
            {
                Value = Folded(custom, computed, constants),
                Computed = true,
                ComputedFrom = Rows(bytecode, calculation, constants),
            };
        }

        return duration is null && captures is null
            ? CustomMagnitudes.None
            : new CustomMagnitudes(duration, captures ?? []);
    }

    public static double? Custom(
        IPropertyHolder magnitude, AbstractFileProvider provider, ConstantsTable constants) =>
        Reduced(magnitude, provider, constants, null, null).Value;

    public static MagnitudeReading Reduced(
        IPropertyHolder magnitude,
        AbstractFileProvider provider,
        ConstantsTable constants,
        IReadOnlySet<string>? assumed,
        ICollection<string>? gates)
    {
        if (PropertyWalk.GetStruct(magnitude, CustomField) is not { } custom) return MagnitudeReading.None;
        if (PropertyWalk.GetString(custom, ClassField) is not { } classPath) return MagnitudeReading.None;
        if (Calculation(provider, classPath) is not { } calculation) return MagnitudeReading.None;

        if (BareStateWalk.Compiled(calculation, Function) is not { ScriptBytecode.Length: > 0 } body)
        {
            return MagnitudeReading.None;
        }

        var walk = new BareStateWalk(body, calculation, constants, null)
        {
            AssumedTags = assumed ?? new HashSet<string>(StringComparer.Ordinal),
        };

        var computed = walk.Answer();

        if (gates is not null)
        {
            foreach (var gate in walk.Gates) gates.Add(gate);
        }

        if (computed is { } answer) return new MagnitudeReading(Folded(custom, answer, constants), null, null);
        if (walk.Range is not { } range) return MagnitudeReading.None;

        return new MagnitudeReading(
            null, Folded(custom, range.Low, constants), Folded(custom, range.High, constants));
    }

    private static double Folded(IPropertyHolder custom, double computed, ConstantsTable constants) =>
        Math.Round(
            Scaled(custom, "Coefficient", 1, constants)
            * (computed + Scaled(custom, "PreMultiplyAdditiveValue", 0, constants))
            + Scaled(custom, "PostMultiplyAdditiveValue", 0, constants),
            10);

    private static UObject? Calculation(AbstractFileProvider provider, string classPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package)) return null;

        return package.GetExports()
            .FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
    }

    private static double Scaled(IPropertyHolder custom, string field, double fallback, ConstantsTable constants) =>
        PropertyWalk.GetStruct(custom, field) is { } scalable
            ? Scalable(scalable, constants) ?? fallback
            : fallback;

    public static double? Scalable(IPropertyHolder scalable, ConstantsTable constants)
    {
        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue);
        var row = PropertyWalk.GetStruct(scalable, "Curve") is { } curve
            ? PropertyWalk.GetString(curve, "RowName")
            : null;

        if (row is null || row.Length == 0 || row.Equals("None", StringComparison.Ordinal)) return value;

        return constants.Lookup(row).Record?.Value is { } resolved ? (value ?? 1) * resolved : null;
    }

    public static double? Reduce(UObject calculation, ConstantsTable constants) =>
        BareStateWalk.Compiled(calculation, Function) is { ScriptBytecode.Length: > 0 } body
            ? new BareStateWalk(body, calculation, constants, null).Answer()
            : null;

    private static string[] Rows(KismetExpression[] bytecode, UObject calculation, ConstantsTable constants)
    {
        var rows = new List<string>();

        foreach (var binding in CurveBindings.Collect(calculation, constants)) rows.Add(binding.RowName);
        foreach (var statement in bytecode) InlineRows(statement, rows, 0);

        return [.. rows.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void InlineRows(KismetExpression? expression, List<string> rows, int depth)
    {
        if (expression is null || depth > 24) return;

        if (expression is EX_StructConst { Struct.Name: "CurveTableRowHandle" } handle)
        {
            foreach (var name in handle.Properties.OfType<EX_NameConst>()) rows.Add(name.Value.Text);
            return;
        }

        foreach (var child in Children(expression)) InlineRows(child, rows, depth + 1);
    }

    private static IEnumerable<KismetExpression?> Children(KismetExpression expression) => expression switch
    {
        EX_Let let => [let.Variable, let.Assignment],
        EX_LetObj let => [let.Variable, let.Assignment],
        EX_LetBool let => [let.Variable, let.Assignment],
        EX_Context context => [context.ObjectExpression, context.ContextExpression],
        EX_Cast cast => [cast.Target],
        EX_Return returned => [returned.ReturnExpression],
        EX_JumpIfNot jump => [jump.BooleanExpression],
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        EX_StructConst constant => constant.Properties,
        EX_SwitchValue chosen =>
            [chosen.IndexTerm, .. chosen.Cases.SelectMany(c => new KismetExpression?[] { c.CaseIndexValueTerm, c.CaseTerm }), chosen.DefaultTerm],
        EX_StructMemberContext member => [member.StructExpression],
        _ => [],
    };
}
