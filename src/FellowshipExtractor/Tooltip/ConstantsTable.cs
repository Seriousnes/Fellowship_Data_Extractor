using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Tooltip;

public sealed class ConstantsTable
{
    private readonly Dictionary<string, List<ConstantRecord>> _byKey;
    private readonly HashSet<string> _namespaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ConstantRecord>> _directChildren = new(StringComparer.OrdinalIgnoreCase);

    public ConstantsTable(AbstractFileProvider provider)
    {
        _byKey = new Dictionary<string, List<ConstantRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in CsvExportCommands.ReadConstants(provider))
        {
            if (!_byKey.TryGetValue(record.Key, out var list)) _byKey[record.Key] = list = [];
            list.Add(record);

            var segments = record.Key.Split('.');
            for (var i = 1; i < segments.Length; i++)
            {
                _namespaces.Add(string.Join('.', segments[..i]));
            }

            if (segments.Length < 2) continue;

            var parent = string.Join('.', segments[..^1]);
            if (!_directChildren.TryGetValue(parent, out var children)) _directChildren[parent] = children = [];
            children.Add(record);
        }
    }

    public int Count => _byKey.Count;

    public (ConstantRecord? Record, string[] Tables) Lookup(string? rowName)
    {
        if (rowName is null || !_byKey.TryGetValue(rowName, out var list)) return (null, []);
        return (list[0], [.. list.Select(r => r.Table)]);
    }

    public bool IsNamespace(string prefix) => _namespaces.Contains(prefix);

    public IReadOnlyList<ConstantRecord> DirectlyUnder(string prefix) =>
        _directChildren.TryGetValue(prefix, out var children) ? children : [];

    public IEnumerable<ConstantRecord> AllUnder(string prefix)
    {
        var scope = prefix + '.';
        foreach (var (key, records) in _byKey)
        {
            if (key.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) yield return records[0];
        }
    }
}
