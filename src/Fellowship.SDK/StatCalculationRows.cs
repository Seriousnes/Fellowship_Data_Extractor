namespace Fellowship.SDK;

/// <summary>
/// The primary stat pool an item level is worth, as the coefficients of the curve the build draws
/// it with: <c>multiplier x growthBase^(itemLevel / levelDivisor) x sqrt(itemLevel)</c>, and zero
/// below <see cref="MinimumItemLevel"/>.
/// </summary>
public sealed record PrimaryStatPool(int MinimumItemLevel, double LevelDivisor, double GrowthBase, double Multiplier);

/// <summary>
/// The secondary stat pool an item level is worth: <c>multiplier x sqrt(itemLevel x
/// levelMultiplier)</c>, and zero below <see cref="MinimumItemLevel"/>.
/// </summary>
public sealed record SecondaryStatPool(int MinimumItemLevel, double LevelMultiplier, double Multiplier);

/// <summary>
/// What one kind of slot does to the share of the pool it draws, under the names the shipped
/// settings give them.
///
/// All three kinds are the same struct and a cooked package writes only the properties that differ
/// from the class default, so a field absent here is one the designers left alone rather than one
/// the struct lacks. What the default is is C++ and not readable, which is why an absent multiplier
/// is left null for the consumer to decide rather than published as a number this cannot see.
/// </summary>
public sealed record SlotStatSettings
{
    public double? PrimaryMultiplier { get; init; }
    public double? SecondaryMultiplier { get; init; }
    public double? StaminaMultiplierAtThreshold { get; init; }

    /// <summary>The share of the pool a slot with no weight of its own is worth.</summary>
    public double? WeightMultiplier { get; init; }

    public double? StaminaWeight { get; init; }
    public double? SecondaryWeight { get; init; }
}

/// <summary>What a slot in an equipment position is worth, and how much armour it carries.</summary>
public sealed record SlotWeight(double StatWeight, double ArmorWeight);

/// <summary>What an armour type multiplies a slot's armour by, and the flat armour it adds.</summary>
public sealed record ArmorTypeWeight(double ArmorWeight, double FlatArmor);

/// <summary>
/// One band of a rating's diminishing returns: the share of the raw percentage kept up to
/// <see cref="MaxBracketValue"/>. The bands are cumulative, so a raw percentage past a band's top
/// keeps that band's share of the part inside it and moves on to the next.
/// </summary>
public sealed record RatingBracket(double MaxBracketValue, double PenaltyPercentage);

/// <summary>
/// How the game writes one attribute, and how a rating on an item becomes the attribute it feeds.
///
/// A gear slot holds a rating and the attribute behind it is a percentage: 50 haste rating is 8%,
/// through BaseStatMultiplier of 0.16. Past that the brackets take their cut, keeping the whole of
/// the first 10 percent, 98% of the next five, 96% of the five after and 94% of the five after
/// that, with FinalPenaltyPercentage on everything above the last band.
///
/// The four rated stats say so themselves by naming a different attribute from the one their row is
/// keyed by: the Haste row's attribute is HasteRating on CRHeroAttributeSet, while the Haste that
/// gameplay effects capture is on CRBasicAttributeSet. They are two quantities and this row is the
/// conversion between them.
///
/// A stat with no brackets and a multiplier of one is not a rating at all - Strength, Intellect and
/// Stamina are the number they say - and converting one is the identity.
/// </summary>
public sealed record AttributeDisplay(
    string DisplayName,
    int RoundToDecimals,
    bool IsPercent,
    string? Attribute = null,
    double BaseStatMultiplier = 1,
    IReadOnlyList<RatingBracket>? Brackets = null,
    double FinalPenaltyPercentage = 1);

/// <summary>
/// The extra slots a rarity grants an item on top of the ones the item itself declares. The
/// guaranteed ones are every copy of the item at that rarity; the rolled ones are a count drawn
/// from a pool and differ per drop, so they are carried as the count and the pool rather than as
/// an answer.
/// </summary>
public sealed record RaritySlots(
    int Tier,
    IReadOnlyList<string> GuaranteedSlots,
    int RandomRolls,
    IReadOnlyList<string> RandomizedSlots)
{
    /// <summary>
    /// Slots the rarity grants only where a condition holds. One shipped row uses it: a legendary
    /// gains a gem socket under a condition of its own, which is why a legendary reads as having no
    /// socket if only the guaranteed list is consulted. The condition is carried as the build
    /// spells it rather than evaluated, because nothing here knows what its arguments stand for.
    /// </summary>
    public IReadOnlyList<SpecialSlot> SpecialSlots { get; init; } = [];
}

/// <summary>A slot a rarity grants subject to a condition, as the shipped row states it.</summary>
public sealed record SpecialSlot(string SlotName, string? Condition);
