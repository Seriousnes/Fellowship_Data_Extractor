namespace Fellowship.SDK;

public sealed record AbilityTooltipText
{
    public double UnitsPerYard { get; init; }

    public double MeleeMaximum { get; init; }

    public int PrintedAboveYards { get; init; }

    public string Range { get; init; } = string.Empty;

    public string MeleeRange { get; init; } = string.Empty;

    public string Self { get; init; } = string.Empty;

    public string RangeUnit { get; init; } = string.Empty;

    public string Instant { get; init; } = string.Empty;

    public string Cast { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string ChargeUp { get; init; } = string.Empty;

    public string CastUnit { get; init; } = string.Empty;

    public string Cooldown { get; init; } = string.Empty;

    public string CooldownUnit { get; init; } = string.Empty;
}
