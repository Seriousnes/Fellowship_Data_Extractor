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

public sealed record DifficultyLevel
{
    public string Name { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public double Health { get; init; }

    public double Damage { get; init; }

    public double? MaxDungeonScore { get; init; }
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

/// <summary>
/// Every hero's <c>CRStaggerActorComponent</c>, as <c>BP_PlayerCharacter</c> binds it. The component spreads each
/// part over <see cref="Buckets"/> buckets and, every <see cref="Period"/> seconds while the pool holds Stagger, deals
/// the first bucket as <see cref="DamageEffect"/> and shifts the rest forward.
/// </summary>
public sealed record StaggerSettings
{
    /// <summary>Seconds between ticks (<c>StaggerPeriodRef</c>).</summary>
    public double Period { get; init; }

    public string PeriodRow { get; init; } = string.Empty;

    /// <summary>Bucket count, the ceiling of <c>StaggerDurationRef</c>.</summary>
    public int Buckets { get; init; }

    public string BucketsRow { get; init; } = string.Empty;

    /// <summary>Cap on the pool as a share of MaxHealth (<c>StaggerMaxHealthPercentage</c>).</summary>
    public double MaxHealthPercentage { get; init; }

    public string MaxHealthPercentageRow { get; init; } = string.Empty;

    /// <summary>Effect a tick applies to the owner.</summary>
    public string DamageEffect { get; init; } = string.Empty;

    /// <summary>SetByCaller tag holding a tick's damage.</summary>
    public string DamageSetByCallerTag { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    /// <summary>Attribute the cap multiplies: the owner's current value at each add.</summary>
    public string MaxHealthAttribute { get; init; } = string.Empty;

    /// <summary>Attribute of a hit's target. The pool takes the hit's Health damage times it.</summary>
    public string DamageAdditionFactorAttribute { get; init; } = string.Empty;

    /// <summary>Attribute of a heal's target. The heal removes its amount times it from the pool.</summary>
    public string HealReductionFactorAttribute { get; init; } = string.Empty;

    /// <summary>Tag of a heal effect <see cref="HealReductionFactorAttribute"/> applies to.</summary>
    public string HealAffectStaggerTag { get; init; } = string.Empty;

    /// <summary>Tags of a damage effect the pool takes no part of.</summary>
    public IReadOnlyList<string> DamageIgnoreStaggerTags { get; init; } = [];

    /// <summary>Execution adding its set-by-caller magnitude to the target's pool. A negative magnitude is a cleanse.</summary>
    public string ModifyExecution { get; init; } = string.Empty;

    /// <summary>Set-by-caller tag <see cref="ModifyExecution"/> reads.</summary>
    public string ModifySetByCallerTag { get; init; } = string.Empty;

    /// <summary>Resource type of the <c>RESOURCE_CHANGED</c> combat log line a <see cref="ModifyExecution"/> delta writes.</summary>
    public int LoggedResourceType { get; init; }

    public string CapText { get; init; } = string.Empty;

    public string RemovalText { get; init; } = string.Empty;

    public string LoggedChangeText { get; init; } = string.Empty;
}

/// <summary>
/// The chance <c>CRRandomStreamComponent.RollProcPerMinute</c> gives one roll at a rate, as the shipping binary
/// computes it. A proc roll's rate is its <see cref="ProcRoll.Formula"/>.
/// </summary>
public sealed record ProcPerMinuteRoll
{
    public string Latex { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public IReadOnlyList<FormulaSymbol> Symbols { get; init; } = [];

    /// <summary>The attribute a hasted rate is multiplied by. It holds 1 plus the Haste percentage.</summary>
    public string HasteAttribute { get; init; } = string.Empty;

    /// <summary>Until a stream's first proc, the chance is at least the rate divided by this.</summary>
    public double FirstProcChanceFloorDivisor { get; init; }

    /// <summary>The most seconds since the last proc the drought factor counts.</summary>
    public double LongestDrought { get; init; }
}

/// <summary>
/// The split of one hit between the target's absorption effects and its Health, as the shipping binary's damage
/// calculation computes it, and the amount the combat log writes for the hit.
/// </summary>
public sealed record DamageAbsorption
{
    public string DisplayText { get; init; } = string.Empty;

    /// <summary>The amount, hit points and absorption of the hit's combat log line.</summary>
    public string LoggedAmountText { get; init; } = string.Empty;

    public IReadOnlyList<FormulaSymbol> Symbols { get; init; } = [];

    /// <summary>The tag of an effect the hit is split over.</summary>
    public string EffectTag { get; init; } = string.Empty;

    /// <summary>The set-by-caller magnitude with an effect's pool.</summary>
    public string PoolMagnitude { get; init; } = string.Empty;

    /// <summary>The set-by-caller magnitude with the share of absorbed damage an effect's pool loses.</summary>
    public string ConsumptionScalerMagnitude { get; init; } = string.Empty;

    /// <summary>The consumption scaler of an effect with no such magnitude.</summary>
    public double DefaultConsumptionScaler { get; init; }

    /// <summary>The factor on an effect's consumption scaler for a hit its deduction crits on.</summary>
    public double DeductionCritScaler { get; init; }

    /// <summary>The attribute of the effect's instigator the deduction crit chance comes from.</summary>
    public string CritChanceAttribute { get; init; } = string.Empty;

    /// <summary>The most times one hit goes through the effects.</summary>
    public int MostPasses { get; init; }
}
