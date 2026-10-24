namespace Fellowship.SDK.Models;

public sealed record EffectStacking(string Type)
{
    public string? DurationRefresh { get; init; }

    public string? PeriodReset { get; init; }

    public string? Expiration { get; init; }
}
