namespace Fellowship.SDK;

public sealed record HeroEntry(string Name, string? ArmorType, string PrimaryStat)
{
    public string? Color { get; init; }

    public string? Portrait { get; init; }
}

public sealed record RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName);

public sealed record AbilityCategoryEntry(string Name, int Value);

public sealed record TemperCeiling(int ItemLevel, int MaxTempers);

public sealed record DropItemLevel(string Row, int Difficulty, int ItemLevel);

public sealed record BlessingEntry(string Name, string HeroBlessing, string TierLevel, int Tiers);

public sealed record TraitTreeEntry(string Tag)
{
    public bool UseRecipeRowUnlocks { get; init; }

    public int? RowsUnlockedAtItemGeneration { get; init; }

    public IReadOnlyDictionary<int, int> RowToUpgradeRequirement { get; init; } = new Dictionary<int, int>();

    public IReadOnlyList<TraitTreeNode> Nodes { get; init; } = [];
}

public sealed record TraitTreeNode(int UITreeIndex, int TraitRowIndex)
{
    public IReadOnlyList<int> ParentNodes { get; init; } = [];

    public IReadOnlyList<int> ChildrenNodes { get; init; } = [];

    public IReadOnlyList<string> TraitCategories { get; init; } = [];

    public bool RandomizedTraitFromTraitCategories { get; init; }

    public int? MinDungeonRatingAtGenerationTime { get; init; }

    public int? EnableAtItemLevel { get; init; }

    public int? MaxTraitRankOverride { get; init; }
}

public sealed record TraitNodeUpgradeCost(int Rank, double Cost, int Currency);

public sealed record DifficultyScaling
{
    public IReadOnlyDictionary<int, double> Health { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<int, double> Damage { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<int, double>? MaxDungeonScore { get; init; }

    public int? ModifierAbilityUnlock { get; init; }

    public IReadOnlyDictionary<string, DifficultyScaling> Namespaces { get; init; } =
        new Dictionary<string, DifficultyScaling>(StringComparer.OrdinalIgnoreCase);
}

[System.Text.Json.Serialization.JsonConverter(
    typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LeagueKind>))]
public enum LeagueKind
{
    Ranked = 1,
    Eternal = 2,
    Pinnacle = 3,
}

public sealed record LeagueEntry(string Row, string Name, string Tag, LeagueKind Kind)
{
    public int MinimumDifficulty { get; init; }

    public int MaximumDifficulty { get; init; }

    public IReadOnlyList<int> Difficulties { get; init; } = [];
}

public sealed record MultiTargetDamageFalloff
{
    public string Latex { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;
}
