using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class LegendaryStatDocument : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }
}
