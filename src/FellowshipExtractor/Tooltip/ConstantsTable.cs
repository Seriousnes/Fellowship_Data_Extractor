using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The tuning constants, keyed the way assets reference them. An FScalableFloat names a curve
/// table row; that row name is the key here, which is the link from an ability to its numbers.
///
/// The keys are dotted, and the prefixes they share are meaningful: every row belonging to one
/// ability sits under that ability's name. That grouping is indexed too, because some numbers are
/// read by native code that names no row in any asset, and the table is the only record that they
/// belong to the ability at all.
/// </summary>
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

    /// <summary>
    /// The constant a curve row name resolves to. A key defined in more than one table is
    /// reported rather than silently picking one.
    /// </summary>
    public (ConstantRecord? Record, string[] Tables) Lookup(string? rowName)
    {
        if (rowName is null || !_byKey.TryGetValue(rowName, out var list)) return (null, []);
        return (list[0], [.. list.Select(r => r.Table)]);
    }

    /// <summary>Whether any shipped row sits under this prefix.</summary>
    public bool IsNamespace(string prefix) => _namespaces.Contains(prefix);

    /// <summary>
    /// The rows one segment below a prefix. Deeper rows belong to a talent or a rank tier that is
    /// its own entity, so the immediate ones are the entity's own tuning.
    /// </summary>
    public IReadOnlyList<ConstantRecord> DirectlyUnder(string prefix) =>
        _directChildren.TryGetValue(prefix, out var children) ? children : [];

    /// <summary>
    /// Every row at any depth below a prefix. Right for a talent, whose namespace bottoms out at
    /// the talent itself with nothing else nested inside it, and wrong for an ability, whose
    /// namespace contains its talents.
    /// </summary>
    public IEnumerable<ConstantRecord> AllUnder(string prefix)
    {
        var scope = prefix + '.';
        foreach (var (key, records) in _byKey)
        {
            if (key.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) yield return records[0];
        }
    }
}
