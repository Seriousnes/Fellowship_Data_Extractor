using System.ComponentModel;

namespace Fellowship.Codex.Models;

/// <summary>
/// Relation between a measure and the number of a <see cref="Comparison"/>. The member name is the suffix of
/// the parameter: <c>?hasteGt=0</c>.
/// </summary>
public enum Comparator
{
    [Description(">")]
    Gt,

    [Description("≥")]
    Min,

    [Description("=")]
    Eq,

    [Description("≤")]
    Max,

    [Description("<")]
    Lt,
}

/// <param name="Measure">Key of the measure.</param>
public sealed record Comparison(string Measure, Comparator Comparator, double Value)
{
    public string Parameter => Comparisons.Parameter(Measure, Comparator);

    public bool Admits(double value) => Comparator switch
    {
        Comparator.Gt => value > Value,
        Comparator.Min => value >= Value,
        Comparator.Eq => value == Value,
        Comparator.Max => value <= Value,
        Comparator.Lt => value < Value,
        _ => throw new ArgumentOutOfRangeException(nameof(Comparator), Comparator, null),
    };
}

public static class Comparisons
{
    public const Comparator Default = Comparator.Gt;

    public const double DefaultValue = 0;

    public static string Suffix(Comparator comparator) => comparator.ToString();

    public static string Note { get; } =
        "A comparison parameter is a measure key followed by a comparator: `?hasteGt=0`. The comparators are " +
        string.Join(", ", Enum.GetValues<Comparator>().Select(comparator => $"`{Suffix(comparator)}` ({CodexKeys.Label(comparator)})")) +
        ". A query can hold several comparisons on one measure: `?cooldownMin=3&cooldownLt=10`.";

    public static string Parameter(string measure, Comparator comparator) => measure + Suffix(comparator);

    /// <summary>Measure key and comparator of a parameter such as <c>hasteGt</c>.</summary>
    public static bool TryParse(string key, IEnumerable<string> measures, out string measure, out Comparator comparator)
    {
        foreach (var member in Enum.GetValues<Comparator>())
        {
            var suffix = Suffix(member);

            if (!key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

            var stem = key[..^suffix.Length];

            if (measures.FirstOrDefault(known => known.Equals(stem, StringComparison.OrdinalIgnoreCase)) is { } named)
            {
                measure = named;
                comparator = member;
                return true;
            }
        }

        measure = string.Empty;
        comparator = default;
        return false;
    }

    public static IEnumerable<string> Parameters(IEnumerable<string> measures) =>
        measures.SelectMany(measure => Enum.GetValues<Comparator>().Select(comparator => Parameter(measure, comparator)));
}
