using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public abstract class ItemTraitDocument : EntityDocument
{
    public string? Category { get; init; }

    public string? CategoryTag { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public EntityReference? Power { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<RankRun>? RankRuns { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}

public sealed class SigilDocument : ItemTraitDocument;

public sealed class AspectDocument : ItemTraitDocument;

public sealed class ProtectiveEnchantDocument : ItemTraitDocument;

public sealed class EnchantDocument : ItemTraitDocument
{
    public PrimaryStat? PrimaryStat { get; init; }

    public IReadOnlyList<Placeholder>? Placeholders { get; init; }

    public IReadOnlyList<TraitStat>? Stats { get; init; }
}
