namespace Fellowship.SDK.Records;

public sealed record SetBonusPowerRecord : EntityRecord
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
