using System.Text.Json.Serialization;

namespace Fellowship.SDK.Models;

public sealed record EffectModifier(string Attribute)
{
    [JsonIgnore]
    public string? Row { get; init; }

    public string? Op { get; init; }

    public double? Value { get; init; }

    public double? ValueLow { get; init; }

    public double? ValueHigh { get; init; }

    public double? Coefficient { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
}
