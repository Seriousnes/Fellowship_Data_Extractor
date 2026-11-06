using System.Collections.Immutable;

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

/// <summary>One branch on a path, with the side the path took.</summary>
public sealed record BranchTaken(KismetExpression Condition, bool Held);

/// <summary>
/// The paths through a compiled body from an entry offset, each statement with the branches taken to reach it. The
/// walk follows jumps, both sides of a branch and the execution flow stack a sequence node pushes.
/// </summary>
public sealed class BranchPaths
{
    private const int MaxStates = 40000;
    private const int MaxDepth = 16;

    private readonly KismetExpression[] _bytecode;
    private readonly Dictionary<int, int> _indexOf = [];
    private readonly Dictionary<string, List<KismetExpression>> _locals = new(StringComparer.Ordinal);

    public BranchPaths(UStruct body)
    {
        _bytecode = body.ScriptBytecode ?? [];

        for (var at = 0; at < _bytecode.Length; at++)
        {
            _indexOf[_bytecode[at].StatementIndex] = at;

            var (variable, value) = _bytecode[at] switch
            {
                EX_Let { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                EX_LetObj { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                EX_LetBool { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                _ => (null, null),
            };

            if (variable is null || DynamicTextFloats.Name(variable) is not { } local) continue;

            if (!_locals.TryGetValue(local, out var values)) _locals[local] = values = [];
            values.Add(value!);
        }
    }

    /// <summary>The value a local holds where one assignment gives it, through casts.</summary>
    public KismetExpression? Value(KismetExpression? expression, int depth = 0)
    {
        if (expression is null || depth > MaxDepth) return expression;
        if (expression is EX_Cast cast) return Value(cast.Target, depth + 1);

        return expression is EX_LocalVariable or EX_LocalOutVariable &&
               DynamicTextFloats.Name(expression) is { } local &&
               _locals.GetValueOrDefault(local) is [var assigned]
            ? Value(assigned, depth + 1)
            : expression;
    }

    public IEnumerable<(KismetExpression Statement, ImmutableList<BranchTaken> Taken)> From(int entry)
    {
        if (!_indexOf.TryGetValue(entry, out var root)) yield break;

        var visited = new HashSet<(int, string, string)>();
        var pending = new Stack<(int At, ImmutableStack<int> Flow, ImmutableList<BranchTaken> Taken)>();

        pending.Push((root, ImmutableStack<int>.Empty, []));

        while (pending.Count > 0 && visited.Count < MaxStates)
        {
            var (at, flow, taken) = pending.Pop();

            if (at < 0 || at >= _bytecode.Length) continue;

            var key = (at, string.Join(',', flow), string.Join(',', taken.Select(branch => $"{branch.Condition.StatementIndex}:{branch.Held}")));

            if (!visited.Add(key)) continue;

            var statement = _bytecode[at];

            yield return (statement, taken);

            switch (statement)
            {
                case EX_PushExecutionFlow push:
                    pending.Push((at + 1, flow.Push(Offset((int)push.PushingAddress)), taken));
                    break;
                case EX_PopExecutionFlow:
                    if (!flow.IsEmpty) pending.Push((flow.Peek(), flow.Pop(), taken));
                    break;
                case EX_PopExecutionFlowIfNot branch:
                    pending.Push((at + 1, flow, taken.Add(new BranchTaken(branch.BooleanExpression, true))));
                    if (!flow.IsEmpty) pending.Push((flow.Peek(), flow.Pop(), taken.Add(new BranchTaken(branch.BooleanExpression, false))));
                    break;
                case EX_JumpIfNot branch:
                    pending.Push((Offset((int)branch.CodeOffset), flow, taken.Add(new BranchTaken(branch.BooleanExpression, false))));
                    pending.Push((at + 1, flow, taken.Add(new BranchTaken(branch.BooleanExpression, true))));
                    break;
                case EX_Jump jump:
                    pending.Push((Offset((int)jump.CodeOffset), flow, taken));
                    break;
                case EX_Return or EX_EndOfScript or EX_ComputedJump:
                    break;
                default:
                    pending.Push((at + 1, flow, taken));
                    break;
            }
        }
    }

    private int Offset(int offset) => _indexOf.GetValueOrDefault(offset, -1);
}
