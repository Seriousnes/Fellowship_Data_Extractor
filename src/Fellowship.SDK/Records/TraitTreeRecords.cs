namespace Fellowship.SDK.Records;

public sealed record TraitTreeRecord(string Tag)
{
    public bool UseRecipeRowUnlocks { get; init; }

    public int? RowsUnlockedAtItemGeneration { get; init; }

    public IReadOnlyDictionary<int, int> RowToUpgradeRequirement { get; init; } = new Dictionary<int, int>();

    public IReadOnlyList<TraitTreeNodeRecord> Nodes { get; init; } = [];

    public IReadOnlyList<TraitNodeUpgradeCostRecord> UpgradeCosts { get; init; } = [];
}

public sealed record TraitTreeNodeRecord(int UITreeIndex, int TraitRowIndex)
{
    public IReadOnlyList<int> ParentNodes { get; init; } = [];

    public IReadOnlyList<int> ChildrenNodes { get; init; } = [];

    public IReadOnlyList<TraitCategoryRecord> TraitCategories { get; init; } = [];

    public bool RandomizedTraitFromTraitCategories { get; init; }

    public int? MinDungeonRatingAtGenerationTime { get; init; }

    public int? EnableAtItemLevel { get; init; }

    public int? MaxTraitRankOverride { get; init; }
}

public sealed record TraitCategoryRecord(string Tag)
{
    public string? Name { get; init; }

    public IReadOnlyList<RecordReference> Traits { get; init; } = [];
}

public sealed record TraitNodeUpgradeCostRecord(int Rank, double Cost, RecordReference Currency);
