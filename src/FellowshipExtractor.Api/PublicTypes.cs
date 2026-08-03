using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// One entity type as a reader meets it: the shipped table behind it, the route segment that names
/// it, the heading a collection of them is drawn under, the word for a single one, and the menu
/// column it is filed in.
///
/// The slug is the label lower-cased with its spaces hyphenated, and it is the only spelling a route
/// or a query value accepts. The table is an internal fact and is never a path segment:
/// <c>/api/tooltip/talent/124</c> is the address of a talent, and the asset class the row happens to
/// live in is not addressable at all.
/// </summary>
public sealed record PublicType(string Table, string Slug, string Label, string Noun, string Group);

/// <summary>
/// The entity types a reader is allowed to meet, and the only place that decision is written down.
///
/// The export carries 18 tag tables over 15,235 rows, of which 6 tables and 14,478 rows are public.
/// The test a table has to pass is not its size but whether its rows carry names the game itself
/// would print. 11 of the 12 tables left out carry no named row whatsoever, so a reader could only
/// ever meet one of them as an internal spelling, which is exactly what is being removed. Talent.ID
/// is the known anomaly at 1 named row in 237, which fails the same test and drops with the rest;
/// its grid coordinates are still read in code, they are just never a page.
///
/// A table not named here is not public. That default carries the fix: the failure being closed is
/// an internal table reaching the browser because nobody thought to exclude it, so silence has to
/// fail closed and a table the export starts carrying stays invisible until someone applies the test
/// above and adds it here.
///
/// Nothing else about the corpus is written down. Which heroes, slots, rarities and sets exist is
/// counted off the export and stays that way. Only a table's reader-facing name resists being
/// derived, because a shipped table name says what it holds and not what a reader would look under.
/// </summary>
public static class PublicTypes
{
    /// <summary>
    /// Every public type, in the order the menu draws them.
    ///
    /// Grouped so that a column's entries are adjacent, because the sequence of distinct
    /// <see cref="PublicType.Group"/> values here is the order the columns are laid out in and is
    /// written nowhere else. Within a column the largest table leads, which is the order the browser
    /// already draws its types in.
    /// </summary>
    public static IReadOnlyList<PublicType> All { get; } =
    [
        new PublicType("ItemID", "items", "Items", "item", "Items"),
        new PublicType("ItemTrait.ID", "traits", "Traits", "trait", "Items"),
        new PublicType("GameplayEffect", "effect", "Effect", "effect", "Abilities"),
        new PublicType("GameplayAbility", "ability", "Ability", "ability", "Abilities"),
        new PublicType("CRCharacterAugmentationAsset", "talent", "Talent", "talent", "Characters"),
        new PublicType("CRAICharacter", "creatures", "Creatures", "creature", "Characters"),
    ];

    private static readonly IReadOnlyDictionary<string, PublicType> ByTable =
        All.ToDictionary(type => type.Table, StringComparer.Ordinal);

    /// <summary>
    /// Case insensitive, because a slug arrives typed by hand as often as it arrives clicked, and
    /// keyed by slug alone: an internal table name resolves to nothing, so a route spelled with one
    /// answers 404 rather than being quietly understood.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, PublicType> BySlug =
        All.ToDictionary(type => type.Slug, StringComparer.OrdinalIgnoreCase);

    public static bool IsPublic(string? table) => table is not null && ByTable.ContainsKey(table);

    /// <summary>
    /// What a caller is told when the type they named is not one. Written once and here rather than
    /// beside any one of the four entry points that give the answer, because a page and an endpoint
    /// disagreeing about which types exist is the same hole this file closes: the slugs are listed
    /// outright, in the order a reader has already met them, since a caller who reached this spelled
    /// a type wrong and the sentence is the only place the right spellings appear.
    /// </summary>
    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    private static readonly string Accepted = string.Join(", ", All.Select(type => type.Slug));

    /// <summary>The route segment for a table, or null when the table is not public.</summary>
    public static string? SlugOf(string? table) => Find(table)?.Slug;

    /// <summary>
    /// The internal table a slug names, or null when the slug names nothing public. Internal table
    /// names are not accepted.
    /// </summary>
    public static string? TableOf(string? slug) =>
        slug is not null && BySlug.TryGetValue(slug, out var type) ? type.Table : null;

    /// <summary>
    /// What a reader is shown for a table: the plural collection heading, "Items" or "Effect".
    ///
    /// The fallback is a fixed word rather than anything worked out from the table's own spelling,
    /// which is the trap this whole type exists to close: deriving it would print "Talent / ID" and
    /// leak the name in the act of hiding it. A non-public table has no reader-facing name to give,
    /// so nothing that reaches a reader should be asking for one.
    /// </summary>
    public static string Label(string? table) => Find(table)?.Label ?? "Entries";

