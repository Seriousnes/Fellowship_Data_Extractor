using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Blueprints;

public static class KismetListing
{
    private const string Indent = "    ";

    private abstract record Node(int Head);

    private sealed record Statement(int Head) : Node(Head);

    private sealed record Branch(int Head, Node[] Then, int ElseHead, Node[] Else) : Node(Head);

    private sealed record Sequence(int Head, Node[] Body) : Node(Head);

    private sealed record Edge(int Source, long Target);

    public static IReadOnlyList<string> Flat(
        IReadOnlyList<KismetExpression> statements,
        IReadOnlyDictionary<long, IReadOnlyList<string>>? entries = null)
    {
        var listing = new Listing(statements, entries ?? new Dictionary<long, IReadOnlyList<string>>());
        var nodes = Enumerable.Range(0, statements.Count).Select(at => (Node)new Statement(at)).ToArray();
        return listing.Render(nodes);
    }

    public static IReadOnlyList<string> Structured(
        IReadOnlyList<KismetExpression> statements,
        IReadOnlyDictionary<long, IReadOnlyList<string>>? entries = null)
    {
        var listing = new Listing(statements, entries ?? new Dictionary<long, IReadOnlyList<string>>());
        return listing.Render(listing.Structure(0, statements.Count));
    }

    public static IEnumerable<long> Targets(KismetExpression statement) => statement switch
    {
        EX_Jump jump => [jump.CodeOffset],
        EX_PushExecutionFlow push => [push.PushingAddress],
        EX_AutoRtfmTransact transact => [transact.CodeOffset],
        _ => [],
    };

    private sealed class Listing
    {
        private readonly IReadOnlyList<KismetExpression> _statements;
        private readonly IReadOnlyDictionary<long, IReadOnlyList<string>> _entries;
        private readonly Dictionary<long, int> _byOffset = [];
        private readonly List<Edge> _edges = [];
        private readonly HashSet<int> _absorbed = [];

        public Listing(IReadOnlyList<KismetExpression> statements, IReadOnlyDictionary<long, IReadOnlyList<string>> entries)
        {
            _statements = statements;
            _entries = entries;

            for (var at = 0; at < statements.Count; at++)
            {
                _byOffset.TryAdd(statements[at].StatementIndex, at);
                foreach (var target in Targets(statements[at])) _edges.Add(new Edge(at, target));
            }

            foreach (var offset in entries.Keys) _edges.Add(new Edge(-1, offset));
        }

        public Node[] Structure(int low, int high)
        {
            var nodes = new List<Node>();
            var at = low;

            while (at < high)
            {
                var statement = _statements[at];

                if (statement.GetType() == typeof(EX_JumpIfNot) &&
                    Index(((EX_JumpIfNot)statement).CodeOffset) is { } target &&
                    target > at + 1 && target <= high &&
                    SingleEntry(at + 1, target, at))
                {
                    var last = _statements[target - 1];

                    if (last.GetType() == typeof(EX_Jump) &&
                        target - 1 > at + 1 &&
                        Index(((EX_Jump)last).CodeOffset) is { } end &&
                        end > target && end <= high &&
                        !_edges.Any(edge => Index(edge.Target) == target - 1) &&
                        SingleEntry(target, end, at))
                    {
                        _absorbed.Add(at);
                        _absorbed.Add(target - 1);
                        nodes.Add(new Branch(at, Structure(at + 1, target - 1), target, Structure(target, end)));
                        at = end;
                        continue;
                    }

                    _absorbed.Add(at);
                    nodes.Add(new Branch(at, Structure(at + 1, target), -1, []));
                    at = target;
                    continue;
                }

                if (statement is EX_PushExecutionFlow push &&
                    Index(push.PushingAddress) is { } resume &&
                    resume > at + 1 && resume <= high &&
                    _statements[resume - 1].GetType() == typeof(EX_PopExecutionFlow) &&
                    SingleEntry(at + 1, resume, -2))
                {
                    nodes.Add(new Sequence(at, Structure(at + 1, resume)));
                    at = resume;
                    continue;
                }

                nodes.Add(new Statement(at));
                at++;
            }

            return [.. nodes];
        }

        private int? Index(long offset) => _byOffset.TryGetValue(offset, out var at) ? at : null;

        private bool SingleEntry(int low, int high, int allowed)
        {
            foreach (var edge in _edges)
            {
                if (Index(edge.Target) is not { } target || target < low || target >= high) continue;
                if (edge.Source >= low && edge.Source < high) continue;
                if (edge.Source == allowed && target == low && edge.Source >= 0) continue;
                return false;
            }

            return true;
        }

        public IReadOnlyList<string> Render(Node[] nodes)
        {
            var labelled = _edges
                .Where(edge => edge.Source < 0 || !_absorbed.Contains(edge.Source))
                .Select(edge => edge.Target)
                .ToHashSet();

            var lines = new List<string>();
            Write(nodes, 0, labelled, lines);

            foreach (var missing in labelled.Where(offset => !_byOffset.ContainsKey(offset)).Order())
            {
                lines.Add($"{string.Empty,6}  {KismetPrinter.Label(missing)}: no statement at this offset");
            }

            return lines;
        }

        private void Write(Node[] nodes, int depth, HashSet<long> labelled, List<string> lines)
        {
            var indent = string.Concat(Enumerable.Repeat(Indent, depth));

            foreach (var node in nodes)
            {
                WriteLabel(node.Head, indent, labelled, lines);
                var statement = _statements[node.Head];
                var offset = $"{statement.StatementIndex,6}";

                switch (node)
                {
                    case Branch branch:
                        var condition = KismetPrinter.Expression(((EX_JumpIfNot)statement).BooleanExpression);
                        lines.Add($"{offset}  {indent}if ({condition}) {{");
                        Write(branch.Then, depth + 1, labelled, lines);

                        if (branch.ElseHead >= 0)
                        {
                            lines.Add($"{_statements[branch.ElseHead - 1].StatementIndex,6}  {indent}}} else {{");
                            Write(branch.Else, depth + 1, labelled, lines);
                        }

                        lines.Add($"{string.Empty,6}  {indent}}}");
                        break;

                    case Sequence sequence:
                        var resume = ((EX_PushExecutionFlow)statement).PushingAddress;
                        lines.Add($"{offset}  {indent}sequence then {KismetPrinter.Label(resume)} {{");
                        Write(sequence.Body, depth + 1, labelled, lines);
                        lines.Add($"{string.Empty,6}  {indent}}}");
                        break;

                    default:
                        lines.Add($"{offset}  {indent}{KismetPrinter.Expression(statement)}");
                        break;
                }
            }
        }

        private void WriteLabel(int head, string indent, HashSet<long> labelled, List<string> lines)
        {
            var offset = (long)_statements[head].StatementIndex;
            if (!labelled.Contains(offset)) return;

            var label = $"{string.Empty,6}  {indent}{KismetPrinter.Label(offset)}:";
            lines.Add(_entries.TryGetValue(offset, out var names)
                ? $"{label}  // entry from {string.Join(", ", names)}"
                : label);
        }
    }
}
