using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

public sealed record DynamicTextFloat(string Property, bool ScalesWithHaste);

public sealed class DynamicTextFloatMap
{
    public static readonly DynamicTextFloatMap Empty = new(new Dictionary<int, string>(), null, false);

    private readonly IReadOnlyDictionary<int, string> _cases;
    private readonly string? _sole;
    private readonly bool _haste;

    internal DynamicTextFloatMap(IReadOnlyDictionary<int, string> cases, string? sole, bool haste)
    {
        _cases = cases;
        _sole = sole;
        _haste = haste;
    }

    public DynamicTextFloat? For(int identifier)
    {
        if (_cases.TryGetValue(identifier, out var property)) return new DynamicTextFloat(property, _haste);

        return _cases.Count == 0 && _sole is not null ? new DynamicTextFloat(_sole, _haste) : null;
    }
}

public sealed record DynamicTextRange(
    double Coefficient, string? Attribute, bool ScalesWithExpertise, double? Spread);

public static class DynamicTextRanges
{
    private const string Function = "ResolveDynamicTextFloatRange";
    private const string Expertise = "Expertise";
    private const string Minimum = "MinValue";
    private const string Maximum = "MaxValue";

    public static DynamicTextRange? For(UObject cdo, int identifier, Func<string, double?> scalable)
    {
        if (DynamicTextFloats.Body(cdo, Function) is not { Length: > 0 } bytecode) return null;

        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
        KismetExpression? low = null, high = null;

        foreach (var statement in bytecode)
        {
            if (Assignment(statement) is not var (name, value) || name is null) continue;

            if (name == Minimum) low = value;
            else if (name == Maximum) high = value;
            else locals[name] = value;
        }

        if (low is null || high is null) return null;

        var reader = new Terms(locals, identifier, scalable);

        if (reader.Of(low) is not { } least || reader.Of(high) is not { } most) return null;
        if (least.Attribute != most.Attribute || least.Expertise != most.Expertise) return null;

        var middle = (least.Factor + most.Factor) / 2;
        if (middle == 0) return null;

        var spread = (most.Factor - least.Factor) / middle;

        return new DynamicTextRange(
            Math.Round(middle, 10),
            least.Attribute,
            least.Expertise,
            spread > 0 ? Math.Round(spread, 10) : null);
    }

    private static (string? Name, KismetExpression Value)? Assignment(KismetExpression statement)
    {
        if (statement is not EX_Let let) return null;

        return (DynamicTextFloats.Name(let.Variable), let.Assignment);
    }

    private readonly record struct Term(double Factor, string? Attribute, bool Expertise);

    private sealed class Terms(
        IReadOnlyDictionary<string, KismetExpression> locals,
        int identifier,
        Func<string, double?> scalable)
    {
        public Term? Of(KismetExpression? expression, int depth = 0)
        {
            if (expression is null || depth > 24) return null;

            switch (expression)
            {
                case EX_DoubleConst value: return new Term(value.Value, null, false);
                case EX_FloatConst value: return new Term(value.Value, null, false);
                case EX_IntConst value: return new Term(value.Value, null, false);

                case EX_Cast cast: return Of(cast.Target, depth + 1);

                case EX_LocalVariable or EX_LocalOutVariable or EX_InstanceVariable
                    when DynamicTextFloats.Name(expression) is { } name && locals.TryGetValue(name, out var bound):
                    return Of(bound, depth + 1);

                case EX_SwitchValue chosen: return Of(Case(chosen), depth + 1);

                case EX_CallMath call: return Called(call, depth);
            }

            return null;
        }

        private KismetExpression? Case(EX_SwitchValue chosen)
        {
            foreach (var branch in chosen.Cases)
            {
                if (branch.CaseIndexValueTerm is EX_IntConst index && index.Value == identifier)
                {
                    return branch.CaseTerm;
                }
            }

            return null;
        }

        private Term? Called(EX_CallMath call, int depth)
        {
            var function = call.StackNode.Name;

            if (function.StartsWith("GetScalableFloat", StringComparison.Ordinal))
            {
                return DynamicTextFloats.Name(call.Parameters.FirstOrDefault()) is { } property &&
                       scalable(property) is { } value
                    ? new Term(value, null, false)
                    : null;
            }

            if (function.Equals("GetFloatAttribute", StringComparison.Ordinal))
            {
                return Attribute(call) is not { } attribute
                    ? null
                    : attribute.Equals(Expertise, StringComparison.Ordinal)
                        ? new Term(1, null, true)
                        : new Term(1, attribute, false);
            }

            if (function.StartsWith("Multiply_", StringComparison.Ordinal))
            {
                return Combine(Of(call.Parameters.ElementAtOrDefault(0), depth + 1),
                               Of(call.Parameters.ElementAtOrDefault(1), depth + 1));
            }

            if (function.StartsWith("Add_", StringComparison.Ordinal))
            {
                return Raised(Of(call.Parameters.ElementAtOrDefault(0), depth + 1),
                              Of(call.Parameters.ElementAtOrDefault(1), depth + 1));
            }

            if (function.Equals("SelectFloat", StringComparison.Ordinal))
            {
                return Of(call.Parameters.ElementAtOrDefault(1), depth + 1);
            }

            return null;
        }

        private static string? Attribute(EX_CallMath call)
        {
            foreach (var parameter in call.Parameters)
            {
                if (parameter is not EX_StructConst holder) continue;

                foreach (var member in holder.Properties)
                {
                    if (member is EX_StringConst named) return named.Value;
                }
            }

            return null;
        }

        private static Term? Combine(Term? first, Term? second)
        {
            if (first is not { } a || second is not { } b) return null;
            if (a.Attribute is not null && b.Attribute is not null) return null;

            return new Term(a.Factor * b.Factor, a.Attribute ?? b.Attribute, a.Expertise || b.Expertise);
        }

        private static Term? Raised(Term? first, Term? second)
        {
            if (first is not { } a || second is not { } b) return null;

            if (a.Expertise && b is { Factor: 1, Attribute: null, Expertise: false }) return a;
            if (b.Expertise && a is { Factor: 1, Attribute: null, Expertise: false }) return b;

            return null;
        }
    }
}

