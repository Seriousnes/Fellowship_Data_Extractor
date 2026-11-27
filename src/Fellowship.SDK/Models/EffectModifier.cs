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

    /// <summary>
    /// The SetByCaller tag the magnitude reads, for a modifier whose value the applying ability or actor sets on the
    /// effect spec.
    /// </summary>
    public string? SetByCaller { get; init; }

    /// <summary>
    /// The modifier's <c>SourceTags.RequireTags</c>: tags of the source of the spec an attribute is evaluated for,
    /// each of which that spec needs for the modifier to count. A spec's source tags include its effect's asset tags.
    /// </summary>
    public IReadOnlyList<string>? SourceRequireTags { get; init; }

    /// <summary>The modifier's <c>SourceTags.IgnoreTags</c>: a spec whose source has any of them leaves the modifier out.</summary>
    public IReadOnlyList<string>? SourceIgnoreTags { get; init; }

    /// <summary>The modifier's <c>TargetTags.RequireTags</c>: tags the target needs for the modifier to count.</summary>
    public IReadOnlyList<string>? TargetRequireTags { get; init; }

    /// <summary>The modifier's <c>TargetTags.IgnoreTags</c>: a target with any of them leaves the modifier out.</summary>
    public IReadOnlyList<string>? TargetIgnoreTags { get; init; }

    public bool Equals(EffectModifier? other) =>
        other is not null &&
        Attribute == other.Attribute && Row == other.Row && Op == other.Op && Value == other.Value &&
        ValueLow == other.ValueLow && ValueHigh == other.ValueHigh && Coefficient == other.Coefficient && Formula == other.Formula &&
        SetByCaller == other.SetByCaller && Same(Ranks, other.Ranks) && Same(ScalesOff, other.ScalesOff) &&
        Same(SourceRequireTags, other.SourceRequireTags) && Same(SourceIgnoreTags, other.SourceIgnoreTags) &&
        Same(TargetRequireTags, other.TargetRequireTags) && Same(TargetIgnoreTags, other.TargetIgnoreTags);

    public override int GetHashCode() =>
        HashCode.Combine(Attribute, Row, Op, Value, ValueLow, ValueHigh, Coefficient, HashCode.Combine(Formula, SetByCaller));

    private static bool Same<T>(IReadOnlyList<T>? first, IReadOnlyList<T>? second) =>
        first is null ? second is null : second is not null && first.SequenceEqual(second);
}
