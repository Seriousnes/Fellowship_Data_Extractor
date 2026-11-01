using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class MountPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    /// <summary>Effects the abilities it grants apply or have as parts.</summary>
    public IReadOnlyList<EntityReference>? Applies { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}
