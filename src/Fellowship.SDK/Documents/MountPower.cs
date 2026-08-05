namespace Fellowship.SDK.Documents;

/// <summary>What a mount gives its rider.</summary>
public sealed class MountPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}
