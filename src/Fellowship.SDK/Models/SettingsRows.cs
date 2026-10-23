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

public sealed record GlyphEntry(string Name, string Augmentation, string TierLevel, string TierSources, int Tiers);

public sealed record AttunementTreeEntry(string Tag)
{
    public bool UseRecipeRowUnlocks { get; init; }

    public int? RowsUnlockedAtItemGeneration { get; init; }

    public IReadOnlyDictionary<int, int> RowToUpgradeRequirement { get; init; } = new Dictionary<int, int>();

    public IReadOnlyList<AttunementTreeNode> Nodes { get; init; } = [];

    public AttunementTreeLayout? Layout { get; init; }
}

public sealed record AttunementTreePoint(double X, double Y);

public sealed record AttunementTreeLayout(string Widget, double Width, double Height)
{
    public required AttunementTreePoint Root { get; init; }

    public IReadOnlyList<AttunementTreeRootLayer> RootLayers { get; init; } = [];

    public double NodeWidth { get; init; }

    public double NodeHeight { get; init; }

    public IReadOnlyDictionary<int, AttunementTreePoint> Nodes { get; init; } = new Dictionary<int, AttunementTreePoint>();
}

public sealed record AttunementTreeRootLayer(double Width, double Height)
{
    public string? Texture { get; init; }

    public bool Item { get; init; }
}

public sealed record AttunementNodeColorRows(string Otherwise)
{
    public IReadOnlyDictionary<string, string> ByCategory { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record AttunementTreeNode(int UITreeIndex, int AugmentationRowIndex)
{
    public IReadOnlyList<int> ParentNodes { get; init; } = [];

    public IReadOnlyList<int> ChildrenNodes { get; init; } = [];

    public IReadOnlyList<string> AugmentationCategories { get; init; } = [];

    public bool RandomizedFromAugmentationCategories { get; init; }

    public int? MinDungeonRatingAtGenerationTime { get; init; }

    public int? EnableAtItemLevel { get; init; }

    public int? MaxRankOverride { get; init; }
}

public sealed record AttunementNodeUpgradeCost(int Rank, double Cost, int Currency);

public sealed record DifficultyScaling
{
    public IReadOnlyDictionary<int, double> Health { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<int, double> Damage { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<int, double>? MaxDungeonScore { get; init; }

    public int? ModifierAbilityUnlock { get; init; }

    public IReadOnlyDictionary<string, DifficultyScaling> Namespaces { get; init; } =
        new Dictionary<string, DifficultyScaling>(StringComparer.OrdinalIgnoreCase);
}

public sealed record LeagueKindEntry(string Name, int Value);

public sealed record LeagueEntry(
    string Row,
    string Name,
    string Tag,
    [property: System.Text.Json.Serialization.JsonConverter(
        typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LeagueKind>))]
    LeagueKind Kind)
{
    public int MinimumDifficulty { get; init; }

    public int MaximumDifficulty { get; init; }

    public IReadOnlyList<int> Difficulties { get; init; } = [];
}

public sealed record MultiTargetDamageFalloff
{
    public string Latex { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public IReadOnlyList<FormulaSymbol> Symbols { get; init; } = [];
}

public sealed record FormulaSymbol(string Symbol, string Meaning);
