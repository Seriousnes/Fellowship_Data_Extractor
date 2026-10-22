using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class TraitDocument : EntityDocument
{
    public string? Category { get; init; }

    public string? CategoryTag { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public EntityReference? Power { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<RankRun>? RankRuns { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public IReadOnlyDictionary<string, string>? NameByHero { get; init; }

    public IReadOnlyList<TraitStat>? Stats { get; init; }
}
