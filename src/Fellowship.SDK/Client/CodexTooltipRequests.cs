using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Fellowship.SDK.Client;

/// <summary>
/// What one type's tooltip is to be drawn for. Each entity type declares its own request, carrying
/// only what that type's card reads, so a parameter one card has no use for cannot be sent to it.
/// </summary>
public interface ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    static abstract EntityType Type { get; }
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

/// <summary>What one ability's tooltip is to be drawn for.</summary>
public sealed record AbilityTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Ability;

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

    /// <summary>The hero whose stat a PrimaryAttribute coefficient scales off.</summary>
    [Description("The hero whose stat a PrimaryAttribute coefficient scales off.")]
    public string? Hero { get; init; }

    /// <summary>The dungeon difficulty to compute a creature ability's damage at.</summary>
    [Description("The dungeon difficulty to compute a creature ability's damage at.")]
    public int? Difficulty { get; init; }

    /// <summary>
    /// The magnitude each stat stands at, by the name the build gives the stat. A stat rolled more
    /// times than the item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "The magnitude each stat stands at, by the name the build gives the stat, to state a magnitude the " +
        "tooltip prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>What one blessing's tooltip is to be drawn for.</summary>
public sealed record BlessingTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Blessing;

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

    /// <summary>The state to draw the row at: allocated or unallocated.</summary>
    [Description("The state to draw the row at: allocated or unallocated.")]
    public string? State { get; init; }

    /// <summary>Which of a blessing's tiers is equipped.</summary>
    [Description("Which of a blessing's tiers is equipped. None stated reads as none equipped.")]
    public int? EquippedTier { get; init; }
}

/// <summary>What one companion pet's tooltip is to be drawn for.</summary>
public sealed record CompanionPetTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.CompanionPet;

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
}

/// <summary>What one creature's tooltip is to be drawn for.</summary>
public sealed record CreatureTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Creature;

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

    /// <summary>The dungeon difficulty to read the creature's health at.</summary>
    [Description("The dungeon difficulty to read the creature's health at.")]
    public int? Difficulty { get; init; }
}

/// <summary>What one currency's tooltip is to be drawn for.</summary>
public sealed record CurrencyTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Currency;

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

    /// <summary>The rarity to read at, by the name the game prints or by tier.</summary>
    [Description("The rarity to read at, by the name the game prints or by tier.")]
    public string? Rarity { get; init; }
}

/// <summary>What one dungeon's tooltip is to be drawn for.</summary>
public sealed record DungeonTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Dungeon;

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
}

/// <summary>What one effect's tooltip is to be drawn for.</summary>
public sealed record EffectTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Effect;

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

    /// <summary>
    /// The magnitude each stat stands at, by the name the build gives the stat. A stat rolled more
    /// times than the item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "The magnitude each stat stands at, by the name the build gives the stat, to state a magnitude the " +
        "tooltip prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>What one gem's tooltip is to be drawn for.</summary>
public sealed record GemTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Gem;

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

    /// <summary>The rarity to read at, by the name the game prints or by tier.</summary>
    [Description("The rarity to read at, by the name the game prints or by tier.")]
    public string? Rarity { get; init; }
}

/// <summary>What one gem trait's tooltip is to be drawn for.</summary>
public sealed record GemTraitTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.GemTrait;

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
}

/// <summary>What one item's tooltip is to be drawn for.</summary>
public sealed record ItemTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Item;

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
    [Description("The hero whose reading of the id to draw, where one id reads differently per hero.")]
    public string? Hero { get; init; }

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

/// <summary>What one legendary power's tooltip is to be drawn for.</summary>
public sealed record LegendaryPowerTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.LegendaryPower;

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
}

/// <summary>What one mount's tooltip is to be drawn for.</summary>
public sealed record MountTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Mount;

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
}

/// <summary>What one relic power's tooltip is to be drawn for.</summary>
public sealed record RelicPowerTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.RelicPower;

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

    /// <summary>
    /// The magnitude each stat stands at, by the name the build gives the stat. A stat rolled more
    /// times than the item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "The magnitude each stat stands at, by the name the build gives the stat, to state a magnitude the " +
        "tooltip prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>What one set bonus's tooltip is to be drawn for.</summary>
public sealed record SetBonusTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.SetBonus;

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
}

/// <summary>What one set bonus power's tooltip is to be drawn for.</summary>
public sealed record SetBonusPowerTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.SetBonusPower;

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
}

/// <summary>What one talent's tooltip is to be drawn for.</summary>
public sealed record TalentTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Talent;

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
}

/// <summary>What one trait's tooltip is to be drawn for.</summary>
public sealed record TraitTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Trait;

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

    /// <summary>The state to draw the node at: selected, available or unavailable.</summary>
    [Description("The state to draw the node at: selected, available or unavailable.")]
    public string? State { get; init; }
}

/// <summary>What one trait power's tooltip is to be drawn for.</summary>
public sealed record TraitPowerTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.TraitPower;

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
}

/// <summary>What one weapon power's tooltip is to be drawn for.</summary>
public sealed record WeaponPowerTooltipRequest : ITooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.WeaponPower;

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

    /// <summary>The hero whose stat a PrimaryAttribute coefficient scales off.</summary>
    [Description("The hero whose stat a PrimaryAttribute coefficient scales off.")]
    public string? Hero { get; init; }

    /// <summary>
    /// The magnitude each stat stands at, by the name the build gives the stat. A stat rolled more
    /// times than the item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "The magnitude each stat stands at, by the name the build gives the stat, to state a magnitude the " +
        "tooltip prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>The request record each entity type's tooltip takes, where that type draws one.</summary>
public static class TooltipRequestTypes
{
    private const string Suffix = "TooltipRequest";

    /// <summary>
    /// The record naming what <paramref name="type"/>'s tooltip is drawn for, or null where the type
    /// draws no tooltip.
    /// </summary>
    [RequiresUnreferencedCode(
        "A request is read into a type named at run time, so trimming can take that type away.")]
    public static Type? Find(EntityType type) =>
        typeof(ITooltipRequest).Assembly.GetType($"{typeof(ITooltipRequest).Namespace}.{type}{Suffix}");
}
