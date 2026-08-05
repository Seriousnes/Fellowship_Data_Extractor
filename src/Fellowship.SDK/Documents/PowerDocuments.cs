using Fellowship.SDK;

namespace Fellowship.SDK.Documents;

/// <summary>
/// A trait an item rolls. A weapon trait upgrades four times and a necklace trait once, and the
/// rank progressions its description prints are the only tie between the printed digits and the
/// numbers the game uses.
/// </summary>
public sealed class Trait : EntityDocument
{
    /// <summary>The category under the name the game prints. The Major tag ships as Master.</summary>
    public string? Category { get; init; }

    /// <summary>How far it upgrades: 4 on every weapon trait, 1 on every necklace trait.</summary>
    public int? MaxRank { get; init; }

    /// <summary>The weight it is rolled at against the others in its category.</summary>
    public int? RandomChance { get; init; }

    public int? PowerId { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<RankRun>? RankRuns { get; init; }
}
