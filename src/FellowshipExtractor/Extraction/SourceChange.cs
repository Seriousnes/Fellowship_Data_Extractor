using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record SourceChange
{
    public ModifierType? Type { get; init; }

    public ModifierBasis? Basis { get; init; }

    public IReadOnlyList<EntityReference>? Targets { get; init; }

    public string? Attribute { get; init; }

    public double? Value { get; init; }

    public double? ValueLow { get; init; }

    public double? ValueHigh { get; init; }

    public double? PerCount { get; init; }

    public double? Max { get; init; }

    public IReadOnlyList<EntityReference>? Counts { get; init; }

    public int? Uncounted { get; init; }

    public bool? CountsStacks { get; init; }

    public string? CountsCall { get; init; }

    public string? Per { get; init; }

    public double? Coefficient { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public ModifierCondition? Condition { get; init; }

    public string? Row { get; init; }
}
