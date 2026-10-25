namespace Fellowship.SDK.Models;

public sealed record ModifierCondition
{
    public IReadOnlyList<EntityReference>? Effects { get; init; }

    public IReadOnlyList<EntityReference>? OwnEffects { get; init; }

    public double? Stacks { get; init; }

    public IReadOnlyList<EntityReference>? TargetEffects { get; init; }

    public double? Chance { get; init; }

    public IReadOnlyList<EntityReference>? Talents { get; init; }

    public IReadOnlyList<EntityReference>? CastBy { get; init; }

    public IReadOnlyList<EntityReference>? Cooldowns { get; init; }

    public IReadOnlyList<string>? States { get; init; }

    public IReadOnlyList<EntityReference>? StateSources { get; init; }

    public IReadOnlyDictionary<string, double>? HealthFractions { get; init; }

    public IReadOnlyList<string>? TargetStates { get; init; }

    public IReadOnlyDictionary<string, double>? TargetHealthFractions { get; init; }
}
