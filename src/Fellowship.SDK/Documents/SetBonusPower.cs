namespace Fellowship.SDK.Documents;

/// <summary>The mechanics behind a set bonus.</summary>
public sealed class SetBonusPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
