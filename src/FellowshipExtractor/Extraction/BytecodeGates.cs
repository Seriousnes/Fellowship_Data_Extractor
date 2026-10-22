using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed class BytecodeGates
{
    private const int MaxDepth = 12;

    private readonly KismetExpression[] _bytecode;
    private readonly int[][] _successors;
    private readonly int[] _roots;

    private BytecodeGates(KismetExpression[] bytecode, int[][] successors, int[] roots)
    {
        _bytecode = bytecode;
        _successors = successors;
        _roots = roots;
    }

    public static BytecodeGates? Of(UStruct body, UObject cdo)
    {
        if (body.ScriptBytecode is not { Length: > 0 } bytecode) return null;
        if (Roots(body, cdo, bytecode) is not { } roots) return null;

        return new BytecodeGates(bytecode, Successors(bytecode), roots);
    }

    public IEnumerable<(KismetExpression Condition, int At)> Branches
    {
        get
        {
            for (var at = 0; at < _bytecode.Length; at++)
            {
                if (Guarding(_bytecode[at]) is { } condition) yield return (condition, at);
            }
        }
    }

    public IReadOnlySet<int> Reached => Reachable(-1);

    public IReadOnlySet<int> Only(int at)
    {
        var reached = Reachable(-1);

        reached.ExceptWith(Reachable(at));

        return reached;
    }

    private static KismetExpression? Guarding(KismetExpression statement) => statement switch
    {
        EX_JumpIfNot branch => branch.BooleanExpression,
        EX_PopExecutionFlowIfNot branch => branch.BooleanExpression,
        _ => null,
    };

    private HashSet<int> Reachable(int without)
    {
        var seen = new HashSet<int>();
        var pending = new Stack<int>();

        foreach (var root in _roots)
        {
            if (root >= 0 && seen.Add(root)) pending.Push(root);
        }

        while (pending.Count > 0)
        {
            var at = pending.Pop();
            var successors = _successors[at];
            var guarded = at == without && Guarding(_bytecode[at]) is not null ? successors.Length - 1 : -1;

            for (var step = 0; step < successors.Length; step++)
            {
                if (step == guarded) continue;

                var next = successors[step];
                if (next < 0 || next >= _bytecode.Length) continue;
                if (seen.Add(next)) pending.Push(next);
            }
        }

        return [.. seen.Select(at => _bytecode[at].StatementIndex)];
    }

    private static int[][] Successors(KismetExpression[] bytecode)
    {
        var index = new Dictionary<int, int>();

        for (var at = 0; at < bytecode.Length; at++) index[bytecode[at].StatementIndex] = at;

        int At(uint offset) => index.TryGetValue((int)offset, out var found) ? found : -1;

        int[] pushed =
        [
            .. bytecode
                .OfType<EX_PushExecutionFlow>()
                .Select(push => At(push.PushingAddress))
                .Where(target => target >= 0)
                .Distinct(),
        ];

        var successors = new int[bytecode.Length][];

        for (var at = 0; at < bytecode.Length; at++)
        {
            successors[at] = bytecode[at] switch
            {
                EX_Return or EX_EndOfScript or EX_ComputedJump => [],
                EX_PopExecutionFlow => pushed,
                EX_PopExecutionFlowIfNot => [.. pushed, at + 1],
                EX_JumpIfNot branch => [At(branch.CodeOffset), at + 1],
                EX_Jump jump => [At(jump.CodeOffset)],
                _ => [at + 1],
            };
        }

        return successors;
    }

    private static int[]? Roots(UStruct body, UObject cdo, KismetExpression[] bytecode)
    {
        var roots = new HashSet<int> { 0 };

        if (!bytecode.Any(statement => statement is EX_ComputedJump)) return [.. roots.Where(at => at >= 0)];

        UObject[] exports = [.. cdo.Owner?.GetExports() ?? []];
        var unbound = Unbound(exports);
        var entered = false;

        foreach (var resumed in bytecode.SelectMany(statement => Descendants(statement, 0)).OfType<EX_SkipOffsetConst>())
        {
            roots.Add(At(bytecode, (int)resumed.Value));
        }

        foreach (var export in exports)
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } calling }) continue;

            foreach (var statement in calling)
            {
                foreach (var entry in Entered(statement, body.Name, 0))
                {
                    entered = true;
                    if (!unbound.Contains(export.Name)) roots.Add(At(bytecode, entry));
                }
            }
        }

        return entered ? [.. roots.Where(at => at >= 0)] : null;
    }

    public static IReadOnlySet<string> Unbound(IEnumerable<UObject> exports)
    {
        var handlers = new List<(string Local, string Function)>();
        var bound = new HashSet<string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var export in exports)
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                switch (statement)
                {
                    case EX_BindDelegate { Delegate: EX_LocalVariable local } bind when Leaf(local.Variable) is { } name:
                        handlers.Add((name, bind.FunctionName.Text));
                        break;
                    case EX_BindDelegate bind:
                        bound.Add(bind.FunctionName.Text);
                        break;
                    case EX_RemoveMulticastDelegate:
                        break;
                    default:
                        foreach (var reached in Descendants(statement, 0))
                        {
                            if (reached is EX_LocalVariable variable && Leaf(variable.Variable) is { } read) used.Add(read);
                            if (reached is EX_InstanceDelegate instance) bound.Add(instance.FunctionName.Text);
                        }

                        break;
                }
            }
        }

        return handlers
            .GroupBy(handler => handler.Function, StringComparer.Ordinal)
            .Where(group => !bound.Contains(group.Key) && group.All(handler => !used.Contains(handler.Local)))
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static IEnumerable<KismetExpression> Descendants(KismetExpression expression, int depth)
    {
        yield return expression;

        if (depth > MaxDepth) yield break;

        foreach (var read in AppliedEffects.Reaching(expression.GetType()))
        {
            switch (read(expression))
            {
                case KismetExpression child:
                    foreach (var below in Descendants(child, depth + 1)) yield return below;
                    break;
                case KismetExpression[] children:
                    foreach (var child in children)
                    {
                        foreach (var below in Descendants(child, depth + 1)) yield return below;
                    }

                    break;
            }
        }
    }

    private static string? Leaf(FKismetPropertyPointer pointer) =>
        pointer.New?.Path is { Length: > 0 } path ? path[^1].Text : null;

    private static int At(KismetExpression[] bytecode, int offset)
    {
        for (var at = 0; at < bytecode.Length; at++)
        {
            if (bytecode[at].StatementIndex == offset) return at;
        }

        return -1;
    }

    private static IEnumerable<int> Entered(KismetExpression? expression, string function, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        var called = expression switch
        {
            EX_LocalFinalFunction call => (Name: call.StackNode.Name, call.Parameters),
            EX_FinalFunction call => (Name: call.StackNode.Name, call.Parameters),
            _ => default,
        };

        if (called.Name is not null &&
            called.Name.Equals(function, StringComparison.Ordinal) &&
            called.Parameters.FirstOrDefault() is EX_IntConst offset)
        {
            yield return offset.Value;
        }

        foreach (var child in ResourceGains.Children(expression))
        {
            foreach (var entry in Entered(child, function, depth + 1)) yield return entry;
        }
    }
}
