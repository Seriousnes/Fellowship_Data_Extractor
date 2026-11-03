using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class GemAbilityDocument : EntityDocument
{
    public required GemType Gem { get; init; }

    public required int Node { get; init; }

    public required int RequiredPower { get; init; }

    public EntityReference? Replaces { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    /// <summary>Effects the abilities it grants apply or have as parts.</summary>
    public IReadOnlyList<EntityReference>? Applies { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    /// <summary>Proc-per-minute rolls the granted abilities make, each with the rate it is made at.</summary>
    public IReadOnlyList<ProcRoll>? ProcRolls { get; init; }
}
