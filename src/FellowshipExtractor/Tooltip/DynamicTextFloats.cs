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

    private static KismetExpression[]? Body(UObject cdo)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is UStruct body && export.Name.Equals(Function, StringComparison.Ordinal))
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

    private static string? Name(KismetExpression? expression) => expression switch
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
