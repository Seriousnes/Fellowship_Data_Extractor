using System.Text.Json.Serialization;

namespace Fellowship.SDK.Models;

public sealed record EffectModifier(string Attribute)
{
    /// <summary>The symbol <see cref="Formula"/> gives the effect's stack count.</summary>
    public const string Stacks = "Stacks";

    [JsonIgnore]
    public string? Row { get; init; }

    public string? Op { get; init; }

    /// <summary>The value at rank 1, or the one value of a modifier without ranks.</summary>
    public double? Value { get; init; }

    /// <summary>The value at each rank, from rank 1. A sigil or glyph modifier reading a ranked row has them.</summary>
    public IReadOnlyList<double>? Ranks { get; init; }

    /// <summary>
    /// The value at the effect's maximum stacks: stacks sum an additive or multiplicitive op's terms, and
    /// compound a MultiplyCompound factor.
    /// </summary>
    public double? AtMaxStacks { get; init; }

    public double? ValueLow { get; init; }

    public double? ValueHigh { get; init; }

    public double? Coefficient { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    /// <summary>
    /// The magnitude a custom calculation computes, in the attributes of <see cref="ScalesOff"/> and
    /// <see cref="Stacks"/>, where it has no one value.
    /// </summary>
    public string? Formula { get; init; }

    public bool Equals(EffectModifier? other) =>
        other is not null &&
        Attribute == other.Attribute && Row == other.Row && Op == other.Op && Value == other.Value &&
        ValueLow == other.ValueLow && ValueHigh == other.ValueHigh && Coefficient == other.Coefficient && Formula == other.Formula &&
        Same(Ranks, other.Ranks) && Same(ScalesOff, other.ScalesOff);

    public override int GetHashCode() =>
        HashCode.Combine(Attribute, Row, Op, Value, ValueLow, ValueHigh, Coefficient, Formula);

    private static bool Same<T>(IReadOnlyList<T>? first, IReadOnlyList<T>? second) =>
        first is null ? second is null : second is not null && first.SequenceEqual(second);
}
