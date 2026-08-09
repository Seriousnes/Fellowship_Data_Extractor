namespace FellowshipExtractor.Api.Models;

public sealed class StatParameters
{
    private readonly IReadOnlyDictionary<string, string> byName;
    private readonly IReadOnlyDictionary<string, AttributeDisplay> byStat;

    public StatParameters(IReadOnlyDictionary<string, AttributeDisplay> attributes)
    {
        var counted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in attributes.Values)
        {
            if (row.DisplayName is not { Length: > 0 } printed) continue;

            counted[printed] = counted.GetValueOrDefault(printed) + 1;
        }

        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rows = new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);
        var printedNames = new List<string>();

        foreach (var (key, row) in attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (row.DisplayName is not { Length: > 0 } printed) continue;
            if (counted[printed] > 1) continue;

            named[Compact(printed)] = key;
            rows[key] = row;
            printedNames.Add(printed);
        }

        byName = named;
        byStat = rows;
        Names = [.. printedNames.Order(StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<string> Names { get; }

    public string? Resolve(string parameter) =>
        byName.TryGetValue(Compact(parameter), out var stat) ? stat : null;

    public AttributeDisplay Row(string stat) => byStat[stat];

    private static string Compact(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal);
}
