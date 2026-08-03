using System.Text.Json.Serialization;

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

    /// <summary>
    /// The slot an item is worn in, spelled as the game spells it: Head, Legs, Neck, Relic.
    /// </summary>
    public string? Slot { get; init; }

    /// <summary>Set on the items that are rolled with a gem socket.</summary>
    public bool? GemSlot { get; init; }

    /// <summary>
    /// The armour type this entry is, for the items that read as a different thing per armour
    /// type. One helm is four items - Redeemer's Petal-Weave Hood in cloth, Redeemer's Bramble
    /// Greathelm in plate - and this is which of them this line is.
    /// </summary>
    public string? ArmorType { get; init; }

    /// <summary>
    /// The attribute this entry's primary stat slot holds. An item declares that slot as
    /// HeroRelativePrimaryStat and the hero decides what goes in it, so the same item is a
    /// Strength item for one hero and an Intellect item for another.
    /// </summary>
    public string? PrimaryStat { get; init; }

    /// <summary>
    /// The shipped names of the heroes this entry is for. A dynamic item is one entry per hero and
    /// names exactly one, because the art it shows is the wearer's own armour set and no two heroes
    /// share one; a fixed item names every hero its row is for. Either way the list is part of what
    /// identifies the line rather than a note on it.
    /// </summary>
    public IReadOnlyList<string>? Heroes { get; init; }

    /// <summary>
    /// The sets an item counts towards, under the names the game prints. Usually one. A dynamic
    /// item's set depends on the hero wearing it and a card is an armour type rather than a hero,
    /// so a card covering heroes the shipped tables send to different sets carries all of them.
    /// </summary>
    public IReadOnlyList<string>? SetBonus { get; init; }

    /// <summary>How many pieces a set bonus needs, on the set bonuses themselves.</summary>
    public int? SetCount { get; init; }

    /// <summary>Set on the items the game allows only one of at a time.</summary>
    public bool? UniqueEquipped { get; init; }

    /// <summary>An ability's classification, from its AbilityType tags: Aoe, Casted, Magic.Fire, Major.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// The stats an item's slots carry, in the order the item lists them, each with the share of
    /// the item's stat budget its slot is worth and which kind of slot that is. The magnitude
    /// itself depends on the item's level and so is not a property of the item.
    /// </summary>
    public IReadOnlyList<StatSlot>? Stats { get; init; }

    /// <summary>
    /// The loot rule set the item is rolled under, without its tag prefix. Which extra slots a
    /// rarity grants the item is stated per rule set, so this is what joins an item to the rarity
    /// table in the vocabulary.
    /// </summary>
    public string? RuleSet { get; init; }

    public string? Description { get; init; }

    /// <summary>Description lines the row carries beyond the description itself.</summary>
    public IReadOnlyList<string>? Detail { get; init; }

    /// <summary>The exported image, by file name, in the same folder as this record.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// The image a dynamic hero item shows at each rarity it drops at, where they are not all the
    /// same image. A hero's armour set ships three rungs of art and the item's rarity picks one,
    /// so one line covers up to three pictures of the same item and <see cref="Icon"/> is only the
    /// best of them. Keyed by the rarity's internal name, which is what
    /// <c>vocabulary.rarities</c> joins on.
    ///
    /// Absent wherever the item looks the same at every rarity it has, which is every legendary -
    /// their collections ship one rung - and every item whose slot has no collection art at all.
    /// </summary>
    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    /// <summary>
    /// The entity this record's <see cref="Icon"/> was taken from, on the records that ship no art
    /// of their own. A gameplay effect never carries an icon and the ability that applies it
    /// usually does, so the effect's tooltip shows the ability's art; this says whose art it is.
    ///
    /// Set only where the inheritance is unambiguous, so its presence is also the audit trail: a
    /// reader who disagrees with the pairing can follow it back to the entity that supplied the
    /// image, and a renderer that wants to draw borrowed art differently has the fact it needs.
    /// Absent on every record whose icon is its own.
    /// </summary>
    public IconSource? IconInheritedFrom { get; init; }

    /// <summary>
    /// The entity this record's <see cref="DisplayName"/> was taken from, on the records that ship
    /// no name of their own. An effect is anonymous in the build and the ability that applies it is
    /// not, so the debuff a player calls Searing Blaze is named by the ability rather than by
    /// itself; this says which entity did the naming.
    ///
    /// Carried rather than implied, because the inheritance puts two entities into the export under
    /// one word, and on 1,288 records it does. Without the join a reader is offered the ability and
    /// the debuff it lands under one name with nothing to tell them apart, and 123 of the borrowed
    /// names are the single word "Attack", so a search index has no way to rank a borrowed name
    /// below the entity it was borrowed from. Set only where the inheritance is unambiguous, so its
    /// presence is also the audit trail back to the entity that supplied the word.
    /// </summary>
    public IconSource? NameInheritedFrom { get; init; }

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

    /// <summary>
    /// The Talent.ID slot a talent occupies. A coordinate rather than a position: the same slot
    /// holds a different talent, at a different place in the grid, in every hero's tree.
    /// </summary>
    public string? TalentSlot { get; init; }

    /// <summary>Where a talent sits in its hero's grid, 1 to 6 down and 1 to 3 across.</summary>
    public int? TalentRow { get; init; }

    public int? TalentColumn { get; init; }

    /// <summary>What a talent costs to take, 1 to 3, as its own row declares it.</summary>
    public int? TalentCost { get; init; }

    /// <summary>
    /// The CRCharacterAugmentationAsset netId carrying this record's mechanics and text, on the
    /// lines whose content lives there rather than on themselves.
    /// </summary>
    public int? Augmentation { get; init; }

    /// <summary>
    /// The attribute a gem type accumulates into. A socketed gem adds its tier's power to this one
    /// attribute, and the type's ten-node ladder is unlocked against that running total, so the
    /// attribute is what ties a bag of gems to the bonuses they buy.
    /// </summary>
    public string? GemAttribute { get; init; }

    /// <summary>What a gem type keeps giving once every node of its ladder is unlocked.</summary>
    public GemOverflow? GemOverflow { get; init; }

    /// <summary>The gem type a tier or a ladder node belongs to: Ruby, Amethyst, Topaz.</summary>
    public string? GemType { get; init; }

    /// <summary>Which of the four shipped sizes a gem is, 1 Small to 4 Flawless.</summary>
    public int? GemTier { get; init; }

    /// <summary>The power a gem of this tier contributes to its type's total when socketed.</summary>
    public int? GemPower { get; init; }

    /// <summary>
    /// The rarity a gem of this tier drops at, on the ladder the loot rule sets name, under the
    /// word a gem prints rather than the word an item of the same ordinal prints.
    /// </summary>
    public string? Rarity { get; init; }

    /// <summary>Where on its type's ten-step ladder a gem bonus sits, 1 to 10.</summary>
    public int? GemNode { get; init; }

    /// <summary>The accumulated power of this node's own gem type that unlocks it.</summary>
    public int? RequiredGemPower { get; init; }

    /// <summary>
    /// The netId of the weaker node this one takes the place of, on the upper half of every gem
    /// ladder. Two nodes of the same family are never both live.
    /// </summary>
    public int? Replaces { get; init; }

    /// <summary>
    /// The category tag an item trait carries. It is what a tree row matches candidates against, so
    /// a trait with none can be rolled by nothing: two of the forty are in that state.
    /// </summary>
    public string? TraitCategoryTag { get; init; }

    /// <summary>The category under the name the game prints for it. The Major tag ships as Master.</summary>
    public string? TraitCategory { get; init; }

    /// <summary>How far a trait upgrades: 4 on every weapon trait, 1 on every necklace trait.</summary>
    public int? MaxRank { get; init; }

    /// <summary>The weight a trait is rolled at against the others in its category.</summary>
    public int? RandomChance { get; init; }

    /// <summary>
    /// The rank progressions a trait's description prints, each bound to the shipped curve behind
    /// it. The description contains no tokens - the slash list is characters a designer typed - so
    /// this is the only thing connecting the printed digits to the numbers the game uses.
    /// </summary>
    public IReadOnlyList<RankRun>? RankRuns { get; init; }

    /// <summary>
    /// The roles a set bonus is offered to, where it is offered to some of them. Sin Warding ships
    /// as three rows gated Tank, Healer and Dps, so the gate is part of what identifies the row.
    /// </summary>
    public IReadOnlyList<string>? AllowedRoles { get; init; }

    /// <summary>The primary attributes a set bonus is offered to, on the rows that name any.</summary>
    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    /// <summary>The ability an item grants, as its own tooltip.</summary>
    public Entity? Grants { get; init; }

    /// <summary>A legendary's bonus stats, each with its own name and line.</summary>
    public IReadOnlyList<Entity>? LegendaryStats { get; init; }

    /// <summary>
    /// What an item id reads as per hero, while it is being worked out. Nothing exports it: the
    /// expansion turns these into entries of their own, so a reader sees named items rather than
    /// one item with a list of readings nested inside it.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Variant>? Variants { get; init; }
}

