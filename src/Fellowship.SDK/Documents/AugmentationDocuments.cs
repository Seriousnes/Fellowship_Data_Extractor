using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public abstract class AugmentationDocument : EntityDocument
{
    public string? Category { get; init; }

    public string? CategoryTag { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public IReadOnlyList<RankRun>? RankRuns { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}

public sealed class SigilDocument : AugmentationDocument;

public sealed class AspectDocument : AugmentationDocument;

public sealed class ProtectiveEnchantDocument : AugmentationDocument;

public sealed class EnchantDocument : AugmentationDocument
{
    public PrimaryStat? PrimaryStat { get; init; }

    public IReadOnlyList<EnchantStat>? Stats { get; init; }
}
