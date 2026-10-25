namespace Fellowship.SDK;

public sealed record PrimaryStatPool(int MinimumItemLevel, double LevelDivisor, double GrowthBase, double Multiplier);

public sealed record SecondaryStatPool(int MinimumItemLevel, double LevelMultiplier, double Multiplier);

public sealed record SlotStatSettings
{
    public double? PrimaryMultiplier { get; init; }
    public double? SecondaryMultiplier { get; init; }
    public double? StaminaMultiplierAtThreshold { get; init; }

    public double? WeightMultiplier { get; init; }

    public double? StaminaWeight { get; init; }
    public double? SecondaryWeight { get; init; }
}

public sealed record SlotWeight(double StatWeight, double ArmorWeight);

public sealed record ArmorTypeWeight(double ArmorWeight, double FlatArmor);

public sealed record RatingBracket(double MaxBracketValue, double PenaltyPercentage);

public sealed record AttributeDisplay(
    string DisplayName,
    int RoundToDecimals,
    bool IsPercent,
    string? Attribute = null,
    double BaseStatMultiplier = 1,
    IReadOnlyList<RatingBracket>? Brackets = null,
    double FinalPenaltyPercentage = 1);

public sealed record RaritySlots(
    int Tier,
    IReadOnlyList<string> GuaranteedSlots,
    int RandomRolls,
    IReadOnlyList<string> RandomizedSlots)
{
    public IReadOnlyList<SpecialSlot> SpecialSlots { get; init; } = [];
}

public sealed record SpecialSlot(string SlotName, string? Condition);

public sealed record ItemSlotDefinition(string SlotType, int MinimumItemLevel)
{
    public IReadOnlyList<string> GemPowerPool { get; init; } = [];

    public IReadOnlyList<string> AugmentationPool { get; init; } = [];
}

public sealed record ModifierTypeEntry(string Name)
{
    public string? Printed { get; init; }

    public string? Plural { get; init; }

    public string? Header { get; init; }

    public int? MaximumLimit { get; init; }

    public string? MaximumLimitWarning { get; init; }

    public IReadOnlyList<ModifierRecipe> Recipes { get; init; } = [];
}

public sealed record EmptyModifierSlot(string Sign, string Text);

public sealed record AugmentationTooltipText(
    string EquippedRank,
    string RankSources,
    string GearSlot,
    string RanksOpen,
    string RanksClose)
{
    public IReadOnlyList<ItemTypeText> ItemTypes { get; init; } = [];

    public IReadOnlyList<AugmentationCategoryText> Categories { get; init; } = [];
}

public sealed record AugmentationCategoryText(string? CategoryTag, string Heading, string Band);

public sealed record ItemTypeText(string Type, string Printed, string Slot);

public sealed record ModifierRecipe(string Recipe, int SlotIndex)
{
    public string? Category { get; init; }

    public int Options { get; init; }

    public bool AllOptions { get; init; }
}

public sealed record CraftingRecipeEntry(string Recipe, string Name)
{
    public string? Category { get; init; }

    public string? Description { get; init; }

    public string? Vendor { get; init; }

    public int GoldCost { get; init; }

    public bool GoldCostScalesWithItemLevel { get; init; }

    public IReadOnlyList<RecipeCost> Costs { get; init; } = [];

    public int Inputs { get; init; } = 1;
}

public sealed record RecipeCost(int Currency, int Amount);