/// <summary>
/// An entity, addressed the way a reader would ask for it again. The netId alone is not an address
/// because ids are per table, and the tag name is carried beside it so the pairing can be judged
/// without a second lookup.
/// </summary>
public sealed record IconSource(string Table, int NetId, string Name);

/// <summary>
/// What a gem type gives past the top of its ladder, where further power would otherwise buy
/// nothing. Its own record because it is a second sentence about the type rather than the type's
/// description: a gem type has no description of its own at all.
/// </summary>
public sealed record GemOverflow(string Description)
{
    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    public string? Icon { get; init; }
}

/// <summary>
/// One four-value progression printed in a trait's description, matched to the shipped curve row
/// that produces it.
///
/// <see cref="Literal"/> is the exact substring of the description covering the whole run including
/// its markup, so printing one rank is <c>description.Replace(run.Literal, run.Printed[rank - 1])</c>
/// and needs no parsing contract. <see cref="Printed"/> is authoritative for display and
/// <see cref="Values"/> for arithmetic, and they are separate because in one shipped case they
/// disagree.
/// </summary>
public sealed record RankRun(string Literal, IReadOnlyList<string> Printed)
{
    /// <summary>The four shipped curve values behind the run, in rank order.</summary>
    public IReadOnlyList<double?>? Values { get; init; }

