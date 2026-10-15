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
