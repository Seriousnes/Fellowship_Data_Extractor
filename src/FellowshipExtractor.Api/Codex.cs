using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Fellowship.SDK.Documents;

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
    Func<EntityDocument, IEnumerable<string>> Values)
{
    /// <summary>
    /// Where a value's colour comes from, for the facets the design system has a palette for. The
    /// rarity list in the client is written in the rung's own colour and so is a hero's name, so the
    /// browser is given the colour rather than choosing one.
    /// </summary>
    public Func<DesignTokens, string, string?>? Colour { get; init; }

    public bool Matches(EntityDocument document, IReadOnlyCollection<string> wanted) =>
        Values(document).Any(value => wanted.Contains(value, StringComparer.OrdinalIgnoreCase));
}

/// <summary>A yes-or-no narrowing, which needs no value list.</summary>
public sealed record Flag(string Key, string Label, Func<EntityDocument, bool> Predicate);

/// <summary>
/// A number a row carries, which is both a sortable column and a range to filter on. The browser
/// sends <c>&lt;key&gt;Min</c> and <c>&lt;key&gt;Max</c>.
/// </summary>
public sealed record Measure(string Key, string Label, Func<EntityDocument, double?> Value)
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

/// <summary>
/// One entity type, counted: the route segment that addresses it, the heading the browser prints and
/// the column it is filed in. The type itself is carried so code can match a row against it; only the
/// slug is ever written into a link.
/// </summary>
public sealed record TypeView(EntityType Type, string Slug, string Label, string Group, int Count);

/// <summary>
/// One line of a category menu: a browse query and what it is called. The count is the number of
/// rows the query answers with, so a menu never offers a page that turns out to be empty.
/// </summary>
public sealed record MenuLink(string Label, string Query, int Count, string? Colour = null);

/// <summary>
/// A menu column: one entity type and the facets worth entering it through. The type is named by
/// its heading and reached by its query, which is all a menu is; the table behind it was carried
/// here once and read by nothing, while <c>/api/catalog</c> published it on every entry.
/// </summary>
public sealed record MenuEntry(string Label, string Query, int Count, IReadOnlyList<MenuSection> Sections);

public sealed record MenuSection(string Label, IReadOnlyList<MenuLink> Links);

public sealed record MenuGroup(string Label, IReadOnlyList<MenuEntry> Entries);

/// <summary>
/// A field several sibling document classes declare under the same name and shape but share no
/// common interface for, read off a document by its own runtime type rather than by a hand-kept
/// list of which classes carry it. The same idiom <see cref="EntityStore"/> uses privately for its
/// list-valued fields, here for the scalar shapes a facet, a flag or a measure reads: a number, a
/// word and a list of words.
///
/// A document class with no property of that name answers null or empty rather than throwing, which
/// is what lets a measure like cooldown span seven document types with no list of them written here,
/// and pick up an eighth the day a document class gains the field. <see cref="Number"/> unboxes a
/// <c>double</c> or an <c>int</c> field alike, and either's nullable form the same way: a boxed value
/// carries only its own runtime type, whatever the property's declared type says, which is why
/// <c>Cost</c> answered null on every <see cref="TalentSlot"/> until this read both rather than only
/// the first. A <c>bool</c> field still needs pattern matching, because a flag reads it as a
/// predicate rather than a magnitude and has no use for a boxed reading here.
/// </summary>
internal static class DocumentFields
{
    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Cache = new();

    private static PropertyInfo? Property(EntityDocument document, string name) =>
        Cache.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property));

    public static double? Number(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) switch
        {
            null => null,
            double number => number,
            int number => number,
            var boxed => throw new InvalidOperationException(
                $"{document.GetType().Name}.{name} is {boxed.GetType().Name}, which DocumentFields.Number does not unbox."),
        };

    public static string? Text(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) as string;

    public static IReadOnlyList<string> Words(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) as IReadOnlyList<string> ?? [];
}

