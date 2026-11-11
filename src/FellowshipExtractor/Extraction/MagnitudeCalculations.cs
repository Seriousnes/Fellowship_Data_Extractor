using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

public sealed record CustomMagnitudes(CurveBinding? Duration, IReadOnlyList<string> Captures)
{
    public static readonly CustomMagnitudes None = new(null, []);

    public IReadOnlyList<UObject> Calculators { get; init; } = [];
}

public sealed record MagnitudeReading(double? Value, double? Low, double? High)
{
    public static readonly MagnitudeReading None = new(null, null, null);

    public bool HasValue => Value is not null || Low is not null;

    public bool ReadsCapture { get; init; }

    public IReadOnlyList<double>? Ranks { get; init; }
}

/// <summary>A custom calculation's magnitude written out over what it captures.</summary>
public sealed record CalculationFormula(double Coefficient, IReadOnlyList<string> Attributes, string Text);

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
        var calculators = new List<UObject>();
        var properties = PropertyWalk.Walk(cdo).ToArray();
        var gated = TalentGated(properties);

        foreach (var property in properties)
        {
            if (!property.Name.Equals(ClassField, StringComparison.Ordinal)) continue;
            if (PropertyWalk.AsString(property.Value) is not { } classPath) continue;
            if (Calculation(provider, classPath) is not { } calculation) continue;

            if (!calculators.Contains(calculation)) calculators.Add(calculation);

            if (!gated.Any(scope => property.Path.StartsWith(scope, StringComparison.Ordinal)) &&
                BareStateWalk.Compiled(calculation, Function) is { ScriptBytecode: { Length: > 0 } read })
            {
                foreach (var attribute in ReadCaptures(calculation, provider, read))
                {
                    if (!(captures ??= []).Contains(attribute)) captures.Add(attribute);
                }
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

        return duration is null && captures is null && calculators.Count == 0
            ? CustomMagnitudes.None
            : new CustomMagnitudes(duration, captures ?? []) { Calculators = calculators };
    }

    private const string RequireTagsField = "RequireTags";
    private const string TalentTagPrefix = "Talents.";
    private const string CapturesField = "RelevantAttributesToCapture";
    private const string CaptureField = "AttributeToCapture";
    private const string AttributeNameField = "AttributeName";
    private const string CaptureRead = "GetCapturedAttribute";
    private const string ElementRead = "Array_Get";

    private static string[] TalentGated(IEnumerable<WalkedProperty> properties) =>
    [
        .. properties
            .Where(property =>
                property.Name.Equals(RequireTagsField, StringComparison.Ordinal) &&
                PropertyWalk.AsTags(property.Value)?.Any(tag => tag.StartsWith(TalentTagPrefix, StringComparison.Ordinal)) is true)
            .Select(property => property.Path.Split('.') is { Length: > 2 } segments
                ? string.Join('.', segments[..^2]) + '.'
                : null)
            .OfType<string>(),
    ];

    public static IEnumerable<string> ReadCaptures(
        UObject calculation, AbstractFileProvider provider, IEnumerable<KismetExpression> statements)
    {
        var declared = Declared(calculation, provider);
        var elements = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            if (statement is EX_Context { ContextExpression: EX_FinalFunction { StackNode.Name: ElementRead } element } &&
                DynamicTextFloats.Name(element.Parameters.ElementAtOrDefault(2)) is { } item)
            {
                elements[item] = element.Parameters.ElementAtOrDefault(1) is EX_IntConst at ? at.Value : -1;
            }

            foreach (var call in Calls(statement, 0))
            {
                int? index = call.Parameters.ElementAtOrDefault(1) switch
                {
                    EX_ArrayGetByRef { ArrayIndex: EX_IntConst constant } => constant.Value,
                    EX_ArrayGetByRef => -1,
                    var local when DynamicTextFloats.Name(local) is { } name && elements.TryGetValue(name, out var bound) => bound,
                    _ => null,
                };

                if (index is -1)
                {
                    foreach (var any in declared.OfType<string>()) yield return any;
                }
                else if (index is { } found && declared.ElementAtOrDefault(found) is { } attribute)
                {
                    yield return attribute;
                }
            }
        }
    }

    private static IEnumerable<EX_CallMath> Calls(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > 24) yield break;

        if (expression is EX_CallMath call && call.StackNode.Name.Equals(CaptureRead, StringComparison.Ordinal))
        {
            yield return call;
        }

        foreach (var child in Children(expression))
        {
            foreach (var found in Calls(child, depth + 1)) yield return found;
        }
    }

    private static string?[] Declared(UObject calculation, AbstractFileProvider provider) =>
        ClassDefaults.Inherited(calculation, CapturesField, provider)?.GenericValue is UScriptArray captures
            ?
            [
                .. captures.Properties.Select(element =>
                    element?.GenericValue switch
                    {
                        FScriptStruct { StructType: IPropertyHolder capture } => capture,
                        IPropertyHolder capture => capture,
                        _ => null,
                    } is { } held && PropertyWalk.GetStruct(held, CaptureField) is { } attribute
                        ? PropertyWalk.GetString(attribute, AttributeNameField)
                        : null),
            ]
            : [];

    public static MagnitudeReading Custom(
        IPropertyHolder magnitude, AbstractFileProvider provider, ConstantsTable constants, EffectContext? effect) =>
        Reduced(magnitude, provider, constants, null, null, effect);

    public static MagnitudeReading Reduced(
        IPropertyHolder magnitude,
        AbstractFileProvider provider,
        ConstantsTable constants,
        IReadOnlySet<string>? assumed,
        ICollection<string>? gates,
        EffectContext? effect)
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
            Effect = effect,
        };

        var computed = walk.Answer();

        if (gates is not null)
        {
            foreach (var gate in walk.Gates) gates.Add(gate);
        }

        if (computed is { } answer)
        {
            return new MagnitudeReading(Folded(custom, answer, constants), null, null) { ReadsCapture = walk.ReadsCapture };
        }
        if (walk.Range is not { } range) return MagnitudeReading.None;

        return new MagnitudeReading(
            null, Folded(custom, range.Low, constants), Folded(custom, range.High, constants));
    }

    /// <summary>
    /// The magnitude of a custom calculation whose <c>CalculateBaseMagnitude</c> runs one straight line of arithmetic
    /// over captured attributes, the effect's stack count and numbers. The magnitude is
    /// <c>Coefficient × (calculation + PreMultiplyAdditiveValue) + PostMultiplyAdditiveValue</c>, so
    /// <c>GMMC_Shared_InverseStackCountHasteMultiplier</c>'s <c>(Haste - 1) / Stacks + 1</c> loses its 1 to a
    /// PreMultiplyAdditiveValue of -1.
    /// </summary>
    public static CalculationFormula? Formula(IPropertyHolder magnitude, AbstractFileProvider provider, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(magnitude, CustomField) is not { } custom) return null;
        if (PropertyWalk.GetString(custom, ClassField) is not { } classPath) return null;
        if (Calculation(provider, classPath) is not { } calculation) return null;
        if (BareStateWalk.Compiled(calculation, Function)?.ScriptBytecode is not { Length: > 0 } statements) return null;
        if (statements.Any(statement => statement is EX_JumpIfNot or EX_Jump or EX_ComputedJump)) return null;

        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            if (statement is EX_Let { Variable: { } variable, Assignment: { } value } &&
                DynamicTextFloats.Name(variable) is { } local)
            {
                locals[local] = value;
            }
        }

        var returned = statements.OfType<EX_Return>().Select(statement => statement.ReturnExpression).FirstOrDefault();
        var attributes = new List<string>();

        if (Term(returned, locals, Declared(calculation, provider), attributes, 0) is not { Text: not null } term) return null;

        var coefficient = Math.Round(Scaled(custom, "Coefficient", 1, constants), 10);

        var added = term with
        {
            Addend = Math.Round(term.Addend + Scaled(custom, "PreMultiplyAdditiveValue", 0, constants), 10),
        };

        var scaled = coefficient == 1
            ? added.Written
            : $"{Number(coefficient)}{Times}{added.Factor}";

        var post = Scaled(custom, "PostMultiplyAdditiveValue", 0, constants);

        return new CalculationFormula(
            coefficient,
            [.. attributes.Distinct(StringComparer.Ordinal)],
            post == 0 ? scaled : $"{scaled}{Signed(post)}");
    }

    /// <summary>
    /// A custom calculation's magnitude as a coefficient on one captured attribute, with the bracket the attribute is
    /// multiplied by. <c>GMMC_Warmaster_Toughness_Max</c> under a Coefficient of
    /// <c>Warmaster.Toughness.MaxToughnessStrengthScaler</c> is 7.7 of Strength times
    /// <c>2 × (CritChance + Haste + Expertise + Spirit) - 1</c>. A magnitude with a PreMultiplyAdditiveValue or a
    /// PostMultiplyAdditiveValue other than 0 has no such reading.
    /// </summary>
    public static DynamicTextReading? Reading(
        IPropertyHolder magnitude, AbstractFileProvider provider, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(magnitude, CustomField) is not { } custom) return null;
        if (PropertyWalk.GetString(custom, ClassField) is not { } classPath) return null;
        if (Calculation(provider, classPath) is not { } calculation) return null;
        if (BareStateWalk.Compiled(calculation, Function)?.ScriptBytecode is not { Length: > 0 } statements) return null;
        if (Scaled(custom, "PreMultiplyAdditiveValue", 0, constants) != 0) return null;
        if (Scaled(custom, "PostMultiplyAdditiveValue", 0, constants) != 0) return null;

        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            if (DynamicTextFloats.Assigned(statement) is not var (variable, assignment)) continue;
            if (DynamicTextFloats.Name(variable) is { } local) locals[local] = assignment;
        }

        if (!locals.TryGetValue(ReturnedLocal, out var returned)) return null;

        var reader = new DynamicTextArithmetic(locals, 0, _ => null)
        {
            Captures = Declared(calculation, provider),
            Rows = row => constants.Lookup(row).Record?.Value,
        };

        if (reader.Of(returned) is not { Attribute: not null } value) return null;

        var coefficient = Scaled(custom, "Coefficient", 1, constants);

        return DynamicTextArithmetic.Published(value with { Scale = value.Scale * coefficient });
    }

    private const string ReturnedLocal = "ReturnValue";

    private const string Times = " × ";
    private const string StackCountRead = "GetGameplayEffectStackCount";

    /// <summary>A sum of <see cref="Text"/> and a number. A null <see cref="Text"/> is the number alone.</summary>
    private sealed record FormulaTerm(string? Text, double Addend, bool Sum)
    {
        public string Written => Text is null ? Number(Addend) : Addend == 0 ? Text : $"{Text}{Signed(Addend)}";

        public string Factor => Text is not null && (Sum || Addend != 0) ? $"({Written})" : Written;
    }

    private static string Number(double value) => value.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture);

    private static string Signed(double value) => value < 0 ? $" - {Number(-value)}" : $" + {Number(value)}";

    private static FormulaTerm? Term(
        KismetExpression? expression,
        IReadOnlyDictionary<string, KismetExpression> locals,
        string?[] declared,
        List<string> attributes,
        int depth)
    {
        if (expression is null || depth > 24) return null;

        FormulaTerm? Next(KismetExpression? inner) => Term(inner, locals, declared, attributes, depth + 1);

        switch (expression)
        {
            case EX_DoubleConst value: return new FormulaTerm(null, value.Value, false);
            case EX_FloatConst value: return new FormulaTerm(null, value.Value, false);
            case EX_IntConst value: return new FormulaTerm(null, value.Value, false);
            case EX_Cast cast: return Next(cast.Target);
            case EX_LocalVariable or EX_LocalOutVariable:
                return DynamicTextFloats.Name(expression) is { } local && locals.TryGetValue(local, out var assigned)
                    ? Next(assigned)
                    : null;
            case EX_CallMath call:
                return Called(call);
            default:
                return null;
        }

        FormulaTerm? Called(EX_CallMath call)
        {
            var name = call.StackNode.Name;

            if (name == CaptureRead)
            {
                if (call.Parameters.ElementAtOrDefault(1) is not EX_ArrayGetByRef { ArrayIndex: EX_IntConst index }) return null;
                if (declared.ElementAtOrDefault(index.Value) is not { } attribute) return null;

                attributes.Add(attribute);

                return new FormulaTerm(attribute, 0, false);
            }

            if (name == StackCountRead) return new FormulaTerm(Fellowship.SDK.Models.EffectModifier.Stacks, 0, false);
            if (name.StartsWith("Conv_", StringComparison.Ordinal)) return Next(call.Parameters.FirstOrDefault());
            if (call.Parameters.Length != 2 || Next(call.Parameters[0]) is not { } left || Next(call.Parameters[1]) is not { } right) return null;

            if (name.StartsWith("Add_", StringComparison.Ordinal)) return Added(left, right, 1);
            if (name.StartsWith("Subtract_", StringComparison.Ordinal)) return Added(left, right, -1);
            if (name.StartsWith("Multiply_", StringComparison.Ordinal)) return new FormulaTerm($"{left.Factor}{Times}{right.Factor}", 0, false);
            if (name.StartsWith("Divide_", StringComparison.Ordinal)) return new FormulaTerm($"{left.Factor} / {right.Factor}", 0, false);

            return null;
        }
    }

    private static FormulaTerm Added(FormulaTerm left, FormulaTerm right, int sign)
    {
        var addend = left.Addend + sign * right.Addend;

        return (left.Text, right.Text) switch
        {
            (null, null) => new FormulaTerm(null, addend, false),
            (_, null) => left with { Addend = addend },
            (null, _) when sign > 0 => right with { Addend = addend },
            _ => new FormulaTerm($"{left.Text}{(sign > 0 ? " + " : " - ")}{right.Text}", addend, true),
        };
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

    /// <summary>The scalable float at each rank, where its row has ranks.</summary>
    public static IReadOnlyList<double>? ScalableRanks(IPropertyHolder scalable, ConstantsTable constants)
    {
        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue) ?? 1;
        var row = PropertyWalk.GetStruct(scalable, "Curve") is { } curve
            ? PropertyWalk.GetString(curve, "RowName")
            : null;

        return constants.Lookup(row).Record?.Ranks is { } ranks
            ? [.. ranks.Select(rank => Math.Round(value * rank, 10))]
            : null;
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
