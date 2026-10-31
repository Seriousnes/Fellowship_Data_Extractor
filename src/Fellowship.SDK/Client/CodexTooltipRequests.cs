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

    [Description(
        "The talents and legendary powers whose changes the tooltip applies, each a type and an id, with the " +
        "effects held while a change applies after a '~', such as talent:38~effect:1376.")]
    public IReadOnlyList<ChangeSource>? ChangedBy { get; init; }
}

/// <summary>Tooltip for a glyph.</summary>
public sealed record GlyphTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.Glyph;

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

    /// <summary>Which of a glyph's tiers is equipped.</summary>
    [Description("Which of a glyph's tiers is equipped. None stated reads as none equipped.")]
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

    [Description("Hero to resolve the id against. One id resolves to a different entity per hero.")]
    public string? Hero { get; init; }

    [Description(
        "The talents and legendary powers whose changes the tooltip applies, each a type and an id, with the " +
        "effects held while a change applies after a '~', such as talent:38~effect:1376.")]
    public IReadOnlyList<ChangeSource>? ChangedBy { get; init; }
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

/// <summary>Tooltip for a gem ability.</summary>
public sealed record GemAbilityTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.GemAbility;

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

public sealed record EnchantTooltipRequest
{
    public static EntityType Type => EntityType.Enchant;

    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    [Description("Hero to resolve the id against. One id resolves to a different entity per hero.")]
    public string? Hero { get; init; }

    [Description("The state to draw the node at: selected, available or unavailable.")]
    public string? State { get; init; }

    [Range(1, 10_000)]
    [Description(
        "The item level of each item carrying the enchant. It sets the ranks each rank source adds. None stated " +
        "reads as the highest item level.")]
    public int? ItemLevel { get; init; }

    [Description(
        "The gear slots of the equipped items carrying the enchant, comma separated, each an item type such as " +
        "Head or Necklace. Each adds the ranks one item at the item level adds. None stated reads as one " +
        "item in the first gear slot that can carry it.")]
    public string? RankSources { get; init; }
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
        "The item's modifiers, each a kind, an id and an amount. The kinds are the build's modifier slot " +
        "names in lower case, such as statenchant, abilityrank and gempower.")]
    public IReadOnlyList<ItemModifier>? Modifiers { get; init; }

    /// <summary>The augmentations attuned in the item's attunement tree.</summary>
    [Description("The augmentations attuned in the item's attunement tree, each an id and a rank.")]
    public IReadOnlyList<Attunement>? Attunements { get; init; }

    /// <summary>How many attunement tree slots are filled.</summary>
    [Description("How many attunement tree slots are filled.")]
    public int? AllocatedAttunements { get; init; }

    /// <summary>Draw the attunements line whatever slot the item is in.</summary>
    [Description("True when the attunements line is to be drawn whatever slot the item is in.")]
    public bool? HasAttunements { get; init; }

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

/// <summary>Tooltip for a dungeon affix.</summary>
public sealed record DungeonAffixTooltipRequest
{
    /// <summary>The entity type whose tooltip this request draws.</summary>
    public static EntityType Type => EntityType.DungeonAffix;

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

public sealed record SigilTooltipRequest
{
    public static EntityType Type => EntityType.Sigil;

    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    [Description("The state to draw the node at: selected, available or unavailable.")]
    public string? State { get; init; }

    [Description(
        "The gear slots of the equipped items carrying the sigil, comma separated, each an item type such as " +
        "Head or Necklace. Each adds one rank. None stated reads as one " +
        "item in the first gear slot that can carry it.")]
    public string? RankSources { get; init; }
}

public sealed record AspectTooltipRequest
{
    public static EntityType Type => EntityType.Aspect;

    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    [Description("The state to draw the node at: selected, available or unavailable.")]
    public string? State { get; init; }

    [Description(
        "The gear slots of the equipped items carrying the aspect, comma separated, each an item type such as " +
        "Head or Necklace. Each adds one rank. None stated reads as one " +
        "item in the first gear slot that can carry it.")]
    public string? RankSources { get; init; }
}

public sealed record ProtectiveEnchantTooltipRequest
{
    public static EntityType Type => EntityType.ProtectiveEnchant;

    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    [Description("The state to draw the node at: selected, available or unavailable.")]
    public string? State { get; init; }

    [Description(
        "The gear slots of the equipped items carrying the protective enchant, comma separated, each an item type such as " +
        "Head or Necklace. Each adds one rank. None stated reads as one " +
        "item in the first gear slot that can carry it.")]
    public string? RankSources { get; init; }
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