    /// <summary>The tuning row the values come from, under its full shipped name.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// The arithmetic that turns a value into its printed number: raw is a fraction as a percent,
    /// delta is a multiplier's excess over 1, reduction is its shortfall, plain is the number
    /// itself, and ticks is a per-tick value totalled over <see cref="Ticks"/> of them.
    /// </summary>
    public string? Transform { get; init; }

    /// <summary>How many ticks a ticks-transformed run totals over.</summary>
    public int? Ticks { get; init; }

    /// <summary>Set where a printed value and its curve value do not agree. One shipped case.</summary>
    public bool? Disagrees { get; init; }
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
/// unambiguously the token is self-sufficient: the mean the game rolls around is
/// <c>Coefficient x Attribute</c>, and <see cref="Spread"/> is how wide the band around it is. So
/// Fire Ball's {FireBallDirect} at 100 Intellect is 5.8482 x 100 = 585 at spread 0.2, shown as 526
/// to 643.
/// </summary>
public sealed record Placeholder
{
    /// <summary>The attribute the coefficient multiplies, named by the tuning row itself.</summary>
    public string? Attribute { get; init; }

    public double? Coefficient { get; init; }

    /// <summary>
    /// The width of the band the number rolls in, as a share of the mean. It is the total width
    /// rather than a plus or minus, so the printed range is
    /// <c>mean x (1 - spread / 2)</c> to <c>mean x (1 + spread / 2)</c>: 0.2 is 90 to 110 percent
    /// and 0.4 is 80 to 120. Zero is a rolled band of nothing, meaning the number is exact, and is
    /// a different fact from the field being absent, which means nothing in the build says.
    ///
    /// Only ever set beside a coefficient. A period or a target count is not rolled.
    /// </summary>
    public double? Spread { get; init; }

    /// <summary>
    /// The number the token prints outright, for the tokens that scale off nothing: a period, a
    /// duration, a delay, a radius, a target count. 53 of the resolved tokens read this way.
    /// </summary>
    public double? Value { get; init; }

    /// <summary>
    /// The word the token prints, for the tokens that stand in for a name rather than a number. A
    /// gem type's overflow sentence writes "{typename} Gem Essence" and the struct holding the
    /// sentence names the type beside it, so the substitution is text and has no arithmetic at all.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Set when the token declares its number is shown as a share rather than as itself. The game
    /// says so itself, on 24 of the 225 tokens censused, and every sentence carrying one reads as a
    /// share: "{Rend} of the damage dealt", "{BloodboundBuff} increased damage", "{VoidTouchAccum}
    /// of all damage you deal".
    /// </summary>
    public bool? Percent { get; init; }

    /// <summary>
    /// How a gem or set bonus argument's number is printed: number for the value as it stands,
    /// percent for a fraction shown as one, delta for a multiplier shown as its signed excess over
    /// 1. Not folded into <see cref="Percent"/>, which comes from an ability token's ShowPercent
    /// flag on a different struct: percent and delta both print a percent sign and reach it by
    /// different arithmetic, and a bool cannot say which.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// What the number is measured in, for the tokens whose sentence leaves the word out: "healing
    /// you every {BloodRushPeriod}". Only seconds is establishable and it is the only unit the
    /// build asks for, because every token declaring one carries a Calculation naming a period or
    /// a duration.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Set when the token asks for the damage school to be spoken as part of the number rather than
    /// left to the surrounding sentence. It is what picks between the shipped print formats named
    /// Damage and DamageMagic, which the vocabulary file carries: without it a renderer choosing
    /// DamageRangeMagic would write "fire damage magic damage" on the sentences that already say it.
    ///
    /// True on 10 of the 225 tokens censused, and the flag alone does not select the format. 8 of
    /// the 10 carry a coefficient and are damage; the other two are Reaver's Edge and Blood Arc's
    /// {Radius}, where the flag is set on a distance. A school variant applies only where this and
    /// a <see cref="Coefficient"/> are both present.
    /// </summary>
    public bool? ShowDamageType { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    /// <summary>
    /// Set when the token names the entity that owns it rather than a gameplay effect. Native code
    /// works the number out from the entity's own tuning, which is in its constants; the data does
    /// not say which rows. Distinguishes that from a token that failed to resolve.
    /// </summary>
    public bool? FromOwnConstants { get; init; }

    /// <summary>
    /// The quantity the token's Calculation ordinal asks for. Not exported: it says which of the
    /// entity's rows can answer the token, which the resolver has already acted on by the time
    /// anything reads the record.
    /// </summary>
    [JsonIgnore]
    public TokenQuantity Quantity { get; init; }

    /// <summary>
    /// The band the gameplay effect this token names rolls in. Not exported: it is the fallback
    /// behind <see cref="Spread"/>, and once the ability's own override has had its say the
    /// resolved band is the only one worth carrying.
    /// </summary>
    [JsonIgnore]
    public double? PresetSpread { get; init; }
}
