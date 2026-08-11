namespace Fellowship.SDK.Models;

public enum CreatureRank
{
    Enemy,
    Elite,
    Boss,
    Friendly,
}

public sealed record CreatureAbility(EntityReference Ability, string Phase, bool Shown);

public sealed record AbilityCoefficient(string Attribute, double Value)
{
    public string? Part { get; init; }

    public double? Spread { get; init; }
}