public static class DynamicTextFloats
{
    private const string Function = "ResolveDynamicTextFloat";
    private const string ScalableRead = "GetScalableFloat";
    private const string HasteScaler = "GetDefaultTimingScalerBasedOnHaste";

    public static DynamicTextFloatMap Of(UObject cdo)
    {
        if (Body(cdo) is not { Length: > 0 } bytecode) return DynamicTextFloatMap.Empty;

        var reads = new Dictionary<string, string>(StringComparer.Ordinal);
        var haste = false;

        foreach (var statement in bytecode)
        {
            if (Named(statement) == HasteScaler) haste = true;

            if (statement is not EX_Let let) continue;
            if (Name(let.Variable) is not { } local) continue;
            if (let.Assignment is not EX_CallMath call) continue;
            if (!call.StackNode.Name.StartsWith(ScalableRead, StringComparison.Ordinal)) continue;

            if (Name(call.Parameters.FirstOrDefault()) is { } property) reads[local] = property;
        }

        if (reads.Count == 0) return DynamicTextFloatMap.Empty;

        var cases = new Dictionary<int, string>();

        foreach (var switched in bytecode.OfType<EX_Let>().Select(l => l.Assignment).OfType<EX_SwitchValue>())
        {
            foreach (var branch in switched.Cases)
            {
                if (branch.CaseIndexValueTerm is not EX_IntConst index) continue;
                if (Name(branch.CaseTerm) is not { } local) continue;
                if (reads.TryGetValue(local, out var property)) cases[index.Value] = property;
            }
        }

        return new DynamicTextFloatMap(cases, reads.Count == 1 ? reads.Values.First() : null, haste);
    }

    private static KismetExpression[]? Body(UObject cdo) => Body(cdo, Function);

    internal static KismetExpression[]? Body(UObject cdo, string function)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is UStruct body && export.Name.Equals(function, StringComparison.Ordinal))
            {
                return body.ScriptBytecode;
            }
        }

        return null;
    }

    private static string? Named(KismetExpression expression) => expression switch
    {
        EX_Context context => Named(context.ContextExpression),
        EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
        EX_LocalFinalFunction call => call.StackNode.Name,
        EX_CallMath call => call.StackNode.Name,
        _ => null,
    };

    internal static string? Name(KismetExpression? expression) => expression switch
    {
        EX_StructMemberContext member => Leaf(member.Property),
        EX_InstanceVariable variable => Leaf(variable.Variable),
        EX_LocalVariable variable => Leaf(variable.Variable),
        EX_LocalOutVariable variable => Leaf(variable.Variable),
        EX_Cast cast => Name(cast.Target),
        _ => null,
    };

    private static string? Leaf(FKismetPropertyPointer pointer) =>
        pointer.New?.Path is { Length: > 0 } path ? path[^1].Text : null;
}