/// <summary>
/// Everything the browser needs to draw its menus, its filter panel and its type tabs, worked out
/// once from the export.
///
/// Nothing here is a list of the game's content. What a reader calls each type is
/// <see cref="PublicTypes"/>'s decision; every value inside them - which slots exist, which
/// heroes, which schools of magic, how many of each - is counted off the export, so a slot the game
/// adds appears in the menu without this file being touched.
///
/// What a facet may offer is decided the same way a type's label is: by whether its values are words
/// the game itself prints. A slot is Chest and a school is Magic / Fire, both read off the game's own
/// screens; a loot rule set is Default_NoGemSlot_Necklace and a gameplay tag is Skill.Secondary,
/// neither a word a player ever reads. 2,204 items carry a rule set today, nine spellings between
/// them, and no document carries a tag field at all - so whether or not a document states one, the
/// corpus carries axes this offers no way to browse by, and that is the point rather than an
/// omission.
///
/// The values a facet offers are also the whole of what it may be asked for, which
/// <see cref="Offered"/> answers and <see cref="CodexQuery.Parse"/> enforces. A filter is a URL a
/// reader hands on, so a spelling nothing here would ever draw is answered with the list rather than
/// with an empty page under a chip repeating it.
/// </summary>
public sealed class Codex
{
    /// <summary>
    /// How many values a menu offers before it stops listing them and sends the reader to the filter
    /// panel instead. The resource list is fifteen entries and the schools fourteen, and a menu
    /// column that long is a scroll rather than a menu.
    /// </summary>
    private const int MenuValueLimit = 12;

    /// <summary>How many facets a type is offered under before the menu stops and the panel takes over.</summary>
    private const int MenuSectionLimit = 4;

    private readonly EntityStore store;
    private readonly DesignTokens tokens;

    /// <summary>What each facet's rows actually carry, which is the whole of what it may be asked for.</summary>
    private readonly IReadOnlyDictionary<string, string[]> offered;

    public IReadOnlyList<Facet> Facets { get; }

    public IReadOnlyList<Flag> Flags { get; }

    public IReadOnlyList<Measure> Measures { get; }

    /// <summary>
    /// Every entity type, counted, in the order <see cref="PublicTypes"/> declares its menu columns.
    /// A type the export happens to hold no row of today is absent, so a tab, a menu entry and a
    /// heading can only ever name a type a reader can actually open.
    /// </summary>
    public IReadOnlyList<TypeView> Types { get; }

    /// <summary>
    /// How many rows a reader can browse, tab into or search: the whole of the export. The
    /// extractor's own drop rules already keep a row a reader cannot meet out of the file, so there
    /// is no narrower count left to take, and every surface stating a corpus size states this one.
    ///
    /// Read off the store rather than summed over the types, so a second derivation of the number on
    /// screen cannot drift from the first.
    /// </summary>
    public int Count => store.Count;

    public IReadOnlyList<MenuGroup> Menu { get; }

