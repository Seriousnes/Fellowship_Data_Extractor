namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One thing the game shows a tooltip for, reduced to what the tooltip needs and nothing else.
///
/// A reference to another asset is never carried as a path, because a class name reconstructs
/// nothing. Either the number or the text behind the reference is read and folded in here, or the
/// reference is left out. The same rule drops the cooldown, charge and global-cooldown effect
/// classes every ability names: the cooldown itself is already a number on the ability.
///
/// Every field is optional because entities differ. An item has stats, an ability has a cast time,
/// a relic has both and a granted power besides.
/// </summary>
public sealed record Entity
{
    /// <summary>Identity. Set on a top-level entity and absent from anything nested inside one.</summary>
    public string? Table { get; init; }

    public int? NetId { get; init; }
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>An item's type line: Relic, Legendary, Trinket.</summary>
    public string? Type { get; init; }

    /// <summary>An ability's classification, from its AbilityType tags: Aoe, Casted, Magic.Fire, Major.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>The stats an item's slots carry, in the order the item lists them.</summary>
    public IReadOnlyList<string>? Stats { get; init; }

    public string? Description { get; init; }

    /// <summary>Description lines the row carries beyond the description itself.</summary>
    public IReadOnlyList<string>? Detail { get; init; }

    /// <summary>The exported image, by file name, in the same folder as this record.</summary>
    public string? Icon { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Charges { get; init; }
    public double? Cost { get; init; }

    /// <summary>What the cost is paid in: Mana, Spirit, Fury, Burning Ember.</summary>
    public string? Resource { get; init; }

    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    /// <summary>The attributes this entity's damage, healing or buffs scale off.</summary>
    public IReadOnlyList<string>? ScalesOff { get; init; }

    /// <summary>
    /// The {Token} substitutions the description contains, each carrying the attributes and
    /// coefficients that produce its number.
    /// </summary>
    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    /// <summary>
    /// Every tuning number this entity reaches that is not already a named field above and is not
    /// gated behind a talent, keyed by its row in the shipped tables with the entity's own
    /// namespace stripped. A value is one number, four numbers for a row that scales with rank, or
    /// null when the asset names a row the shipped tables do not define.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    /// <summary>
    /// Tuning rows whose names name a talent, grouped by that talent. The name is all this says:
    /// a row named for a talent is very often unconditional, because a talent folded into its
    /// ability leaves its name behind in the row. Ten abilities carry a charge count only in a row
    /// like this, which is why they are grouped rather than left in a flat list.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    /// <summary>
    /// The gameplay tag an entity requires before a talent group applies, for the talents that
    /// declare one. Absence means the asset declares no condition, not that none exists. Cross
    /// against a <see cref="GrantedTags"/> somewhere in the export to see whether anything in the
    /// build grants it: ten declared gates are granted by nothing at all.
    /// </summary>
    public IReadOnlyDictionary<string, string>? TalentGates { get; init; }

    /// <summary>
    /// The gameplay tags this entity grants. A talent's entire content is this list, so without it
    /// a talent exports as a bare name and the chain from a gate back to the talent that opens it
    /// cannot be followed.
    /// </summary>
    public IReadOnlyList<string>? GrantedTags { get; init; }

    /// <summary>The ability an item grants, as its own tooltip.</summary>
    public Entity? Grants { get; init; }

    /// <summary>A legendary's bonus stats, each with its own name and line.</summary>
    public IReadOnlyList<Entity>? LegendaryStats { get; init; }

    /// <summary>Set when one item id reads differently per hero.</summary>
    public IReadOnlyList<Variant>? Variants { get; init; }
}

/// <summary>One hero's reading of an item that differs per hero.</summary>
public sealed record Variant(string Hero)
{
    public string? HeroName { get; init; }
    public string? DisplayName { get; set; }
    public Entity? Grants { get; set; }
}

/// <summary>
/// What a {Token} in a description resolves to. Where the data identifies one coefficient
/// unambiguously the token is self-sufficient: the number the game prints is
/// <c>Coefficient x Attribute</c>, displayed as a range of plus or minus a tenth around it. So
/// Fire Ball's {FireBallDirect} at 100 Intellect is 5.8482 x 100 = 585, shown as 526 to 643.
/// </summary>
public sealed record Placeholder
{
    /// <summary>The attribute the coefficient multiplies, named by the tuning row itself.</summary>
    public string? Attribute { get; init; }

    public double? Coefficient { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    /// <summary>
    /// Set when the token names the entity that owns it rather than a gameplay effect. Native code
    /// works the number out from the entity's own tuning, which is in its constants; the data does
    /// not say which rows. Distinguishes that from a token that failed to resolve.
    /// </summary>
    public bool? FromOwnConstants { get; init; }
}
