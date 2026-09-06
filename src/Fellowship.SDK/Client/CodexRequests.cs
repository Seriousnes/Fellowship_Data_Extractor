using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Fellowship.SDK.Client;

public sealed record EntityFilter
{
    public string? Q { get; init; }

    public IReadOnlyList<EntityType>? Type { get; init; }

    public string? Sort { get; init; }

    public string? Dir { get; init; }

    public string? Group { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    public IReadOnlyDictionary<string, string>? Facets { get; init; }
}

/// <summary>What one stat rolled: its ratings, its percentages, or both.</summary>
public sealed record StatRolls
{
    /// <summary>The rating the stat stands at, one number per roll.</summary>
    [Description("The rating the stat stands at, one number per roll.")]
    public IReadOnlyList<decimal>? Rating { get; init; }

    /// <summary>
    /// The percentage the stat stands at, one number per roll, for a rating that converts to a percentage.
    /// </summary>
    [Description(
        "The percentage the stat stands at, one number per roll, for a rating that converts to a percentage.")]
    public IReadOnlyList<decimal>? Percentage { get; init; }
}

/// <summary>
/// What one tooltip is to be drawn for. Every tooltip takes <see cref="ShowIcon"/> and
/// <see cref="Mobile"/>; the rest are read by the tooltips they mean something to, and an entity
/// that has no use for one ignores it.
/// </summary>
public sealed record TooltipRequest
{
    /// <summary>Draw the entity's art beside the card. A card that draws its art inside ignores it.</summary>
    [Description(
        "True when the entity's art is to be drawn beside the card. A card that draws its art inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    /// <summary>The hero whose reading of the id to draw, where one id reads differently per hero.</summary>
    [Description(
        "The hero whose reading of the id to draw, where one id reads differently per hero, and whose stat " +
        "a PrimaryAttribute coefficient scales off.")]
    public string? Hero { get; init; }

    /// <summary>The dungeon difficulty to compute a creature ability's damage at.</summary>
    [Description(
        "The dungeon difficulty to compute a creature ability's damage at, and to read a creature's health at.")]
    public int? Difficulty { get; init; }

    /// <summary>The state a blessing or trait card is drawn in.</summary>
    [Description(
        "The state to draw the row or node at: allocated or unallocated for a blessing, selected, available " +
        "or unavailable for a trait.")]
    public string? State { get; init; }

    /// <summary>Which of a blessing's tiers is equipped.</summary>
    [Description("Which of a blessing's tiers is equipped. None stated reads as none equipped.")]
    public int? EquippedTier { get; init; }

    /// <summary>The rarity to read at, by the name the game prints or by tier.</summary>
    [Description("The rarity to read at, by the name the game prints or by tier.")]
    public string? Rarity { get; init; }

    /// <summary>The item level to compute stat magnitudes at.</summary>
    [Range(0, 10_000)]
    [Description("The item level to compute stat magnitudes at, 0 to 10,000.")]
    public int? ItemLevel { get; init; }

    /// <summary>How many times the item has been tempered.</summary>
    [Description("How many times the item has been tempered.")]
    public int? Upgrades { get; init; }

    /// <summary>The tempering ceiling to print in place of the one the shipped ladder states.</summary>
    [Description(
        "The tempering ceiling to print in place of the one the shipped ladder states for this rarity and level.")]
    public int? MaxUpgrades { get; init; }

    /// <summary>Read the item's gem socket as empty.</summary>
    [Description("True when the item's gem socket is to read as empty.")]
    public bool? NoSocket { get; init; }

    /// <summary>The gem id filling the item's socket.</summary>
    [Description("The gem id filling the item's socket.")]
    public int? Gem { get; init; }

    /// <summary>The power potential to print in place of the one the tooltip computes.</summary>
    [Description(
        "The power potential to print in place of the one the tooltip computes from the item level, the " +
        "tempering ceiling and the slots the rarity adds.")]
    public decimal? PotentialPower { get; init; }

    /// <summary>The armour to print in place of the one the tooltip computes from the item level.</summary>
    [Description("The armour to print in place of the one the tooltip computes from the item level.")]
    public decimal? Armor { get; init; }

    /// <summary>
    /// The stats a random slot rolled, named once per roll and without a magnitude, which the tooltip
    /// computes. A caller holding the magnitudes states them in <see cref="Stats"/> instead.
    /// </summary>
    [Description(
        "The stats a random slot rolled, named once per roll and without a magnitude, which the tooltip " +
        "computes. Rolling a stat the item already declares aggregates into that line.")]
    public IReadOnlyList<string>? RandomStats { get; init; }

    /// <summary>The modifiers the item carries.</summary>
    [Description(
        "The modifiers the item carries, each a kind, an id and an amount. The kinds are trait, blessing " +
        "and essence.")]
    public IReadOnlyList<ItemModifier>? Modifiers { get; init; }

    /// <summary>The traits imbued into the item.</summary>
    [Description("The traits imbued into the item, each an id and the rank it sits at.")]
    public IReadOnlyList<ImbuedTrait>? ImbuedTraits { get; init; }

    /// <summary>How many imbued trait slots are filled.</summary>
    [Description("How many imbued trait slots are filled.")]
    public int? AllocatedTraits { get; init; }

    /// <summary>Draw the imbued traits line whatever slot the item sits in.</summary>
    [Description("True when the imbued traits line is to be drawn whatever slot the item sits in.")]
    public bool? HasImbuedTraits { get; init; }

    /// <summary>
    /// The magnitude each stat stands at, by the name the build gives the stat. A stat rolled more
    /// times than the item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "The magnitude each stat stands at, by the name the build gives the stat, to state a magnitude the " +
        "tooltip prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}
