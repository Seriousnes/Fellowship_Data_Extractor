using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api;

/// <summary>
/// One entity type as a reader meets it: the route segment that names it, the heading a collection
/// of them is drawn under, the word for a single one, and the menu column it is filed in.
///
/// The slug is <see cref="EntityTypes.Slug"/> of the type, and it is the only spelling a route or a
/// query value accepts: <c>/api/tooltip/talent/124</c> is the address of a talent, spelled the way
/// the type's own name spells it and not the way its label prints.
/// </summary>
public sealed record PublicType(Fellowship.SDK.EntityType Type, string Slug, string Label, string Noun, string Group);

/// <summary>
/// Every entity type a reader meets, and the only place their label, noun and menu column are
/// written down.
///
/// The drop rules the extractor applies already keep a row a reader cannot meet out of the export,
/// so every <see cref="Fellowship.SDK.EntityType"/> is public: there is no second test here deciding
/// whether a type reaches a reader, only how it is spelled once it does.
/// </summary>
public static class PublicTypes
{
    /// <summary>
    /// The label, the noun and the menu column each type is shown under, in the order the menu
    /// draws its columns. Written down rather than derived, because a type's name says what it
    /// holds and not what a reader would look under, and the column order is the order the groups
    /// first appear here, which is the only place that order is written.
    /// </summary>
    private static readonly (Fellowship.SDK.EntityType Type, string Label, string Noun, string Group)[] Table =
    [
        (Fellowship.SDK.EntityType.Item, "Items", "item", "Items"),
        (Fellowship.SDK.EntityType.Trait, "Traits", "trait", "Items"),
        (Fellowship.SDK.EntityType.SetBonus, "Set Bonuses", "set bonus", "Items"),
        (Fellowship.SDK.EntityType.SetBonusPower, "Set Bonus Powers", "set bonus power", "Items"),
        (Fellowship.SDK.EntityType.WeaponPower, "Weapon Powers", "weapon power", "Items"),
        (Fellowship.SDK.EntityType.RelicPower, "Relic Powers", "relic power", "Items"),
        (Fellowship.SDK.EntityType.TraitPower, "Trait Powers", "trait power", "Items"),
        (Fellowship.SDK.EntityType.LegendaryStat, "Legendary Stats", "legendary stat", "Items"),
        (Fellowship.SDK.EntityType.Currency, "Currencies", "currency", "Items"),
        (Fellowship.SDK.EntityType.GemType, "Gem Types", "gem type", "Gems"),
        (Fellowship.SDK.EntityType.GemTier, "Gem Tiers", "gem tier", "Gems"),
        (Fellowship.SDK.EntityType.GemTrait, "Gem Traits", "gem trait", "Gems"),
        (Fellowship.SDK.EntityType.Ability, "Abilities", "ability", "Abilities"),
        (Fellowship.SDK.EntityType.Effect, "Effects", "effect", "Abilities"),
        (Fellowship.SDK.EntityType.Finesse, "Finesses", "finesse", "Abilities"),
        (Fellowship.SDK.EntityType.Talent, "Talents", "talent", "Characters"),
        (Fellowship.SDK.EntityType.TalentSlot, "Talent Slots", "talent slot", "Characters"),
        (Fellowship.SDK.EntityType.Creature, "Creatures", "creature", "Characters"),
        (Fellowship.SDK.EntityType.CompanionPet, "Companion Pets", "companion pet", "Characters"),
        (Fellowship.SDK.EntityType.Mount, "Mounts", "mount", "Characters"),
        (Fellowship.SDK.EntityType.MountPower, "Mount Powers", "mount power", "Characters"),
    ];

    /// <summary>
    /// Every public type, in the order the menu draws them.
    ///
    /// Projected from <see cref="Table"/> and checked against every member
    /// <see cref="Enum.GetValues{TEnum}"/> reports for <see cref="Fellowship.SDK.EntityType"/>, so a
    /// member added to the enum without a row in <see cref="Table"/> fails here rather than going
    /// missing from the menu.
    /// </summary>
    public static IReadOnlyList<PublicType> All { get; } = Verified(
        [.. Table.Select(row => new PublicType(row.Type, EntityTypes.Slug(row.Type), row.Label, row.Noun, row.Group))]);

    private static IReadOnlyList<PublicType> Verified(IReadOnlyList<PublicType> table)
    {
        var missing = Enum.GetValues<Fellowship.SDK.EntityType>().Except(table.Select(type => type.Type)).ToArray();

        return missing.Length == 0
            ? table
            : throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(PublicTypes)}.{nameof(Table)}.");
    }

    private static readonly IReadOnlyDictionary<Fellowship.SDK.EntityType, PublicType> ByType =
        All.ToDictionary(type => type.Type);

    /// <summary>
    /// What a caller is told when the type they named is not one. Written once and here rather than
    /// beside any one of the entry points that give the answer, because a page and an endpoint
    /// disagreeing about which types exist is the same hole this file closes: the slugs are listed
    /// outright, in the order a reader has already met them, since a caller who reached this spelled
    /// a type wrong and the sentence is the only place the right spellings appear.
    /// </summary>
    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    private static readonly string Accepted = string.Join(", ", All.Select(type => type.Slug));

    /// <summary>What a reader is shown for a type: the plural collection heading, "Items" or "Effects".</summary>
    public static string Label(Fellowship.SDK.EntityType type) => Find(type).Label;

    /// <summary>The singular noun, used when naming a single row that has no display name of its own.</summary>
    public static string Noun(Fellowship.SDK.EntityType type) => Find(type).Noun;

    /// <summary>The menu column a type is filed under.</summary>
    public static string Group(Fellowship.SDK.EntityType type) => Find(type).Group;

    /// <summary>
    /// The row's own name, on the rows carrying one the game would print, and null on the rest.
    ///
    /// The test deciding which is which - whether a shipped name is written as words or is a build
    /// label - already ran in the extractor, so this is the document's own
    /// <see cref="EntityDocument.Name"/> and nothing more.
    /// </summary>
    public static string? DisplayNameOf(EntityDocument document) => document.Name;

    /// <summary>
    /// The name a reader is shown for one row: its display name, or the row's noun and id, "Effect
    /// 2018".
    ///
    /// The fallback is not a rare corner. Once the extractor's words test has run, 5,997 of the
    /// 12,437 rows in the export carry no name, so nearly half print this way, and the only other
    /// thing a row has to be called by is its gameplay tag, which is precisely the internal spelling
    /// that must never be shown. A number beside a plain English noun says as much as there is to
    /// say and says nothing else.
    /// </summary>
    public static string NameOf(EntityDocument document)
    {
        if (DisplayNameOf(document) is { } name) return name;

        // Every document class is named exactly its EntityType member, so the type is read straight
        // off the runtime type rather than through a second table. `EntityDocumentConverter.Write`,
        // copied.
        var type = Enum.Parse<Fellowship.SDK.EntityType>(document.GetType().Name);
        return $"{Titled(Find(type).Noun)} {document.Id}";
    }

    private static PublicType Find(Fellowship.SDK.EntityType type) => ByType[type];

    /// <summary>
    /// The noun as a name begins, which is the whole of it: every noun in <see cref="Table"/> is one
    /// or two words, and only the first is capitalised, so a name reads "Set bonus power 4012" and
    /// not "Set Bonus Power 4012": that is the sentence a reader gets, and capitalising every word
    /// would read as a title the build never gave the row.
    /// </summary>
    private static string Titled(string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];
}
