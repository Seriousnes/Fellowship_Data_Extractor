namespace Fellowship.SDK;

public sealed record StatCalculationFile
{
    public PrimaryStatPool? PrimaryPool { get; init; }
    public SecondaryStatPool? SecondaryPool { get; init; }

    public double PrimaryPowerBase { get; init; }
    public double PrimaryAndStaminaMultiplier { get; init; }

    public double StatRarityBase { get; init; }

    public SlotStatSettings BaseSlot { get; init; } = new();

    public IReadOnlyDictionary<string, SlotWeight> SlotWeights { get; init; } =
        new Dictionary<string, SlotWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ArmorTypeWeight> ArmorWeights { get; init; } =
        new Dictionary<string, ArmorTypeWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AttributeDisplay> Attributes { get; init; } =
        new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityAdditions { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ItemSlotDefinition> SlotDefinitions { get; init; } =
        new Dictionary<string, ItemSlotDefinition>(StringComparer.Ordinal);
}

public sealed record SettingsFile
{
    public IReadOnlyList<HeroEntry> Heroes { get; init; } = [];

    public IReadOnlyList<RarityEntry> Rarities { get; init; } = [];

    public IReadOnlyList<AbilityCategoryEntry> AbilityCategories { get; init; } = [];

    public IReadOnlyDictionary<string, string> PrintFormats { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Colors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TextStyle> TextStyles { get; init; } =
        new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GemTypes { get; init; } = [];

    public IReadOnlyDictionary<string, string> SlotIcons { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> RarityOrnaments { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<int, string> TalentTierPlates { get; init; } = new Dictionary<int, string>();

    public IReadOnlyDictionary<string, string> RarityColorRows { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> SectionHeadingRows { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<TemperCeiling>> MaxTempers { get; init; } =
        new Dictionary<string, IReadOnlyList<TemperCeiling>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DropItemLevel> DropItemLevels { get; init; } = [];

    public GlyphEntry Glyphs { get; init; } = new(string.Empty, string.Empty, string.Empty, string.Empty, 0);

    public DifficultyScaling DifficultyScaling { get; init; } = new();

    public MultiTargetDamageFalloff MultiTargetDamageFalloff { get; init; } = new();

    public IReadOnlyList<LeagueKindEntry> LeagueKinds { get; init; } = [];

    public IReadOnlyList<LeagueEntry> Leagues { get; init; } = [];

    public IReadOnlyList<AttunementTreeEntry> AttunementTrees { get; init; } = [];

    public IReadOnlyList<AttunementNodeUpgradeCost> AttunementNodeUpgradeCosts { get; init; } = [];

    public AttunementNodeColorRows? AttunementNodeColorRows { get; init; }

    public string? AttunementTreeHeader { get; init; }

    public AugmentationTooltipText? AugmentationTooltip { get; init; }

    public AbilityTooltipText? AbilityTooltip { get; init; }

    public IReadOnlyList<ModifierTypeEntry> ModifierTypes { get; init; } = [];

    public IReadOnlyList<CraftingRecipeEntry> CraftingRecipes { get; init; } = [];

    public EmptyModifierSlot? EmptyModifierSlot { get; init; }

    public StatCalculationFile StatCalculation { get; init; } = new();

    public ItemSettingsFile Items { get; init; } = new();
}
