using System.Globalization;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// One way of narrowing the corpus, and the only place a narrowing is defined.
///
/// The selector is used twice: once to count what the export holds under each value, and once to
/// decide whether a row passes a caller's filter. Sharing it is what stops a facet offering a value
/// that then matches nothing, which is the failure mode of writing the count and the predicate
/// separately.
/// </summary>
/// <param name="Key">The query parameter a caller sends, and the id the browser posts back.</param>
/// <param name="Label">What the browser writes above the list.</param>
/// <param name="Values">Every value a row in the export actually carries under this facet.</param>
public sealed record Facet(
    string Key,
    string Label,
    Func<Entity, IEnumerable<string>> Values)
{
    /// <summary>
    /// Where a value's colour comes from, for the facets the design system has a palette for. The
    /// rarity list in the client is written in the rung's own colour and so is a hero's name, so the
    /// browser is given the colour rather than choosing one.
    /// </summary>
    public Func<DesignTokens, string, string?>? Colour { get; init; }

    /// <summary>
    /// The word the browser prints for a value, where the shipped spelling is not it. A rarity is
    /// filed under one word and printed under another, and the printed one is what a reader types.
    /// </summary>
    public Func<EntityStore, string, string>? Display { get; init; }

    public bool Matches(Entity entity, IReadOnlyCollection<string> wanted) =>
        Values(entity).Any(value => wanted.Contains(value, StringComparer.OrdinalIgnoreCase));
}

/// <summary>A yes-or-no narrowing, which needs no value list.</summary>
public sealed record Flag(string Key, string Label, Func<Entity, bool> Predicate);

/// <summary>
/// A number a row carries, which is both a sortable column and a range to filter on. The browser
/// sends <c>&lt;key&gt;Min</c> and <c>&lt;key&gt;Max</c>.
/// </summary>
public sealed record Measure(string Key, string Label, Func<Entity, double?> Value)
{
    /// <summary>The unit the browser writes after the number, where the field has one.</summary>
    public string? Unit { get; init; }

    /// <summary>Set on the measures a results table shows as a column of its own.</summary>
    public bool Column { get; init; }
}

/// <summary>One value of a facet, with how many rows of the corpus carry it.</summary>
public sealed record FacetValue(string Value, string Label, int Count, string? Colour);

/// <summary>A facet as the browser reads it: the definition plus the counted values.</summary>
public sealed record FacetView(string Key, string Label, IReadOnlyList<FacetValue> Values);

/// <summary>A tag table under the name the browser prints and the group it is filed in.</summary>
public sealed record EntityType(string Table, string Label, string Group, int Count);

/// <summary>
/// One line of a category menu: a browse query and what it is called. The count is the number of
/// rows the query answers with, so a menu never offers a page that turns out to be empty.
/// </summary>
public sealed record MenuLink(string Label, string Query, int Count, string? Colour = null);

/// <summary>A menu column: one entity type and the facets worth entering it through.</summary>
public sealed record MenuEntry(string Table, string Label, string Query, int Count, IReadOnlyList<MenuSection> Sections);

public sealed record MenuSection(string Label, IReadOnlyList<MenuLink> Links);

public sealed record MenuGroup(string Label, IReadOnlyList<MenuEntry> Entries);

/// <summary>
/// Everything the browser needs to draw its menus, its filter panel and its type tabs, worked out
/// once from the export.
///
/// Nothing here is a list of the game's content. The groups a table is filed under are a
/// presentation decision and are written down; every value inside them - which slots exist, which
/// heroes, which schools of magic, how many of each - is counted off the export, so a slot the game
/// adds appears in the menu without this file being touched.
/// </summary>
public sealed class Codex
{
    /// <summary>
    /// How many values a menu offers before it stops listing them and sends the reader to the filter
    /// panel instead. The ability tag list is fifty-odd entries and a menu column that long is a
    /// scroll rather than a menu.
    /// </summary>
    private const int MenuValueLimit = 12;

    /// <summary>How many facets a type is offered under before the menu stops and the panel takes over.</summary>
    private const int MenuSectionLimit = 4;

    private readonly EntityStore store;
    private readonly DesignTokens tokens;

    public IReadOnlyList<Facet> Facets { get; }

    public IReadOnlyList<Flag> Flags { get; }

    public IReadOnlyList<Measure> Measures { get; }

    /// <summary>The tag tables, counted, under the names and groups the browser files them by.</summary>
    public IReadOnlyList<EntityType> Types { get; }

    public IReadOnlyList<MenuGroup> Menu { get; }

