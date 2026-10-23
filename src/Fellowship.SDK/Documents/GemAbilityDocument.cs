using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class GemAbilityDocument : EntityDocument
{
    public required GemType Gem { get; init; }

    public required int Node { get; init; }

    public required int RequiredPower { get; init; }

    public EntityReference? Replaces { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
