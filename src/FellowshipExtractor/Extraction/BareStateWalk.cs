using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed class BareStateWalk(
    UStruct function, UObject cdo, ConstantsTable constants, TypeMappings? mappings)
{
    private const string ScalableRead = "GetScalableFloat";
    private const string RowHandle = "CurveTableRowHandle";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";
    private const string TagStruct = "GameplayTag";
    private const string TagNameField = "TagName";
    private const string AssetTagsRead = "GetAllAssetTagsForThisEffect";
    private const string EffectRead = "GetGameplayEffectDef";
    private const string CaptureRead = "GetCapturedAttribute";
    private const string ElementRead = "Array_Get";
    private const string EntryRead = "Map_Find";
    private const string CaptureField = "AttributeToCapture";
    private const string AttributeNameField = "AttributeName";
    private const int Budget = 1024;
    private const int MaxDepth = 32;

    private readonly KismetExpression[] _bytecode = function.ScriptBytecode ?? [];
    private readonly Dictionary<string, KismetExpression> _locals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _returned = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gates = new(StringComparer.Ordinal);

    public static UStruct? Compiled(UObject cdo, string function)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is UStruct body && export.Name.Equals(function, StringComparison.Ordinal)) return body;
        }

        return null;
    }

    public Func<string, InvalidOperationException>? Unreadable { get; init; }

    private bool Inlined { get; init; }

    public IReadOnlySet<string> AssumedTags { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public EffectContext? Effect { get; init; }

    public bool ReadsCapture { get; private set; }

    public IReadOnlySet<string> Gates => _gates;

    public (double Low, double High)? Range { get; private set; }

    public IReadOnlyDictionary<string, double>? ByLevelType { get; private set; }

    public IEnumerable<KismetExpression> Statements()
    {
        var at = 0;

        for (var step = 0; step < Budget && at < _bytecode.Length; step++)
        {
            var statement = _bytecode[at];

            switch (statement)
            {
                case EX_Return or EX_EndOfScript:
                    yield break;

                case EX_JumpIfNot branch:
                    if (Boolean(branch.BooleanExpression) is not { } taken) yield break;

                    at = taken ? at + 1 : At(branch.CodeOffset);
                    if (at < 0) yield break;
                    continue;

                case EX_Jump jump:
                    at = At(jump.CodeOffset);
                    if (at < 0) yield break;
                    continue;

                default:
                    yield return statement;

                    Bind(statement);
                    Inline(statement);
                    at++;
                    continue;
            }
        }
    }

    private void Inline(KismetExpression statement)
    {
        if (Inlined) return;
        if (statement is not EX_LocalVirtualFunction call) return;
        if (Compiled(cdo, call.VirtualFunctionName.Text) is not { ScriptBytecode.Length: > 0 } body) return;

        var inside = new BareStateWalk(body, cdo, constants, mappings)
        {
            Inlined = true,
            AssumedTags = AssumedTags,
            Effect = Effect,
        };

        foreach (var _ in inside.Statements()) { }

        foreach (var gate in inside._gates) _gates.Add(gate);

        var at = 0;

        foreach (var child in body.ChildProperties ?? [])
        {
            if (child is not FProperty parameter) continue;
            if (!parameter.PropertyFlags.HasFlag(EPropertyFlags.Parm)) continue;
            if (parameter.PropertyFlags.HasFlag(EPropertyFlags.ReturnParm)) continue;

            if (parameter.PropertyFlags.HasFlag(EPropertyFlags.OutParm) &&
                DynamicTextFloats.Name(call.Parameters.ElementAtOrDefault(at)) is { } local &&
                inside._locals.TryGetValue(parameter.Name.Text, out var bound) &&
                inside.Number(bound) is { } number)
            {
                _returned[local] = number;
            }

            at++;
        }
    }

    public double? Answer()
    {
        var at = 0;

        for (var step = 0; step < Budget && at < _bytecode.Length; step++)
        {
            switch (_bytecode[at])
            {
                case EX_Return returned:
                    if (Number(returned.ReturnExpression) is { } answer) return answer;
                    if (ByLevelType is not null || Range is not null) return null;

                    return Refuse($"returns {Describe(returned.ReturnExpression)} rather than a number");

                case EX_JumpIfNot branch:
                    if (Boolean(branch.BooleanExpression) is not { } taken)
                    {
                        return Refuse(
                            $"branches on {Describe(branch.BooleanExpression)}, and that is neither " +
                            "true nor false for a character holding no talents");
                    }

                    at = taken ? at + 1 : At(branch.CodeOffset);
                    if (at < 0) return null;
                    continue;

                case EX_Jump jump:
                    at = At(jump.CodeOffset);
                    if (at < 0) return null;
                    continue;

                case EX_EndOfScript:
                    return Refuse("ends without returning a value");

                default:
                    if (!Bind(_bytecode[at]))
                    {
                        return Refuse(
                            $"runs a {_bytecode[at].Token} statement this reader does not evaluate");
                    }

                    at++;
                    continue;
            }
        }

        return Refuse($"does not return within {Budget} statements");
    }

    public bool Bind(KismetExpression statement)
    {
        if (Written(statement) is { } written)
        {
            _locals[written.Name] = written.Call;
            if (statement is EX_Context) return true;
        }

        var assignment = statement switch
        {
            EX_Let let => (Variable: let.Variable, Value: let.Assignment),
            EX_LetObj let => (Variable: let.Variable, Value: let.Assignment),
            EX_LetBool let => (Variable: let.Variable, Value: let.Assignment),
            _ => default,
        };

        if (assignment.Variable is null || assignment.Value is null) return false;
        if (DynamicTextFloats.Name(assignment.Variable) is not { } name) return false;

        _locals[name] = assignment.Value;
        return true;
    }

    private static (string Name, EX_FinalFunction Call)? Written(KismetExpression statement)
    {
        var context = statement switch
        {
            EX_Context direct => direct,
            EX_Let { Assignment: EX_Context assigned } => assigned,
            EX_LetBool { Assignment: EX_Context assigned } => assigned,
            _ => null,
        };

        if (context?.ContextExpression is not EX_FinalFunction call) return null;
        if (call.StackNode.Name is not (ElementRead or EntryRead)) return null;
        if (DynamicTextFloats.Name(call.Parameters.ElementAtOrDefault(2)) is not { } name) return null;

        return (name, call);
    }

    public void Holds(string name, KismetExpression value) => _locals[name] = value;

    private int At(uint offset)
    {
        for (var index = 0; index < _bytecode.Length; index++)
        {
            if (_bytecode[index].StatementIndex == (int)offset) return index;
        }

        Refuse($"jumps to code offset {offset}, and no statement of its own starts there " +
               $"({string.Join(", ", _bytecode.Select(statement => statement.StatementIndex))})");

        return -1;
    }

    private KismetExpression? Bound(KismetExpression? expression) =>
        DynamicTextFloats.Name(expression) is { } name && _locals.TryGetValue(name, out var assigned)
            ? assigned
            : null;

    public bool? Boolean(KismetExpression? expression, int depth = 0)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_True: return true;
            case EX_False: return false;
            case EX_IntConst value: return value.Value != 0;
            case EX_ByteConst value: return value.Value != 0;
            case EX_DynamicCast: return true;
            case EX_Cast cast: return Boolean(cast.Target, depth + 1);
            case EX_Context context: return Boolean(context.ContextExpression, depth + 1);

            case EX_CallMath call: return CallBoolean(call, depth);

            case EX_LocalVirtualFunction call: return TagBoolean(call.VirtualFunctionName.Text, call.Parameters);
            case EX_VirtualFunction call: return TagBoolean(call.VirtualFunctionName.Text, call.Parameters);
            case EX_LocalFinalFunction call: return TagBoolean(call.StackNode.Name, call.Parameters);
            case EX_FinalFunction { StackNode.Name: EntryRead } call: return Entry(call) is not null;
            case EX_FinalFunction call: return TagBoolean(call.StackNode.Name, call.Parameters);
        }

        return Bound(expression) is { } assigned ? Boolean(assigned, depth + 1) : null;
    }

    private static readonly string[] TagTests =
    [
        "HasMatchingGameplayTag",
        "HasAnyMatchingGameplayTags",
        "HasAllMatchingGameplayTags",
        "HasTag",
        "HasAnyTags",
        "HasAllTags",
    ];

    private bool? TagBoolean(string invoked, IReadOnlyList<KismetExpression> parameters)
    {
        if (!TagTests.Contains(invoked, StringComparer.Ordinal)) return null;

        if (Effect is { } effect && Calls(parameters.FirstOrDefault(), AssetTagsRead))
        {
            return parameters.Skip(1).Select(Tag).OfType<string>().Any(effect.AssetTags.Contains);
        }

        var matched = false;

        foreach (var parameter in parameters)
        {
            if (Tag(parameter) is not { } tag) continue;

            _gates.Add(tag);
            if (AssumedTags.Contains(tag)) matched = true;
        }

        return matched;
    }

    private string? Tag(KismetExpression? expression)
    {
        if (expression is EX_StructConst { Struct.Name: TagStruct } inline)
        {
            return Name(inline.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text);
        }

        if (DynamicTextFloats.Name(expression) is not { } property) return null;
        if (PropertyWalk.GetStruct(cdo, property) is not { } holder) return null;

        return Name(PropertyWalk.GetString(holder, TagNameField));
    }

    private bool? CallBoolean(EX_CallMath call, int depth)
    {
        var invoked = call.StackNode.Name;

        if (invoked.Equals("IsValid", StringComparison.Ordinal)) return true;
        if (TagBoolean(invoked, call.Parameters) is { } known) return known;

        if (invoked.Equals("Not_PreBool", StringComparison.Ordinal))
        {
            return Boolean(call.Parameters.FirstOrDefault(), depth + 1) is { } inner ? !inner : null;
        }

        if (invoked.Equals("BooleanAND", StringComparison.Ordinal))
        {
            var a = Boolean(call.Parameters.ElementAtOrDefault(0), depth + 1);
            var b = Boolean(call.Parameters.ElementAtOrDefault(1), depth + 1);
            if (a is false || b is false) return false;
            return a is true && b is true ? true : null;
        }

        if (invoked.Equals("BooleanOR", StringComparison.Ordinal))
        {
            var a = Boolean(call.Parameters.ElementAtOrDefault(0), depth + 1);
            var b = Boolean(call.Parameters.ElementAtOrDefault(1), depth + 1);
            if (a is true || b is true) return true;
            return a is false && b is false ? false : null;
        }

        var comparison = invoked.Split('_')[0];
        if (comparison is "Greater" or "GreaterEqual" or "Less" or "LessEqual" or "EqualEqual" or "NotEqual")
        {
            var a = Number(call.Parameters.ElementAtOrDefault(0), depth + 1);
            var b = Number(call.Parameters.ElementAtOrDefault(1), depth + 1);
            if (a is not { } left || b is not { } right) return null;

            return comparison switch
            {
                "Greater" => left > right,
                "GreaterEqual" => left >= right,
                "Less" => left < right,
                "LessEqual" => left <= right,
                "EqualEqual" => left == right,
                _ => left != right,
            };
        }

        return null;
    }

    public double? Number(KismetExpression? expression, int depth = 0)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_IntConst value: return value.Value;
            case EX_Int64Const value: return value.Value;
            case EX_ByteConst value: return value.Value;
            case EX_IntConstByte value: return value.Value;
            case EX_FloatConst value: return value.Value;
            case EX_DoubleConst value: return value.Value;
            case EX_IntOne: return 1;
            case EX_IntZero: return 0;

            case EX_Cast cast: return Number(cast.Target, depth + 1);
            case EX_CallMath call: return Called(call, depth);
            case EX_SwitchValue chosen: return Switched(chosen, depth);
        }

        if (DynamicTextFloats.Name(expression) is { } local && _returned.TryGetValue(local, out var number))
        {
            return number;
        }

        return Bound(expression) is { } assigned ? Number(assigned, depth + 1) : null;
    }

    public double? Reading(KismetExpression? expression) => Scalable(expression, 0);

    public string Text(KismetExpression? expression, int depth = 0)
    {
        if (expression is null) return "nothing";
        if (depth > MaxDepth) return "too deep to write out";

        switch (expression)
        {
            case EX_IntConst value: return value.Value.ToString();
            case EX_Int64Const value: return value.Value.ToString();
            case EX_ByteConst value: return value.Value.ToString();
            case EX_IntConstByte value: return value.Value.ToString();
            case EX_FloatConst value: return value.Value.ToString();
            case EX_DoubleConst value: return value.Value.ToString();
            case EX_IntOne: return "1";
            case EX_IntZero: return "0";

            case EX_Cast cast: return Text(cast.Target, depth + 1);
            case EX_CallMath call: return CallText(call, depth);
            case EX_SwitchValue chosen: return $"a switch on {Describe(chosen.IndexTerm)}";
        }

        if (DynamicTextFloats.Name(expression) is { } local && _returned.TryGetValue(local, out var number))
        {
            return $"{local} {number}";
        }

        if (Bound(expression) is { } assigned) return Text(assigned, depth + 1);

        return DynamicTextFloats.Name(expression) is { } unbound
            ? $"{unbound} (nothing binds it)"
            : expression.Token.ToString();
    }

    private string CallText(EX_CallMath call, int depth)
    {
        var invoked = call.StackNode.Name;

        if (invoked.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            var argument = call.Parameters.FirstOrDefault();
            var name = DynamicTextFloats.Name(argument) ?? Describe(argument);

            if (Struct(argument, depth + 1) is not { } read) return $"{name} (no scalable float)";

            return read.Row is { } row
                ? $"{name} -> {row} {constants.Lookup(row).Record?.Value} x {read.Multiplier}"
                : $"{name} -> literal {read.Multiplier}";
        }

        if (invoked.StartsWith("Conv_", StringComparison.Ordinal) ||
            invoked.StartsWith("MakeLiteral", StringComparison.Ordinal))
        {
            return Text(call.Parameters.FirstOrDefault(), depth + 1);
        }

        return $"{invoked}({string.Join(", ", call.Parameters.Select(p => Text(p, depth + 1)))})";
    }

    private double? Called(EX_CallMath call, int depth)
    {
        var invoked = call.StackNode.Name;

        double? Argument(int index) => Number(call.Parameters.ElementAtOrDefault(index), depth + 1);

        if (invoked.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            return Scalable(call.Parameters.FirstOrDefault(), depth + 1);
        }

        if (invoked.Equals(CaptureRead, StringComparison.Ordinal))
        {
            return Capture(call.Parameters.ElementAtOrDefault(1), depth + 1);
        }

        if (invoked.StartsWith("Multiply_", StringComparison.Ordinal))
        {
            var a = Argument(0);
            var b = Argument(1);
            if (a == 0 || b == 0) return 0;
            return a is { } left && b is { } right ? left * right : null;
        }

        if (invoked.StartsWith("Add_", StringComparison.Ordinal))
        {
            return Argument(0) is { } left && Argument(1) is { } right ? left + right : null;
        }

        if (invoked.StartsWith("Subtract_", StringComparison.Ordinal))
        {
            return Argument(0) is { } left && Argument(1) is { } right ? left - right : null;
        }

        if (invoked.StartsWith("Divide_", StringComparison.Ordinal))
        {
            var divisor = Argument(1);
            if (divisor is 0) return null;
            if (Argument(0) is not { } dividend) return null;

            return dividend == 0 ? 0 : divisor is { } by ? dividend / by : null;
        }

        if (invoked is "RandomFloatInRange" or "RandomIntegerInRange")
        {
            if (Argument(0) is not { } least || Argument(1) is not { } most) return null;

            Range = (least, most);
            return null;
        }

        if (invoked is "SelectFloat" or "SelectInt" or "SelectDouble")
        {
            return Boolean(call.Parameters.ElementAtOrDefault(2), depth + 1) switch
            {
                true => Argument(0),
                false => Argument(1),
                null => Refuse(
                    $"picks between two values on {Describe(call.Parameters.ElementAtOrDefault(2))}, " +
                    "which is neither true nor false for a character holding no talents"),
            };
        }

        if (invoked is "FClamp" or "Clamp")
        {
            return Argument(0) is { } value && Argument(1) is { } least && Argument(2) is { } most
                ? Math.Clamp(value, least, most)
                : null;
        }

        if (invoked is "FMax" or "Max")
        {
            return Argument(0) is { } left && Argument(1) is { } right ? Math.Max(left, right) : null;
        }

        if (invoked is "FMin" or "Min")
        {
            return Argument(0) is { } left && Argument(1) is { } right ? Math.Min(left, right) : null;
        }

        if (invoked.Equals("Round", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Floor(value + 0.5) : null;
        }

        if (invoked.Equals("FTrunc", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Truncate(value) : null;
        }

        if (invoked.Equals("FFloor", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Floor(value) : null;
        }

        if (invoked.Equals("FCeil", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Ceiling(value) : null;
        }

        if (invoked.Equals("Loge", StringComparison.Ordinal))
        {
            return Argument(0) is { } value and > 0 ? Math.Log(value) : null;
        }

        if (invoked.Equals("Log", StringComparison.Ordinal))
        {
            return Argument(0) is { } value and > 0 && Argument(1) is { } logBase and > 0 and not 1
                ? Math.Log(value) / Math.Log(logBase)
                : null;
        }

        if (invoked.StartsWith("Conv_", StringComparison.Ordinal) ||
            invoked.StartsWith("MakeLiteral", StringComparison.Ordinal))
        {
            return Argument(0);
        }

        return null;
    }

    private double? Switched(EX_SwitchValue chosen, int depth)
    {
        var answers = new SortedSet<double>();
        var cases = new List<(KismetExpression? Index, double Value)>();

        foreach (var branch in chosen.Cases)
        {
            if (Case(branch.CaseTerm, depth + 1) is not { } value)
            {
                return Refuse(
                    $"chooses its value on {Describe(chosen.IndexTerm)}, and the branch for " +
                    $"{Describe(branch.CaseIndexValueTerm)} reads no number");
            }

            cases.Add((branch.CaseIndexValueTerm, value));
            answers.Add(value);
        }

        if (Case(chosen.DefaultTerm, depth + 1) is { } fallback) answers.Add(fallback);
        if (answers.Count == 1) return answers.Min;

        var byOrdinal = new Dictionary<long, double>();

        foreach (var (index, value) in cases)
        {
            if (Number(index) is not { } ordinal)
            {
                return Refuse(
                    $"chooses its value on {Describe(chosen.IndexTerm)} against {Describe(index)}, " +
                    "which is no ordinal");
            }

            byOrdinal[(long)ordinal] = value;
        }

        ByLevelType = Keyed(chosen, byOrdinal);
        return null;
    }

    private double? Case(KismetExpression? expression, int depth) =>
        expression is EX_SwitchValue chosen
            ? Switched(chosen, depth)
            : Scalable(expression, depth) ?? Number(expression, depth);

    private IReadOnlyDictionary<string, double>? Keyed(
        EX_SwitchValue chosen, IReadOnlyDictionary<long, double> byOrdinal)
    {
        if (mappings is null || Enumerated(chosen.IndexTerm) is not { } enumerated)
        {
            Refuse($"answers a different value per {Describe(chosen.IndexTerm)}, and that names no enum, so " +
                   "the values cannot be keyed by anything the export states");

            return null;
        }

        var members = EnumOrdinals.Members(mappings, enumerated);
        var named = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (ordinal, value) in byOrdinal)
        {
            if (!members.TryGetValue(ordinal, out var member))
            {
                Refuse($"answers a value for {enumerated} {ordinal}, and that enum has no member for it");

                return null;
            }

            named[member] = value;
        }

        return named;
    }

    private string? Enumerated(KismetExpression? index)
    {
        if (DynamicTextFloats.Name(index) is not { } name) return null;

        foreach (var child in function.ChildProperties ?? [])
        {
            if (!child.Name.Text.Equals(name, StringComparison.Ordinal)) continue;

            return child switch
            {
                FEnumProperty { Enum.Name: { Length: > 0 } named } => named,
                FByteProperty { Enum.Name: { Length: > 0 } named } => named,
                _ => null,
            };
        }

        return null;
    }

    private double? Scalable(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;
        if (expression is EX_SwitchValue chosen) return Switched(chosen, depth);
        if (Struct(expression, depth) is not { } scalable) return null;

        var (multiplier, row) = scalable;
        if (row is null) return multiplier;

        if (constants.Lookup(row).Record?.Value is not { } value)
        {
            return Refuse(
                $"reads its value off row {row}, and no ability constants table gives a number for it");
        }

        return multiplier * value;
    }

    private bool Calls(KismetExpression? expression, string function, int depth = 0)
    {
        if (expression is null || depth > MaxDepth) return false;
        if (expression is EX_CallMath call) return call.StackNode.Name.Equals(function, StringComparison.Ordinal);

        return Bound(expression) is { } assigned && Calls(assigned, function, depth + 1);
    }

    private double? Capture(KismetExpression? element, int depth)
    {
        if (Effect?.Captured is not { } captured) return null;

        var (array, at) = Resolved(element, depth) switch
        {
            EX_FinalFunction { StackNode.Name: ElementRead } read => (read.Parameters.ElementAtOrDefault(0), read.Parameters.ElementAtOrDefault(1)),
            EX_ArrayGetByRef read => (read.ArrayVariable, read.ArrayIndex),
            _ => (null, null),
        };

        if (DynamicTextFloats.Name(array) is not { } field) return null;
        if (Number(at, depth + 1) is not { } index) return null;
        if (PropertyWalk.Get(cdo, field)?.GenericValue is not UScriptArray captures) return null;
        if (captures.Properties.ElementAtOrDefault((int)index)?.GenericValue is not FScriptStruct { StructType: IPropertyHolder capture }) return null;
        if (PropertyWalk.GetStruct(capture, CaptureField) is not { } attribute) return null;
        if (PropertyWalk.GetString(attribute, AttributeNameField) is not { } name) return null;

        ReadsCapture = true;
        return captured(name);
    }

    private IPropertyHolder? Entry(EX_FinalFunction find)
    {
        if (Effect is not { } effect) return null;
        if (!Calls(Resolved(find.Parameters.ElementAtOrDefault(1), 0) is EX_MetaCast cast ? cast.Target : null, EffectRead)) return null;
        if (DynamicTextFloats.Name(find.Parameters.ElementAtOrDefault(0)) is not { } field) return null;
        if (PropertyWalk.Get(cdo, field)?.GenericValue is not UScriptMap map) return null;

        foreach (var (key, value) in map.Properties)
        {
            if (!effect.Class.Equals(PropertyWalk.AsString(key?.GenericValue), StringComparison.OrdinalIgnoreCase)) continue;

            return value?.GenericValue switch
            {
                FScriptStruct { StructType: IPropertyHolder held } => held,
                IPropertyHolder held => held,
                _ => null,
            };
        }

        return null;
    }

    private KismetExpression? Resolved(KismetExpression? expression, int depth)
    {
        while (expression is not null && depth++ <= MaxDepth && Bound(expression) is { } assigned) expression = assigned;

        return expression;
    }

    private (double Multiplier, string? Row)? Struct(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;
        if (expression is EX_StructConst constant) return Inline(constant);

        if (expression is EX_FinalFunction { StackNode.Name: EntryRead } find)
        {
            return Entry(find) is { } entry
                ? (PropertyWalk.AsNumber(PropertyWalk.Get(entry, ValueField)?.GenericValue) ?? 1,
                    Name(PropertyWalk.GetStruct(entry, CurveField) is { } held ? PropertyWalk.GetString(held, RowField) : null))
                : null;
        }

        if (Bound(expression) is { } assigned) return Struct(assigned, depth + 1);
        if (DynamicTextFloats.Name(expression) is not { } property) return null;
        if (PropertyWalk.GetStruct(cdo, property) is not { } scalable) return null;

        var row = PropertyWalk.GetStruct(scalable, CurveField) is { } curve
            ? PropertyWalk.GetString(curve, RowField)
            : null;

        return (PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1, Name(row));
    }

    private static (double Multiplier, string? Row)? Inline(EX_StructConst constant)
    {
        double? multiplier = null;
        string? row = null;

        foreach (var member in constant.Properties)
        {
            switch (member)
            {
                case EX_FloatConst value when multiplier is null:
                    multiplier = value.Value;
                    break;

                case EX_DoubleConst value when multiplier is null:
                    multiplier = value.Value;
                    break;

                case EX_StructConst handle when handle.Struct.Name.Equals(RowHandle, StringComparison.Ordinal):
                    row = handle.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text;
                    break;
            }
        }

        return (multiplier ?? 1, Name(row));
    }

    private static string? Name(string? row) =>
        row is null || row.Length == 0 || row.Equals(NoRow, StringComparison.Ordinal) ? null : row;

    private string Describe(KismetExpression? expression, int depth = 0)
    {
        if (expression is null) return "nothing";
        if (depth <= MaxDepth && Bound(expression) is { } assigned) return Describe(assigned, depth + 1);

        return expression switch
        {
            EX_Context context => Describe(context.ContextExpression, depth + 1),
            EX_CallMath call => call.StackNode.Name,
            EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
            EX_VirtualFunction call => call.VirtualFunctionName.Text,
            EX_FinalFunction call => call.StackNode.Name,
            EX_ByteConst value => value.Value.ToString(),
            EX_IntConst value => value.Value.ToString(),
            _ => DynamicTextFloats.Name(expression) ?? expression.Token.ToString(),
        };
    }

    private double? Refuse(string what) => Unreadable is null ? null : throw Unreadable(what);
}
