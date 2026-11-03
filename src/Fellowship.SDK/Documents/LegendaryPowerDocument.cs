using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class LegendaryPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    /// <summary>Proc-per-minute rolls the granted abilities make, each with the rate it is made at.</summary>
    public IReadOnlyList<ProcRoll>? ProcRolls { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    /// <summary>Effects the abilities it grants apply or have as parts.</summary>
    public IReadOnlyList<EntityReference>? Applies { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public IReadOnlyList<Change>? Changes { get; init; }

    public IReadOnlyDictionary<string, double>? Constants { get; init; }
}
