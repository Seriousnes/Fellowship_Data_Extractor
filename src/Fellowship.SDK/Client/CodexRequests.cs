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

/// <summary>
/// What one tooltip is to be drawn for. Every tooltip takes <see cref="ShowIcon"/> and
/// <see cref="Mobile"/>; the rest are read by the tooltips they mean something to, and an entity
/// that has no use for one ignores it.
/// </summary>
public sealed record TooltipRequest
{
    /// <summary>Draw the entity's art beside the card. A card that draws its art inside ignores it.</summary>
    public bool? ShowIcon { get; init; }

    /// <summary>
    /// Wrap the card in the chrome a phone draws it in: a scrim, a close button and a link to its page.
    /// </summary>
    public bool? Mobile { get; init; }

    /// <summary>The hero whose reading of the id to draw, where one id reads differently per hero.</summary>
    public string? Hero { get; init; }

    /// <summary>The dungeon difficulty to compute a creature ability's damage at.</summary>
    public int? Difficulty { get; init; }

    /// <summary>The state a blessing or trait card is drawn in.</summary>
    public string? State { get; init; }

    /// <summary>The rung of the item a blessing is slotted into.</summary>
    public int? EquippedTier { get; init; }

    /// <summary>The rung to read at, by the name the game prints or by tier.</summary>
    public string? Rarity { get; init; }

    /// <summary>The item level to compute stat magnitudes at.</summary>
    public int? ItemLevel { get; init; }

    /// <summary>How many times the item has been tempered.</summary>
    public int? Upgrades { get; init; }

    /// <summary>The tempering ceiling to print in place of the one the shipped ladder states.</summary>
    public int? MaxUpgrades { get; init; }

    /// <summary>Read the item's gem socket as empty.</summary>
    public bool? NoSocket { get; init; }

    /// <summary>The gem id filling the item's socket.</summary>
    public int? Gem { get; init; }

    /// <summary>The power potential to print in place of the one the tooltip computes.</summary>
    public decimal? PotentialPower { get; init; }

    /// <summary>The armour to print in place of the one the tooltip computes from the item level.</summary>
    public decimal? Armor { get; init; }

    /// <summary>
    /// The stats a random slot rolled, named once per roll and without a magnitude, which the tooltip
    /// computes. A caller holding the magnitudes states them in <see cref="Stats"/> instead.
    /// </summary>
    public IReadOnlyList<string>? RandomStats { get; init; }

    /// <summary>The modifiers the item carries.</summary>
    public IReadOnlyList<ItemModifier>? Modifiers { get; init; }

    /// <summary>The traits imbued into the item.</summary>
    public IReadOnlyList<ImbuedTrait>? ImbuedTraits { get; init; }

    /// <summary>How many imbued trait slots are filled.</summary>
    public int? AllocatedTraits { get; init; }

    /// <summary>Draw the imbued traits line whatever slot the item sits in.</summary>
    public bool? HasImbuedTraits { get; init; }

    /// <summary>
    /// The magnitude each stat stands at, by the name the codex files the stat under. A stat named
    /// more times than the item declares slots for it fills a rolled slot with the magnitude given.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<decimal>>? Stats { get; init; }

    /// <summary>Anything else the address is to carry, written as given.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