    public Codex(EntityStore store, DesignTokens tokens)
    {
        this.store = store;
        this.tokens = tokens;

        Facets =
        [
            new Facet("slot", "Slot", e => One(e.Slot)),
            new Facet("armor", "Armour", e => One(e.ArmorType)),
            new Facet("stat", "Primary stat", e => One(e.PrimaryStat)),
            new Facet("hero", "Hero", e => e.Heroes ?? [])
            {
                Colour = (t, value) => t.Hero(Codename(store, value)),
            },
            new Facet("school", "School", e => (e.Tags ?? []).Where(IsSchool)),
            new Facet("tag", "Tag", e => (e.Tags ?? []).Where(tag => !IsSchool(tag))),
            new Facet("ruleset", "Loot rule set", e => One(e.RuleSet)),
            new Facet("set", "Set", e => e.SetBonus ?? []),
            new Facet("resource", "Resource", e => One(e.Resource)),
            new Facet("gem", "Gem type", e => One(e.GemType))
            {
                Colour = (t, value) => t.Gem(value),
            },
            new Facet("trait", "Trait category", e => One(e.TraitCategory)),
            new Facet("role", "Role", e => e.AllowedRoles ?? [])
            {
                Colour = (t, value) => t.Role(value),
            },
            new Facet("rarity", "Rarity", e => One(e.Rarity))
            {
                Colour = (t, value) => t.Rarity(value),
                Display = (s, value) => s.Vocabulary.Rarities
                    .FirstOrDefault(r => string.Equals(r.Name, value, StringComparison.OrdinalIgnoreCase))
                    is { } rung ? rung.GemDisplayName : value,
            },
        ];

        Flags =
        [
            new Flag("named", "Has a display name", e => e.DisplayName is not null),
            new Flag("icon", "Has art", e => e.Icon is not null),
            new Flag("described", "Has description text", e => e.Description is not null),
            new Flag("grants", "Grants an ability", e => e.Grants is not null),
            new Flag("socket", "Rolls with a gem socket", e => e.GemSlot == true),
            new Flag("unique", "Unique equipped", e => e.UniqueEquipped == true),
            new Flag("scaling", "Scales off an attribute", e => e.ScalesOff is { Count: > 0 }),
        ];

        Measures =
        [
            new Measure("cooldown", "Cooldown", e => e.Cooldown) { Unit = "s", Column = true },
            new Measure("cast", "Cast time", e => e.CastTime) { Unit = "s", Column = true },
            new Measure("channel", "Channel", e => e.ChannelTime) { Unit = "s" },
            new Measure("range", "Range", e => e.Range) { Column = true },
            new Measure("radius", "Radius", e => e.Radius),
            new Measure("duration", "Duration", e => e.Duration) { Unit = "s" },
            new Measure("cost", "Cost", e => e.Cost),
            new Measure("charges", "Charges", e => e.Charges),
            new Measure("tier", "Gem tier", e => e.GemTier),
            new Measure("rank", "Max rank", e => e.MaxRank),
            new Measure("id", "Network id", e => e.NetId) { Column = true },
        ];

        var counts = store.Entities
            .GroupBy(e => e.Table!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Types =
        [
            .. store.Tables.Select(table => new EntityType(table, Label(table), Group(table), counts[table])),
        ];

        Menu = BuildMenu();
    }

    /// <summary>Every row of one table, which is what a menu column and its counts are drawn from.</summary>
    public IReadOnlyList<Entity> Of(string table) =>
        [.. store.Entities.Where(e => string.Equals(e.Table, table, StringComparison.Ordinal))];

    /// <summary>
    /// One facet counted over a given set of rows. The browser asks for this twice: over the whole
    /// export when it draws the menus, and over the rows a search or a filter left when it draws the
    /// panel beside the results, so a value that nothing left can answer is not offered.
    /// </summary>
    public FacetView View(Facet facet, IReadOnlyList<Entity> rows)
    {
        var counted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            foreach (var value in facet.Values(row))
            {
                if (value.Length == 0) continue;
                counted[value] = 1 + counted.GetValueOrDefault(value);
            }
        }

        return new FacetView(facet.Key, facet.Label,
        [
            .. counted
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new FacetValue(
                    pair.Key,
                    facet.Display?.Invoke(store, pair.Key) ?? Spaced(pair.Key),
                    pair.Value,
                    CodexView.Readable(facet.Colour?.Invoke(tokens, pair.Key)))),
        ]);
    }

    public Facet? Facet(string key) =>
        Facets.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    public Measure? Measure(string key) =>
        Measures.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    public Flag? Flag(string key) =>
        Flags.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The menus, one group per column. A type gets a submenu per facet its own rows spread across,
    /// so the entries under Items are the slots the build ships and the entries under Abilities are
    /// the heroes and the schools of magic, with nothing listed that nothing carries.
    /// </summary>
    private IReadOnlyList<MenuGroup> BuildMenu()
    {
        var groups = new List<MenuGroup>();

        foreach (var group in Groups)
        {
            var entries = new List<MenuEntry>();

            foreach (var type in Types.Where(t => t.Group == group))
            {
                var rows = Of(type.Table);
                var sections = new List<MenuSection>();

                foreach (var facet in Facets)
                {
                    var view = View(facet, rows);
                    if (view.Values.Count < 2) continue;

                    sections.Add(new MenuSection(view.Label,
                    [
                        .. view.Values.Take(MenuValueLimit).Select(value => new MenuLink(
                            value.Label,
                            $"?type={Uri.EscapeDataString(type.Table)}&{facet.Key}={Uri.EscapeDataString(value.Value)}",
                            value.Count,
                            value.Colour)),
                    ]));
                }

                entries.Add(new MenuEntry(
                    type.Table,
                    type.Label,
                    $"?type={Uri.EscapeDataString(type.Table)}",
                    type.Count,
                    // The first few facets rather than all of them. Facets are declared in the order
                    // a reader would reach for them, and the panel is a way in rather than the
                    // filter itself: the rest are one click away on the results page.
                    [.. sections.Take(MenuSectionLimit)]));
            }

            if (entries.Count > 0) groups.Add(new MenuGroup(group, entries));
        }

        return groups;
    }

    private static IEnumerable<string> One(string? value) => value is null ? [] : [value];

    /// <summary>
    /// A damage school, which is a tag whose first segment says which of the two the ability is.
    /// Split out of the general tag list because it is the axis a reader browses abilities by and
    /// the one a tooltip's damage sentence is written from.
    /// </summary>
    private static bool IsSchool(string tag) =>
        tag.StartsWith("Magic", StringComparison.Ordinal) || tag.StartsWith("Physical", StringComparison.Ordinal);

    /// <summary>
    /// A hero's codename, which is what the palette keys their colour by while the export names them
    /// by the name the game prints.
    /// </summary>
    private static string Codename(EntityStore store, string name) =>
        store.Hero(name)?.Codename ?? name;

    /// <summary>
    /// A shipped identifier as a reader reads it: dotted segments spaced out and runs of capitals
    /// broken up, so CritChance prints as Crit Chance and Skill.Offensive as Skill / Offensive. The
    /// value itself is never rewritten, only what is drawn for it.
    /// </summary>
    public static string Spaced(string value)
    {
        var text = string.Join(" / ", value.Split('.'));
        var spaced = new System.Text.StringBuilder(text.Length + 8);

        for (var at = 0; at < text.Length; at++)
        {
            if (at > 0 && char.IsUpper(text[at]) && (char.IsLower(text[at - 1]) || char.IsDigit(text[at - 1])))
            {
                spaced.Append(' ');
            }

            spaced.Append(text[at]);
        }

        return spaced.ToString();
    }

    /// <summary>
    /// The columns the menu is laid out in, in the order they are drawn.
    /// </summary>
    private static readonly string[] Groups = ["Items", "Abilities", "Characters", "World", "Rules", "Other"];

    /// <summary>
    /// Which column a tag table is filed under, and what it is called there.
    ///
    /// Curated, and the only curated thing in this file. A tag table's name says what it holds but
    /// not what a reader would look under - ItemTrait.ID and DynamicItemSetBonusMapping are both
    /// about items and belong in different columns - and there is nothing in the build that groups
    /// them. A table not named here still appears, under Other and under its own name, so a new
    /// export is browsable before anyone edits this.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Label, string Group)> Filed =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["ItemID"] = ("Items", "Items"),
            ["ItemTrait.ID"] = ("Item Traits", "Items"),
            ["ItemTraitTrees"] = ("Trait Trees", "Items"),
            ["GameplayAbility"] = ("Abilities", "Abilities"),
            ["GameplayEffect"] = ("Effects", "Abilities"),
            ["GameplayCue.PermanentMark"] = ("Cues", "Abilities"),
            ["CharacterID"] = ("Characters", "Characters"),
            ["CRAICharacter"] = ("Creatures", "Characters"),
            ["CRCharacterAugmentationAsset"] = ("Augmentations", "Characters"),
            ["Talent.ID"] = ("Talents", "Characters"),
            ["Level.ID"] = ("Levels", "World"),
            ["UIMaps"] = ("Maps", "World"),
            ["Encounters"] = ("Encounters", "World"),
            ["Modifiers"] = ("Modifiers", "World"),
            ["DynamicHeroItemRuleSet"] = ("Loot Rule Sets", "Rules"),
            ["DynamicItemAbilityPool"] = ("Ability Pools", "Rules"),
            ["DynamicItemAbilityRank"] = ("Ability Ranks", "Rules"),
            ["DynamicItemSetBonusMapping"] = ("Set Bonus Mappings", "Rules"),
        };

    public static string Label(string table) =>
        Filed.TryGetValue(table, out var filed) ? filed.Label : Spaced(table);

    private static string Group(string table) =>
        Filed.TryGetValue(table, out var filed) ? filed.Group : "Other";

    /// <summary>A number as the browser prints it: no trailing zeros, invariant.</summary>
    public static string Number(double value) =>
        Math.Round((decimal)value, 3).Normalize().ToString(CultureInfo.InvariantCulture);
}
