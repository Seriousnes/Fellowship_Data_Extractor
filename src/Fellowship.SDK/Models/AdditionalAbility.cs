namespace Fellowship.SDK.Models;

public sealed record AdditionalAbility
{
    public int Id { get; init; }

    /// <summary>The AbilityCategory of the additional ability's own asset.</summary>
    public AbilityCategory? Category { get; init; }
}
