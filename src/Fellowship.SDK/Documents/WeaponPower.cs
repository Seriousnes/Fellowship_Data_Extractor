namespace Fellowship.SDK.Documents;

/// <summary>An ability a weapon grants: Al'zerac's Shackle, Voidbringer's Touch. 12, all named.</summary>
public sealed class WeaponPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public bool? ScalesWithHaste { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