    public Codex(EntityStore store, DesignTokens tokens)
    {
        this.store = store;
        this.tokens = tokens;

        Facets =
        [
            new Facet("slot", "Slot", document => One(document is Item item ? item.Slot : null)),
            new Facet("armor", "Armour", document =>
                One(document is Item { ArmorType: { } armor } ? armor.ToString() : null)),

            // The primary-stat enum's own members are Agility, Intellect and Strength, which is
            // exactly the word the game prints for each, so the facet reads the document's own value
            // rather than asking the shipped attribute table for a translation nothing here still
            // needs.
            new Facet("stat", "Primary stat", document =>
                One(document is Item { PrimaryStat: { } stat } ? stat.ToString() : null)),

            // Both the items an id splits by and the talent slots one does: an item names every
            // hero the row is for, and a talent slot names exactly one, because a hero's grid never
            // shares a slot position with another hero's.
            new Facet("hero", "Hero", document => document switch
            {
                Item { Heroes: { } heroes } => heroes.Select(hero => hero.ToString()),
                TalentSlot slot => [slot.Hero.ToString()],
                _ => [],
            })
            {
                Colour = (t, value) => t.Hero(Codename(store, value)),
            },

            new Facet("school", "School", Schools),
            new Facet("set", "Set", document => document is Item { SetBonus: { } sets } ? sets : []),
            new Facet("resource", "Resource", document =>
                One(document is Ability { Resource: { } resource } ? resource : null)),

            new Facet("gem", "Gem type", document => One(document switch
            {
                GemType gem => gem.Gem.ToString(),
                GemTier tier => tier.Gem.ToString(),
                GemTrait trait => trait.Gem.ToString(),
                _ => null,
            }))
            {
                Colour = (t, value) => t.Gem(value),
            },

            new Facet("trait", "Trait category", document =>
                One(document is Trait { Category: { } category } ? category : null)),

            new Facet("role", "Role", document => document is SetBonus { AllowedRoles: { } roles } ? roles : [])
            {
                Colour = (t, value) => t.Role(value),
            },

            // The printed word rather than the filed one the document carries, joined on the tier
            // ordinal the enum's own members carry as their value: an item's rung prints through the
            // item column of the ladder and a gem tier's through the gem column, which is why the
            // same filed Champion prints Heroic on one and Flawless on the other.
            //
            // The colour is the reading that still needs the filed word, exactly as a hero's does.
            new Facet("rarity", "Rarity", document => One(document switch
            {
                Item { Rarity: { } rarity } => RarityWord(store, rarity, gem: false),
                GemTier tier => RarityWord(store, tier.Rarity, gem: true),
                _ => null,
            }))
            {
                Colour = (t, value) => t.Rarity(Filed(store, value)),
            },
        ];

        Flags =
        [
            new Flag("named", "Named in the game", document => PublicTypes.DisplayNameOf(document) is not null),
            new Flag("icon", "Has art", document => DocumentFields.Text(document, nameof(Item.Icon)) is not null),
            new Flag("described", "Has description text",
                document => DocumentFields.Text(document, nameof(Item.Description)) is not null),
            new Flag("grants", "Grants an ability", document => document is Item { GrantsId: not null }),
            new Flag("socket", "Rolls with a gem socket", document => document is Item { GemSlot: true }),
            new Flag("unique", "Unique equipped", document => document is Item { UniqueEquipped: true }),
            new Flag("scaling", "Scales off an attribute",
                document => DocumentFields.Words(document, nameof(Ability.ScalesOff)).Count > 0),
        ];

        Measures =
        [
            new Measure("cooldown", "Cooldown", document => DocumentFields.Number(document, nameof(Ability.Cooldown)))
            {
                Unit = "s", Column = true,
            },
            new Measure("cast", "Cast time", document => DocumentFields.Number(document, nameof(Ability.CastTime)))
            {
                Unit = "s", Column = true,
            },
            new Measure("channel", "Channel", document => DocumentFields.Number(document, nameof(Ability.ChannelTime)))
            {
                Unit = "s",
            },
            new Measure("range", "Range", document => DocumentFields.Number(document, nameof(Ability.Range)))
            {
                Column = true,
            },
            new Measure("radius", "Radius", document => DocumentFields.Number(document, nameof(Ability.Radius))),
            new Measure("duration", "Duration", document => DocumentFields.Number(document, nameof(Ability.Duration)))
            {
                Unit = "s",
            },
            new Measure("cost", "Cost", document => DocumentFields.Number(document, nameof(Ability.Cost))),
            new Measure("charges", "Charges", document => DocumentFields.Number(document, nameof(Ability.Charges))),
            new Measure("tier", "Gem tier", document => DocumentFields.Number(document, nameof(GemTier.Tier))),
            new Measure("rank", "Max rank", document => DocumentFields.Number(document, nameof(Trait.MaxRank))),

            // Addressable on its own: /api/tooltip/talent/124 is how the game's own talent is asked
            // for. Every document carries one, so this is the one measure with no row ever missing.
            new Measure("id", "ID", document => (double?)document.Id) { Column = true },
        ];

        var counts = store.Entities
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(g => g.Key, g => g.Count());

        // Counted off the corpus and ordered by PublicTypes.All: the corpus says how many rows of
        // each type there are and PublicTypes says which column and heading each is drawn under, and
        // its declaration order is the only place that column order is written. A type the extractor
        // starts writing appears in the browser's type tabs without either file being touched twice.
        Types =
        [
            .. PublicTypes.All
                .Select(type => new TypeView(
                    type.Type, type.Slug, type.Label, type.Group, counts.GetValueOrDefault(type.Type)))
                .Where(type => type.Count > 0),
        ];

        offered = Facets.ToDictionary(
            facet => facet.Key,
            facet => View(facet, store.Entities).Values.Select(value => value.Value).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        Menu = BuildMenu();
    }

    /// <summary>Every row of one type, which is what a menu column and its counts are drawn from.</summary>
    public IReadOnlyList<EntityDocument> Of(EntityType type) =>
        [.. store.Entities.Where(document => EntityTypes.TypeOf(document) == type)];

    /// <summary>
    /// One facet counted over a given set of rows. The browser asks for this twice: over the whole
    /// export when it draws the menus, and over the rows a search or a filter left when it draws the
    /// panel beside the results, so a value that nothing left can answer is not offered.
    /// </summary>
    public FacetView View(Facet facet, IReadOnlyList<EntityDocument> rows)
    {
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);

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
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(spellings => (Value: Written(spellings), Count: spellings.Sum(pair => pair.Value)))
                .OrderByDescending(value => value.Count)
                .ThenBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
                .Select(value => new FacetValue(
                    value.Value,
                    Printed(facet, value.Value),
                    value.Count,
                    CodexView.Readable(facet.Colour?.Invoke(tokens, value.Value)))),
        ]);
    }

    /// <summary>
    /// Which spelling of a value the panel offers, where the export writes more than one of it.
    ///
    /// A filter has always matched a value whatever its case, so two spellings were already one
    /// entry; the entry was named by whichever the export wrote first, and the 5 abilities spelling
    /// their resource "mana" are written before the 32 spelling it "Mana", so the panel offered a
    /// lower case word and put it in the address bar. The rows decide instead: the spelling the most
    /// of them carry is the one drawn and the one linked. Resource is the only facet in the build
    /// with anything to decide, and a tie falls to the ordinal-first spelling, which is the
    /// capitalised one.
    /// </summary>
    private static string Written(IEnumerable<KeyValuePair<string, int>> spellings) =>
        spellings
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First()
            .Key;

    /// <summary>
    /// The word the browser prints for one value of a facet.
    ///
    /// Every place a chosen value is drawn comes through here, not only the list it was picked from,
    /// so a chip, a group heading and a menu entry can never disagree about what a value is called.
    /// Measured over the export, the 11 facets offer 91 values between them and the longest list is
    /// the 15 resources, and every one of the 91 is already the word a page prints, so this is the
    /// value itself on all of them.
    /// </summary>
    public string Printed(Facet facet, string value) => Spaced(value);

    /// <summary>
    /// Every value a facet's rows carry, in the order the panel offers them, which is the whole of
    /// what a caller may ask that facet for.
    ///
    /// The counted values rather than a list written down here, because they are the same reading
    /// the panel, the menus and the links are all drawn from: a value this accepts is one a reader
    /// can be shown, and a value it turns away is one no surface offers. That is what closes the
    /// last way an internal spelling reached a page. None of them resolved to anything even before -
    /// a hero's codename matches no row, because the export names heroes as the game prints them -
    /// but a filter that merely matched nothing answered 200 with an empty page and a chip reading
    /// "Hero: Lisa", so the codename was still a linkable address that said nothing about being
    /// wrong.
    /// </summary>
    public IReadOnlyList<string> Offered(Facet facet) => offered.GetValueOrDefault(facet.Key, []);

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

        // The columns, in the order the public types declare them, which is the only place that order
        // is written down. Taken off the counted types rather than off a list of column names, so a
        // column exists exactly when something is filed in it.
        foreach (var group in Types.Select(type => type.Group).Distinct(StringComparer.Ordinal))
        {
            var entries = new List<MenuEntry>();

            foreach (var type in Types.Where(t => t.Group == group))
            {
                var rows = Of(type.Type);
                var sections = new List<MenuSection>();

                foreach (var facet in Facets)
                {
                    var view = View(facet, rows);
                    if (view.Values.Count < 2) continue;

                    sections.Add(new MenuSection(view.Label,
                    [
                        .. view.Values.Take(MenuValueLimit).Select(value => new MenuLink(
                            value.Label,
                            $"?type={Uri.EscapeDataString(type.Slug)}&{facet.Key}={Uri.EscapeDataString(value.Value)}",
                            value.Count,
                            value.Colour)),
                    ]));
                }

                entries.Add(new MenuEntry(
                    type.Label,
                    $"?type={Uri.EscapeDataString(type.Slug)}",
                    type.Count,
                    // The first few facets rather than all of them. Facets are declared in the order
                    // a reader would reach for them, and the panel is a way in rather than the
                    // filter itself: the rest are one click away on the results page.
                    [.. sections.Take(MenuSectionLimit)]));
            }

            groups.Add(new MenuGroup(group, entries));
        }

        return groups;
    }

    private static IEnumerable<string> One(string? value) => value is null ? [] : [value];

    /// <summary>
    /// The damage schools a row is, read straight off the document rather than out of a tag list:
    /// the extractor already lifts them out and spells each one the way a tooltip's damage sentence
    /// does, Magic / Fire and Physical / Bleed. Four document types can state the field - Ability,
    /// Effect, RelicPower and WeaponPower - and measured over the export only two of them carry any:
    /// 704 abilities and 8 weapon powers, printing 14 distinct schools between them.
    ///
    /// Public because the facet is not the only reader: a caller holding an arbitrary
    /// <see cref="EntityDocument"/> - a results row, a nested grant - has no narrower type to switch
    /// on itself, so the fan-out across the four document types lives here once rather than once per
    /// caller.
    /// </summary>
    public static IReadOnlyList<string> Schools(EntityDocument document) => document switch
    {
        Ability ability => ability.Schools ?? [],
        Effect effect => effect.Schools ?? [],
        RelicPower relic => relic.Schools ?? [],
        WeaponPower weapon => weapon.Schools ?? [],
        _ => [],
    };

    /// <summary>
    /// The attributes a scaling list names, in the words the game prints where it prints one and
    /// spelled out where it does not.
    ///
    /// Measured over the export: 3,045 rows across 11 document types carry a scaling list and
    /// between them name 82 attributes, of which 15 sit in the shipped attribute table: Strength on
    /// 1,145 rows, Intellect on 486, Critical Strike, Move Speed and eleven more. The other 67 are
    /// DamageScale, IncomingDamageMultiplier, MaxHealth and the like, the tuning knobs behind a
    /// number rather than a stat anybody wears, and 891 of the 3,045 rows name none of the 15: a
    /// reader filtering by one of those knobs would meet an empty panel if this dropped them.
    ///
    /// They are read out instead. A coefficient and the knob it turns are the data a database like
    /// this exists to show; what has to stay hidden is the run-together spelling, and
    /// <see cref="EntityStore.StatName"/> is what takes that off, so a reader meets "Damage Scale"
    /// and never DamageScale.
    ///
    /// One reading for the pills on an entry's page, the flag that offers to filter by it and the
    /// scaling list <c>/api/entity</c> answers with, because a flag that counts rows the page then
    /// draws nothing for is the same defect as a facet offering a value that matches nothing.
    /// </summary>
    public IEnumerable<string> Scaling(IReadOnlyList<string>? attributes) =>
        (attributes ?? []).Select(store.StatName).Distinct(StringComparer.Ordinal);

    /// <summary>
    /// A hero's codename, which is what the palette keys their colour by while the export names them
    /// by the name the game prints.
    /// </summary>
    private static string Codename(EntityStore store, string name) =>
        store.Hero(name)?.Codename ?? name;

    /// <summary>
    /// The word a rarity rung prints, for the document type asking: the item column of the ladder or
    /// the gem column, joined on the tier ordinal the <see cref="Rarity"/> enum's own members carry
    /// as their value. A rung the ladder names no tier for prints its own filed spelling, which is
    /// unreachable on the export today - every <see cref="Rarity"/> value a document carries is one
    /// of the shipped ladder's own tiers.
    /// </summary>
    private static string RarityWord(EntityStore store, Rarity rarity, bool gem) =>
        store.Vocabulary.Rarities.FirstOrDefault(r => r.Tier == (int)rarity) is { } entry
            ? (gem ? entry.GemDisplayName : entry.DisplayName)
            : rarity.ToString();

    /// <summary>
    /// The word a rarity rung is filed under, which is what the palette keys its colour by while the
    /// facet offers the word the rung prints.
    ///
    /// The same shape as <see cref="Codename"/> and for the same reason: the two spellings diverge
    /// at the top of the ladder, where the rung a gem calls Flawless is filed as Champion, so the
    /// gold under rarity-champion was reached by no value the facet carries and the six Flawless
    /// rows drew their tier in no colour at all.
    /// </summary>
    private static string Filed(EntityStore store, string printed) =>
        store.Rarity(printed, gem: true)?.Name ?? printed;

    /// <summary>
    /// A shipped identifier as a reader reads it: dotted segments spaced out and runs of capitals
    /// broken up, so DamageScale prints as Damage Scale and Skill.Offensive as Skill / Offensive. The
    /// value itself is never rewritten, only what is drawn for it.
    ///
    /// Not the path an attribute takes. An attribute goes through <see cref="EntityStore.StatName"/>,
    /// which answers with the word the shipped table prints for it and falls back to here for the 67
    /// the table names no word for; splitting the key directly would print "Crit Chance" for the stat
    /// the game calls Critical Strike.
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
    /// What a reader is shown for a type. Kept as the name every razor page already reaches for,
    /// answered by <see cref="PublicTypes"/>, which is where the decision now lives.
    /// </summary>
    public static string Label(EntityType type) => PublicTypes.Label(type);

    /// <summary>A number as the browser prints it: no trailing zeros, invariant.</summary>
    public static string Number(double value) =>
        Math.Round((decimal)value, 3).Normalize().ToString(CultureInfo.InvariantCulture);
}
