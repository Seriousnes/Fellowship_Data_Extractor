namespace Fellowship.SDK.Models;

public enum CreatureRank
{
    Enemy,
    Elite,
    Boss,
    Friendly,
}

public sealed record CreatureAbility(EntityReference Ability, string Phase, bool Shown)
{
    public bool? EnabledByDefault { get; init; }

    public PhaseCooldown? InitialCooldown { get; init; }

    public PhaseCooldown? Cooldown { get; init; }

    public PhaseCooldown? InterruptCooldown { get; init; }

    public PhaseCooldown? CCCooldown { get; init; }

    public PhaseCooldown? PostAbilityPadding { get; init; }
}

public sealed record PhaseCooldown(string Type)
{
    public double? Seconds { get; init; }

    public double? MinSeconds { get; init; }

    public double? MaxSeconds { get; init; }
}

public sealed record AbilityCoefficient(string Attribute, double Value)
{
    public string? Part { get; init; }

    public double? Spread { get; init; }
}
