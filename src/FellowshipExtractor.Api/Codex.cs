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
    /// The word the browser prints for a value, where the shipped spelling is not it.
    ///
    /// No facet needs one today. A value is both what the panel prints and what its links, its
    /// chips and the list of values a caller may ask for all carry, so a facet whose rows spell
    /// their value the build's way spells it at the selector instead, where every one of those
    /// readings picks it up: that is what <see cref="Codex.Schools"/> and the primary stat facet
    /// do. Printing alone would leave the build's own spelling in the address bar, which is the
    /// half of the fix a reader can hand on to somebody else.
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

/// <summary>
/// A public type, counted: the shipped table behind it, the route segment that addresses it, the
/// heading the browser prints and the column it is filed in. The table is carried so code can match a
/// row against it; only the slug is ever written into a link.
/// </summary>
public sealed record EntityType(string Table, string Slug, string Label, string Group, int Count);

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
/// Everything the browser needs to draw its menus, its filter panel and its type tabs, worked out
/// once from the export.
///
/// Nothing here is a list of the game's content. Which types are public and what a reader calls them
/// is <see cref="PublicTypes"/>'s decision; every value inside them - which slots exist, which
/// heroes, which schools of magic, how many of each - is counted off the export, so a slot the game
/// adds appears in the menu without this file being touched.
///
/// What a facet may offer is decided the same way a type is: by whether its values are words the game
/// itself prints. A slot is Chest and a school is Magic / Fire, both read off the game's own screens; a
/// loot rule set is Default_NoGemSlot_Necklace and a gameplay tag is Skill.Secondary, neither of which
/// a player has ever been shown. So the corpus carries axes this offers no way to browse by, and that
/// is the point rather than an omission.
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
    /// The public types, counted, in the order the menu draws them. A table nothing public is filed
    /// under is absent, so a tab, a menu entry and a heading can only ever name one of these.
    /// </summary>
    public IReadOnlyList<EntityType> Types { get; }

    /// <summary>
    /// How much of the build a reader can actually reach, which is what every surface stating a
    /// corpus size states. The export's own line count is 757 rows and 12 tables larger, and the
    /// footer telling a reader that number promised them entries no page, tab, facet or search can
    /// answer with.
    ///
    /// Read off the store rather than summed over the types. The two are the same number by
    /// construction - both count the rows whose table <see cref="PublicTypes"/> files a slug for -
    /// and a second derivation of a number on screen is how the first one comes to be wrong.
    /// </summary>
    public int Count => store.PublicCount;

    public IReadOnlyList<MenuGroup> Menu { get; }

    public Codex(EntityStore store, DesignTokens tokens)
    {
        this.store = store;
        this.tokens = tokens;

        Facets =
        [
            new Facet("slot", "Slot", e => One(e.Slot)),
            new Facet("armor", "Armour", e => One(e.ArmorType)),

            // Read through the shipped attribute table, because the field holds the build's key for
            // the attribute rather than the word the game prints for it. The three keys an item's
            // primary slot carries are Agility, Intellect and Strength and each is its own printed
            // word, so nothing moves on screen today; a key the table names differently would
            // otherwise reach the panel, the chip and the address bar as CritChance, which is the
            // one spelling of that attribute the game has never shown anybody.
            new Facet("stat", "Primary stat", e =>
                One(e.PrimaryStat is { } attribute ? store.StatName(attribute) : null)),
            new Facet("hero", "Hero", e => e.Heroes ?? [])
            {
                Colour = (t, value) => t.Hero(Codename(store, value)),
            },
            new Facet("school", "School", Schools),
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
            // The word is the export's own and is already the printed one: the 24 rows carrying a
            // rarity are the six gem types at four tiers each, and a gem tier ships the word a gem
            // prints, so the values are Uncommon, Rare, Epic and Flawless. The lookup that used to
            // print a rung's word in place of the word it is filed under returned its input on all
            // four of them, and left the filed word in the link besides.
            //
            // The colour is the reading that still needs the filed word, exactly as a hero's does.
            new Facet("rarity", "Rarity", e => One(e.Rarity))
            {
                Colour = (t, value) => t.Rarity(Filed(store, value)),
            },
        ];

        Flags =
        [
            new Flag("named", "Named in the game", e => PublicTypes.DisplayNameOf(e) is not null),
            new Flag("icon", "Has art", e => e.Icon is not null),
            new Flag("described", "Has description text", e => e.Description is not null),
            new Flag("grants", "Grants an ability", e => e.Grants is not null),
            new Flag("socket", "Rolls with a gem socket", e => e.GemSlot == true),
            new Flag("unique", "Unique equipped", e => e.UniqueEquipped == true),
            new Flag("scaling", "Scales off an attribute", e => Scaling(e.ScalesOff).Any()),
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
            // An id is addressable and so is public - /api/tooltip/talent/124 is how the game's own
            // talent is asked for - but "network id" is the engine's word for it and not a reader's.
            new Measure("id", "ID", e => e.NetId) { Column = true },
        ];

        var counts = store.Entities
            .GroupBy(e => e.Table!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // Driven off the public list rather than off what the export happens to hold, which is the
        // whole of the fix: a table the extractor starts writing is invisible until someone files it
        // as public, where reading the export's own tables would put it in the tabs the same day. A
        // public type the export does not carry is dropped, so no tab offers an empty page.
        Types =
        [
            .. PublicTypes.All
                .Select(type => new EntityType(
                    type.Table, type.Slug, type.Label, type.Group, counts.GetValueOrDefault(type.Table)))
                .Where(type => type.Count > 0),
        ];

        // Counted over the reachable rows alone, for the reason the menus and the filter panel are:
        // a value only the 757 rows outside the public tables carry is a value no page ever offers,
        // and accepting it would leave one filterable spelling of the corpus that nothing shows.
        var reachable = store.Entities.Where(entity => PublicTypes.IsPublic(entity.Table)).ToArray();

        offered = Facets.ToDictionary(
            facet => facet.Key,
            facet => View(facet, reachable).Values.Select(value => value.Value).ToArray(),
            StringComparer.OrdinalIgnoreCase);

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
    /// The facets that would once have needed a different word here spell it at their selector now,
    /// so this is the value itself on all 90 of them.
    /// </summary>
    public string Printed(Facet facet, string value) =>
        facet.Display?.Invoke(store, value) ?? Spaced(value);

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
    ///
    /// The 11 facets offer 90 values between them and the longest list is the 15 resources, so a
    /// caller who asks for something else can simply be told all of them.
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
        // column exists exactly when something public is filed in it: the three that remain are Items,
        // Abilities and Characters, and the Rules column stopped being drawn the moment the one type
        // filed under it was withdrawn, without a line here or anywhere else being touched.
        foreach (var group in Types.Select(type => type.Group).Distinct(StringComparer.Ordinal))
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
    /// The damage schools a row carries, spelled the way a page prints them: the tags whose first
    /// segment says which of the two families it is, read out as Magic / Fire and Physical / Bleed.
    ///
    /// The one family of the build's tags a player meets by name, read straight off a tooltip's
    /// damage sentence, where the other three dozen tags on the same rows are the build talking to
    /// itself: Skill.Secondary, TrackCooldown, Targeting. So the schools are lifted out here and every
    /// other tag is neither browsable nor drawn.
    ///
    /// The tag's own spelling stops here rather than at each place one is drawn. A facet value is
    /// both what a page prints and what its links carry, and this was the one facet whose value was a
    /// shipped tag: the printed word said Magic / Fire while the address said school=Magic.Fire, on
    /// the menu, the filter panel, the results column, the entry pills and /api/entity alike, so a
    /// reader could hand on a link with a gameplay tag in it. Spelling it once at the selector every
    /// one of those reads settles all of them, and the 14 tags this answers for hold nothing but a
    /// family and a school, so nothing is lost in the reading.
    /// </summary>
    public static IEnumerable<string> Schools(Entity entity) =>
        (entity.Tags ?? [])
            .Where(tag =>
                tag.StartsWith("Magic", StringComparison.Ordinal) ||
                tag.StartsWith("Physical", StringComparison.Ordinal))
            .Select(Spaced);

    /// <summary>
    /// The attributes a scaling list names, in the words the game prints where it prints one and
    /// spelled out where it does not.
    ///
    /// 3,081 public rows carry a scaling list and between them name 82 attributes, of which 15 sit
    /// in the shipped attribute table: Strength on 1,145 rows, Intellect on 487, Critical Strike,
    /// Move Speed and eleven more. The other 67 are DamageScale, IncomingDamageMultiplier, MaxHealth
    /// and the like, the tuning knobs behind a number rather than a stat anybody wears, and they
    /// used to be dropped: the flag counted 2,166 rows instead of 3,081, and the 915 rows scaling
    /// off none of the named 15 offered a reader a filter that then drew them an empty panel.
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
    /// The word a rarity rung is filed under, which is what the palette keys its colour by while the
    /// export writes the word the rung prints.
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
    /// What a reader is shown for a table. Kept as the name every razor page already reaches for,
    /// answered by <see cref="PublicTypes"/>, which is where the decision now lives.
    /// </summary>
    public static string Label(string? table) => PublicTypes.Label(table);

    /// <summary>A number as the browser prints it: no trailing zeros, invariant.</summary>
    public static string Number(double value) =>
        Math.Round((decimal)value, 3).Normalize().ToString(CultureInfo.InvariantCulture);
}
