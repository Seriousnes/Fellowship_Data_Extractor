namespace Fellowship.SDK.Models;

public sealed record EffectModifier(string Attribute)
{
    public string? Op { get; init; }

    public double? Value { get; init; }

    public double? ValueLow { get; init; }

    public double? ValueHigh { get; init; }

    public double? Coefficient { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public string? SetByCaller { get; init; }

    public IReadOnlyList<string>? SourceRequires { get; init; }

    public IReadOnlyList<string>? TargetRequires { get; init; }

    public string? CaptureSource { get; init; }

    public bool? Snapshot { get; init; }
}
