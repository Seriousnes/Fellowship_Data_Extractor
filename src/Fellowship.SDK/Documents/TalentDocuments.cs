using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class TalentDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }

    public IReadOnlyList<TargetCountFalloff>? TargetCountFalloff { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public IReadOnlyList<Change>? Changes { get; init; }
}
