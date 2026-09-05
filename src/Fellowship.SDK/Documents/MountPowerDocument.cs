using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class MountPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}
