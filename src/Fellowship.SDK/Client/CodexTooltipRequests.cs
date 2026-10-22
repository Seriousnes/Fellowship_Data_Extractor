using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Fellowship.SDK.Client;

/// <summary>Rolls for one stat: its ratings, its percentages, or both.</summary>
public sealed record StatRolls
{
    /// <summary>The stat's rating, one number per roll.</summary>
    [Description("The stat's rating, one number per roll.")]
    public IReadOnlyList<decimal>? Rating { get; init; }

    /// <summary>
    /// The stat's percentage, one number per roll, for a rating that converts to a percentage.
    /// </summary>
    [Description(
        "The stat's percentage, one number per roll, for a rating that converts to a percentage.")]
    public IReadOnlyList<decimal>? Percentage { get; init; }
}

/// <summary>Tooltip for an ability.</summary>
public sealed record AbilityTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Ability;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
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
    /// Each stat's magnitude, by the name the build gives the stat. A stat rolled more times than the
    /// item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "Each stat's magnitude, by the name the build gives the stat, to state a magnitude the tooltip " +
        "prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>Tooltip for a blessing.</summary>
public sealed record BlessingTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Blessing;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    /// <summary>Which of a blessing's tiers is equipped.</summary>
    [Description("Which of a blessing's tiers is equipped. None stated reads as none equipped.")]
    public int? EquippedTier { get; init; }
}

/// <summary>Tooltip for a companion pet.</summary>
public sealed record CompanionPetTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.CompanionPet;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for a creature.</summary>
public sealed record CreatureTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Creature;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
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

/// <summary>Tooltip for a currency.</summary>
public sealed record CurrencyTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Currency;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
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

/// <summary>Tooltip for a dungeon.</summary>
public sealed record DungeonTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Dungeon;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for an effect.</summary>
public sealed record EffectTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Effect;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    /// <summary>
    /// Each stat's magnitude, by the name the build gives the stat. A stat rolled more times than the
    /// item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "Each stat's magnitude, by the name the build gives the stat, to state a magnitude the tooltip " +
        "prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>Tooltip for a gem.</summary>
public sealed record GemTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Gem;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
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

/// <summary>Tooltip for a gem trait.</summary>
public sealed record GemTraitTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.GemTrait;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for an item.</summary>
public sealed record ItemTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Item;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    /// <summary>Hero to resolve the id against. One id resolves to a different entity per hero.</summary>
    [Description("Hero to resolve the id against. One id resolves to a different entity per hero.")]
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

    /// <summary>The item's modifiers.</summary>
    [Description(
        "The item's modifiers, each a kind, an id and an amount. The kinds are trait, blessing " +
        "and essence.")]
    public IReadOnlyList<ItemModifier>? Modifiers { get; init; }

    /// <summary>The traits imbued into the item.</summary>
    [Description("The traits imbued into the item, each an id and a rank.")]
    public IReadOnlyList<ImbuedTrait>? ImbuedTraits { get; init; }

    /// <summary>How many imbued trait slots are filled.</summary>
    [Description("How many imbued trait slots are filled.")]
    public int? AllocatedTraits { get; init; }

    /// <summary>Draw the imbued traits line whatever slot the item is in.</summary>
    [Description("True when the imbued traits line is to be drawn whatever slot the item is in.")]
    public bool? HasImbuedTraits { get; init; }

    /// <summary>
    /// Each stat's magnitude, by the name the build gives the stat. A stat rolled more times than the
    /// item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "Each stat's magnitude, by the name the build gives the stat, to state a magnitude the tooltip " +
        "prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

public sealed record RelicPassiveTooltipRequest
{
    public static EntityType Type => EntityType.RelicPassive;

    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for a legendary power.</summary>
public sealed record LegendaryPowerTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.LegendaryPower;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for a mount.</summary>
public sealed record MountTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Mount;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for a relic power.</summary>
public sealed record RelicPowerTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.RelicPower;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    /// <summary>
    /// Each stat's magnitude, by the name the build gives the stat. A stat rolled more times than the
    /// item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "Each stat's magnitude, by the name the build gives the stat, to state a magnitude the tooltip " +
        "prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>Tooltip for a talent.</summary>
public sealed record TalentTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Talent;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for a trait.</summary>
public sealed record TraitTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Trait;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
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

/// <summary>Tooltip for a trait power.</summary>
public sealed record TraitPowerTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.TraitPower;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }
}

/// <summary>Tooltip for a weapon power.</summary>
public sealed record WeaponPowerTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.WeaponPower;

    /// <summary>Draw the entity's texture beside the card. A card that draws its texture inside ignores it.</summary>
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
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
    /// Each stat's magnitude, by the name the build gives the stat. A stat rolled more times than the
    /// item declares slots for it fills a rolled slot with each magnitude stated.
    /// </summary>
    [Description(
        "Each stat's magnitude, by the name the build gives the stat, to state a magnitude the tooltip " +
        "prints rather than computes.")]
    public IReadOnlyDictionary<string, StatRolls>? Stats { get; init; }
}

/// <summary>
/// The request record each entity type's tooltip takes, where that type draws one. Each record is
/// named for its entity type, and that name is the join in both directions.
/// </summary>
public static class TooltipRequestTypes
{
    private const string Suffix = "TooltipRequest";

    private const string TrimWarning =
        "A request is read into a type named at run time, so trimming can take that type away.";

    private static readonly ConcurrentDictionary<Type, EntityType> ByRequest = new();

    /// <summary>
    /// The record naming what <paramref name="type"/>'s tooltip is drawn for, or null where the type
    /// draws no tooltip.
    /// </summary>
    [RequiresUnreferencedCode(TrimWarning)]
    public static Type? Find(EntityType type) =>
        typeof(TooltipRequestTypes).Assembly.GetType(
            $"{typeof(TooltipRequestTypes).Namespace}.{type}{Suffix}");

    /// <summary>The entity type whose tooltip <typeparamref name="TRequest"/> is drawn for.</summary>
    public static EntityType TypeOf<TRequest>() where TRequest : class =>
        ByRequest.GetOrAdd(typeof(TRequest), Resolve);

    private static EntityType Resolve(Type request)
    {
        var name = request.Name;
        var member = name.EndsWith(Suffix, StringComparison.Ordinal) ? name[..^Suffix.Length] : name;

        return Enum.TryParse<EntityType>(member, out var type)
            ? type
            : throw new InvalidOperationException($"{name} names no {nameof(EntityType)}.");
    }
}
