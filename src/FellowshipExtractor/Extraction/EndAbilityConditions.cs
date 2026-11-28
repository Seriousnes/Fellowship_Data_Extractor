using System.Collections.Immutable;

using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

/// <summary>A branch statement of a body, by its index in the bytecode, and the side taken.</summary>
public readonly record struct BranchSide(int At, bool Held);

/// <summary>
/// The branches between an entry offset of a compiled body and each statement reached from it. A statement has each
/// branch whose last test before it has the same side on every path from the entry. A branch with a path through each
/// side meeting again before the statement is left out. The walk follows jumps, both sides of a branch and the
/// execution flow stack a sequence node pushes, and joins the paths meeting at a statement.
/// </summary>
/// <param name="body">The compiled body.</param>
/// <param name="closed">
/// Whether a side of a branch is closed to the walk, and null for a branch the walk takes on both sides. A branch with
/// a closed side belongs to no statement's list.
/// </param>
public sealed class EndAbilityConditions(UStruct body, Func<KismetExpression, bool, bool?> closed)
{
    private const int MaxUpdates = 200000;

    private readonly KismetExpression[] _bytecode = body.ScriptBytecode ?? [];
    private readonly Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<BranchSide>>> _from = [];
    private Dictionary<int, int>? _indexOf;

    /// <summary>The statements of the body.</summary>
    public KismetExpression[] Statements => _bytecode;

    /// <summary>True once a walk stops at its update cap. That entry has no branches.</summary>
    public bool Truncated { get; private set; }

    /// <summary>The branches between an entry offset and the statement at an offset, in the order a path takes them.</summary>
    public IReadOnlyList<BranchSide> Between(int entry, int statement)
    {
        if (!_from.TryGetValue(entry, out var reached)) reached = _from[entry] = Walk(entry);

        return reached.GetValueOrDefault(statement, []);
    }

    private Dictionary<int, IReadOnlyList<BranchSide>> Walk(int entry)
    {
        if (_indexOf is null)
        {
            _indexOf = [];

            for (var at = 0; at < _bytecode.Length; at++) _indexOf[_bytecode[at].StatementIndex] = at;
        }

        var states = new Dictionary<(int At, string Flow), ImmutableList<BranchSide>>();
        var pending = new Queue<(int At, ImmutableStack<int> Flow)>();
        var updates = 0;

        void Reach(int at, ImmutableStack<int> flow, ImmutableList<BranchSide> taken)
        {
            if (at < 0 || at >= _bytecode.Length) return;

            var key = (at, string.Join(',', flow));

            if (!states.TryGetValue(key, out var known))
            {
                states[key] = taken;
                pending.Enqueue((at, flow));

                return;
            }

            var shared = known.RemoveAll(side => !taken.Contains(side));

            if (shared.Count == known.Count) return;

            states[key] = shared;
            pending.Enqueue((at, flow));
        }

        void Branch(int at, ImmutableStack<int> flow, ImmutableList<BranchSide> taken, int branch, bool held)
        {
            if (closed(_bytecode[branch], held) is { } shut)
            {
                if (!shut) Reach(at, flow, taken);

                return;
            }

            // The side of this test replaces the side of the branch's test in an earlier turn of a loop.
            Reach(at, flow, taken.RemoveAll(side => side.At == branch).Add(new BranchSide(branch, held)));
        }

        if (!_indexOf.TryGetValue(entry, out var root)) return [];

        Reach(root, ImmutableStack<int>.Empty, []);

        while (pending.Count > 0)
        {
            if (++updates > MaxUpdates)
            {
                Truncated = true;

                return [];
            }

            var (at, flow) = pending.Dequeue();
            var taken = states[(at, string.Join(',', flow))];

            switch (_bytecode[at])
            {
                case EX_PushExecutionFlow push:
                    var address = Offset((int)push.PushingAddress);

                    // A loop back past a sequence node pushes the same address again, so the stack would grow without end.
                    Reach(at + 1, flow.Contains(address) ? flow : flow.Push(address), taken);
                    break;
                case EX_PopExecutionFlow:
                    if (!flow.IsEmpty) Reach(flow.Peek(), flow.Pop(), taken);
                    break;
                case EX_PopExecutionFlowIfNot:
                    Branch(at + 1, flow, taken, at, true);
                    if (!flow.IsEmpty) Branch(flow.Peek(), flow.Pop(), taken, at, false);
                    break;
                case EX_JumpIfNot branch:
                    Branch(Offset((int)branch.CodeOffset), flow, taken, at, false);
                    Branch(at + 1, flow, taken, at, true);
                    break;
                case EX_Jump jump:
                    Reach(Offset((int)jump.CodeOffset), flow, taken);
                    break;
                case EX_Return or EX_EndOfScript or EX_ComputedJump:
                    break;
                default:
                    Reach(at + 1, flow, taken);
                    break;
            }
        }

        var reached = new Dictionary<int, IReadOnlyList<BranchSide>>();

        foreach (var group in states.GroupBy(state => state.Key.At, state => state.Value))
        {
            reached[_bytecode[group.Key].StatementIndex] = group.Aggregate((all, next) => all.RemoveAll(side => !next.Contains(side)));
        }

        return reached;
    }

    private int Offset(int offset) => _indexOf!.GetValueOrDefault(offset, -1);
}
