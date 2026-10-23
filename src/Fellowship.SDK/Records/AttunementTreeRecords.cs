namespace Fellowship.SDK.Records;

public sealed record AttunementTreeRecord(string Tag)
{
    public bool UseRecipeRowUnlocks { get; init; }

    public int? RowsUnlockedAtItemGeneration { get; init; }

    public IReadOnlyDictionary<int, int> RowToUpgradeRequirement { get; init; } = new Dictionary<int, int>();

    public IReadOnlyList<AttunementTreeNodeRecord> Nodes { get; init; } = [];

    public IReadOnlyList<AttunementNodeUpgradeCostRecord> UpgradeCosts { get; init; } = [];
}

public sealed record AttunementTreeNodeRecord(int UITreeIndex, int AugmentationRowIndex)
{
    public IReadOnlyList<int> ParentNodes { get; init; } = [];

    public IReadOnlyList<int> ChildrenNodes { get; init; } = [];

    public IReadOnlyList<AugmentationCategoryRecord> AugmentationCategories { get; init; } = [];

    public bool RandomizedFromAugmentationCategories { get; init; }

    public int? MinDungeonRatingAtGenerationTime { get; init; }

    public int? EnableAtItemLevel { get; init; }

    public int? MaxRankOverride { get; init; }
}

public sealed record AugmentationCategoryRecord(string Tag)
{
    public string? Name { get; init; }

    public IReadOnlyList<RecordReference> Augmentations { get; init; } = [];
}

public sealed record AttunementNodeUpgradeCostRecord(int Rank, double Cost, RecordReference Currency);
