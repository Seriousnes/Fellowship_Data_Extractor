using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class DungeonAffixDocument : EntityDocument
{
    public string? Description { get; init; }

    public IReadOnlyList<Placeholder>? Placeholders { get; init; }

    public string? Icon { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public int? MinimumHeroRank { get; init; }
}