    /// <summary>
    /// The singular noun, used when naming a single row that has no display name of its own.
    /// </summary>
    public static string Noun(string? table) => Find(table)?.Noun ?? "entry";

    /// <summary>
    /// The menu column a table is filed under, and the empty string for a table that is in no
    /// column, so a menu built by grouping on this value can never grow one for a non-public table.
    /// </summary>
    public static string Group(string? table) => Find(table)?.Group ?? string.Empty;

    /// <summary>
    /// The row's own name, on the rows carrying one the game would print, and null on the rest.
    ///
    /// A display name is nearly always exactly that, which is why the field was taken as one by
    /// definition until the export turned out to carry the build's own labels in it. 18 of the 2,595
    /// distinct display names on public rows are one, on 48 rows, and they come in three shapes:
    /// UB_CadavrianBannerman.WarHorn is an asset path; ArcaneBlast, AutoAttack, CastAndImpact,
    /// MeleeAutoAttack, Playtest GearToken and eleven more are run-together labels, one of them the
    /// "AoE Heal" on GA_FxDebug_AoeSplash_Soul_Heal_01; and [PH - StarMap Token] is a placeholder
    /// marker. Whitespace was the first test of them and let every one of the last two shapes
    /// through.
    ///
    /// What is tested is whether the name is written as words, which is what
    /// <see cref="WrittenAsWords"/> asks, because the shape is what makes a label a label and a list
    /// of the 18 goes stale the day the build ships a nineteenth. A row failing it is treated as
    /// carrying no name, and prints, sorts and searches as "Effect 2812" like the 7,909 rows that
    /// ship none. Every reader of the field goes through here rather than reading it directly, so
    /// the heading, the flag that says a row is named in the game, the colour a name is drawn in and
    /// the terms the omnibox is built from all agree on which rows have one.
    /// </summary>
    public static string? DisplayNameOf(Entity entity) =>
        entity.DisplayName is { } name && WrittenAsWords(name) ? name : null;

    /// <summary>
    /// Whether a shipped name is spelled the way a name a player reads is spelled.
    ///
    /// Three characters and one join fail it. A dot or an underscore is a path's punctuation, and
    /// the 2,577 names that pass carry neither. A square bracket is how this build marks a row that
    /// was never meant to ship. And a capital straight after a lower case letter or a digit is the
    /// join in a run-together compound, which is the shape of every label the designers left in the
    /// field: it catches GearToken and StarMap inside the names that do carry spaces, so the two
    /// shapes are one test rather than a bracket rule with a compound rule beside it.
    /// </summary>
    private static bool WrittenAsWords(string name)
    {
        for (var at = 0; at < name.Length; at++)
        {
            if (name[at] is '.' or '_' or '[' or ']') return false;

            if (at > 0 && char.IsUpper(name[at]) &&
                (char.IsLower(name[at - 1]) || char.IsDigit(name[at - 1])))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The name a reader is shown for one row: its display name, or the row's noun and network id,
    /// "Effect 2018".
    ///
    /// The fallback is not a rare corner. 7,957 of the 14,478 public rows carry no name by the test
    /// above, so more than half print this way, and the only other thing a row has to be called by
    /// is its gameplay tag, which is precisely the internal spelling that must never be shown. A
    /// number beside a plain English noun says as much as there is to say and says nothing else.
    ///
    /// The share is falling rather than fixed. 1,288 effects are now named by the entity that
    /// applies them, which is 1,288 rows that used to reach this line and no longer do, so a figure
    /// here is a reading of one export rather than a property of the build.
    /// </summary>
    public static string NameOf(Entity entity)
    {
        if (DisplayNameOf(entity) is { } name) return name;

        // A row nested inside another is addressed by nothing: none of the 200 granted abilities or
        // 111 legendary stat lines in the export carries a table or an id. So the noun stands alone
        // rather than trailing an empty number, on the rows that reach here at all. All 200 grants
        // are named, which is why this is a guard rather than a path anything routinely takes.
        var noun = Titled(Noun(entity.Table));
        return entity.NetId is { } netId ? $"{noun} {netId}" : noun;
    }

    private static PublicType? Find(string? table) =>
        table is not null && ByTable.TryGetValue(table, out var type) ? type : null;

    /// <summary>
    /// The noun as a name begins, which is the whole of it: every one of the six nouns above and the
    /// "entry" fallback beside them is a single word. 7,957 of the 14,478 public rows carry no name
    /// of their own, so this is the path more than half of them take rather than a corner.
    /// </summary>
    private static string Titled(string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];
}
