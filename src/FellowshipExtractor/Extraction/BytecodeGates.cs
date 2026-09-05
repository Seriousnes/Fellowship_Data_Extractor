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

        var entered = false;

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } calling }) continue;

            foreach (var statement in calling)
            {
                foreach (var entry in Entered(statement, body.Name, 0))
                {
                    entered = true;
                    roots.Add(At(bytecode, entry));
                }
            }
        }

        return entered ? [.. roots.Where(at => at >= 0)] : null;
    }

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
