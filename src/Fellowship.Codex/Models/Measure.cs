using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

/// <param name="Key">Query key of the measure, the stem of its comparison parameters and its sort key.</param>
public sealed record Measure(string Key, string Label, Func<EntityDocument, double?> Value)
{
    public Measure(MeasureType type, Func<EntityDocument, double?> value)
        : this(QueryKeys.Key(type), CodexKeys.Label(type), value)
    {
    }

    public string? Unit { get; init; }

    /// <summary>The value is the item's at its drop item level, so a record list's item reading changes it.</summary>
    public bool Itemised { get; init; }

    public MeasureGroup Group { get; init; } = MeasureGroup.Stats;

    /// <summary>The rail offers a comparison on the measure. A query still takes one when the rail does not.</summary>
    public bool Offered { get; init; } = true;
}
