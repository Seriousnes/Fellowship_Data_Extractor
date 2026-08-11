namespace Fellowship.SDK.Models;

public enum CreatureRank
{
    Enemy,
    Elite,
    Boss,
    Friendly,
}

public sealed record CreatureAbility(EntityReference Ability, string Phase, bool Shown);
