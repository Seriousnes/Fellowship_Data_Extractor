using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The tuning constants, keyed the way assets reference them. An FScalableFloat names a curve
/// table row; that row name is the key here, which is the link from an ability to its numbers.
/// </summary>
public sealed class ConstantsTable
{
    private readonly Dictionary<string, List<ConstantRecord>> _byKey;

    public ConstantsTable(AbstractFileProvider provider)
    {
        _byKey = new Dictionary<string, List<ConstantRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in CsvExportCommands.ReadConstants(provider))
        {
            if (!_byKey.TryGetValue(record.Key, out var list)) _byKey[record.Key] = list = [];
            list.Add(record);
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
}
