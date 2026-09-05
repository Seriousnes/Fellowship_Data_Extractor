namespace Fellowship.SDK.Records;

public sealed record MountPowerRecord : EntityRecord
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}
