using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api;

/// <summary>Why an id did not produce exactly one tooltip.</summary>
public enum Lookup
{
    Found,
    NoSuchId,

    /// <summary>The id exists but reads differently per hero and the caller named none.</summary>
    NeedsHero,

    /// <summary>The id exists and has no reading for the hero the caller named.</summary>
    NotForThatHero,
}

/// <summary>
/// The stat names a tooltip request may carry, and the name each one is filed under inside the
/// build.
///
/// A caller writes the stat the way the game writes it. The build's own spelling used to be the only
/// one accepted, so ?CritChance=99 answered 200 with a line reading "+99 Critical Strike" while
/// ?CriticalStrike=99 answered 400 and the sentence listing the parameters printed the internal
/// spelling to say so - the same failure a hero's codename and a rarity's filed word were withdrawn
/// for. The printed name resolves and nothing else does.
///
/// Whitespace is ignored on the way in, because a query string is a poor place for a space and
/// "Critical Strike" is two words: CriticalStrike, critical%20strike and Critical+Strike are one
/// parameter. Seven of the eight names are a single word, where the printed and the filed spelling
/// are the same string and nothing changes for a caller.
/// </summary>
public sealed class StatParameters
{
    private readonly IReadOnlyDictionary<string, string> byName;
    private readonly IReadOnlyDictionary<string, AttributeDisplay> byStat;

    public StatParameters(IEnumerable<string> stats, IReadOnlyDictionary<string, AttributeDisplay> attributes)
    {
        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rows = new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);
        var printed = new List<string>();

        foreach (var stat in stats)
        {
            // A stat the shipped attribute table has no row for has no printed name to be asked for
            // by, and asking for it by the build's name is the thing being closed, so it is not a
            // parameter at all. A row carrying no word is the same case rather than a lesser one:
            // registering it would make the empty string a working address. All 8 stats an item slot
            // can hold have a row and all 24 rows print a word, so nothing is turned away today.
            if (!attributes.TryGetValue(stat, out var attribute) ||
                attribute.DisplayName is not { Length: > 0 })
            {
                continue;
            }

            named[Compact(attribute.DisplayName)] = stat;
            rows[stat] = attribute;
            printed.Add(attribute.DisplayName);
        }

        byName = named;
        byStat = rows;
        Names = [.. printed.Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The printed names, which is what the sentence listing the parameters says.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// The stat a caller's parameter names, or null when it names none. The answer is the build's
    /// spelling, because that is what an item's slots, a token's attribute and the magnitude
    /// arithmetic are all keyed by.
    /// </summary>
    public string? Resolve(string parameter) =>
        byName.TryGetValue(Compact(parameter), out var stat) ? stat : null;

    /// <summary>
    /// The shipped attribute row behind a stat, which is what says whether the number a caller sends
    /// is a rating and what it converts to. A stat is registered only where the table has a row for
    /// it, so anything <see cref="Resolve"/> answered has one.
    /// </summary>
    public AttributeDisplay Row(string stat) => byStat[stat];

    private static string Compact(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal);
}

/// <summary>
/// The export, read once at startup and indexed by the two things a route carries.
///
/// An id does not always name one thing. 207 of the 10,285 ids in the build expand into several
/// entries, up to twelve. 188 of them are items that read as a different item per armour type and
/// primary stat: id 5146 is a Webbed Robe in cloth, a Tribal Hauberk in leather, a Skittershard
/// Bound Chestpiece in mail and a Venom Pitted Chestpiece in plate, across its twelve readings. A
/// hero settles every one of the 188, because a hero fixes both discriminators at once. Without
/// one the request is genuinely ambiguous and is answered as such rather than by picking a
/// variant and hoping.
///
/// The other 19 are talent slots, not items: <see cref="TalentSlot"/> numbers a position in a
/// hero's own grid, and every hero's grid reuses that same small pool of numbers for its own
/// eighteen slots, so the id collides across all twelve heroes rather than naming one slot twice.
/// A hero does not settle these - <see cref="Reads"/> answers true for every hero on a document
/// that is not an <see cref="Item"/> - so a caller narrows a talent slot by hero and position
/// rather than by this id.
/// </summary>
public sealed class EntityStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new EntityDocumentConverter() },
    };

    private readonly Dictionary<(Fellowship.SDK.EntityType Type, int Id), EntityDocument[]> byId = new();

    /// <summary>
    /// The roster by the name the game prints, and by that name alone.
    ///
    /// A hero's codename is the spelling their asset files are named by, and Vigour's are filed under
    /// Vigor. Registering those beside the printed names made every one of them a working address:
    /// ?hero=Bowguy answered 200 with the tooltip ?hero=Elarion answers, byte for byte, while the
    /// browse filter reading the same corpus answered 0 rows for the same word. An internal spelling
    /// that resolves is one a reader can find, so it resolves nowhere and the two surfaces agree.
    /// </summary>
    private readonly Dictionary<string, HeroEntry> heroesByName = new(StringComparer.OrdinalIgnoreCase);

    public VocabularyFile Vocabulary { get; }

    /// <summary>
    /// Every line of the export in the order it was written, which is the order the browser
    /// paginates and the order the search index numbers its documents by. A reading of an id is a
    /// row of its own here: id 5146 is twelve rows, one per hero, spelling four differently named
    /// items (Webbed Robe in cloth, Tribal Hauberk in leather, Skittershard Bound Chestpiece in
    /// mail, Venom Pitted Chestpiece in plate), and a browser that showed one of them would be
    /// hiding eleven real entries.
    /// </summary>
    public IReadOnlyList<EntityDocument> Entities { get; }

    /// <summary>
    /// The document types the export contains, largest first then by slug. Derived rather than
    /// listed, so a type the extractor starts writing appears in the browser's type tabs without
    /// being named here.
    /// </summary>
    public IReadOnlyList<Fellowship.SDK.EntityType> Types { get; }

    /// <summary>The stat names an item slot can hold, as a caller spells them.</summary>
    public StatParameters Stats { get; }

    /// <summary>The three attributes a wearer brings to an item, which draw on the primary pool.</summary>
    public IReadOnlySet<string> PrimaryAttributes { get; }

    /// <summary>
    /// How many rows the export holds. The drop rules the extractor applies already keep a row a
    /// reader cannot meet out of the export, so every row here is one a reader can reach and this is
    /// the whole of what a surface stating a corpus size states.
    /// </summary>
    public int Count { get; }

    public EntityStore(ExportPaths paths, ILogger<EntityStore> logger)
    {
        var file = JsonSerializer.Deserialize<VocabularyFile>(File.ReadAllText(paths.Vocabulary), Options)
            ?? throw new InvalidOperationException($"{paths.Vocabulary} deserialised to nothing.");

        // Deserialisation builds ordinal dictionaries whatever the declared initialiser says, and
        // the tags a description writes do not agree with the style table on case: the table files
        // every style lower case while the corpus contains rt.Absorb and rt.MeikoAbility3. Rebuilt
        // case insensitively so those five occurrences resolve rather than falling to unstyled.
        Vocabulary = file with
        {
            TextStyles = new Dictionary<string, TextStyle>(file.TextStyles, StringComparer.OrdinalIgnoreCase),
            Colors = new Dictionary<string, string>(file.Colors, StringComparer.OrdinalIgnoreCase),
            StatCalculation = file.StatCalculation with
            {
                SlotWeights = new Dictionary<string, SlotWeight>(
                    file.StatCalculation.SlotWeights, StringComparer.OrdinalIgnoreCase),
                Attributes = new Dictionary<string, AttributeDisplay>(
                    file.StatCalculation.Attributes, StringComparer.OrdinalIgnoreCase),
                RarityAdditions = new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(
                    file.StatCalculation.RarityAdditions, StringComparer.OrdinalIgnoreCase),
            },
        };

        var attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hero in Vocabulary.Heroes)
        {
            heroesByName[hero.Name] = hero;
            attributes.Add(hero.PrimaryStat);
        }

        // The attributes a wearer's own slot can hold, which is what tells the two stat pools
        // apart. Taken from the heroes rather than listed here, because the heroes are the whole
        // of what the build allows in that slot.
        PrimaryAttributes = attributes;

        var groups = new Dictionary<(Fellowship.SDK.EntityType, int), List<EntityDocument>>();
        var stats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spelled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<EntityDocument>();

        foreach (var line in File.ReadLines(paths.Entities))
        {
            if (line.Length == 0) continue;

            var document = JsonSerializer.Deserialize<EntityDocument>(line, Options)
                ?? throw new InvalidOperationException($"A line of {paths.Entities} deserialised to nothing.");

            var key = (EntityTypes.TypeOf(document), document.Id);

            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            list.Add(document);
            entities.Add(document);

            Census(document, stats, spelled);

            Count++;
        }

        foreach (var (key, list) in groups) byId[key] = [.. list];

        Entities = entities;
        Stats = new StatParameters(stats, Vocabulary.StatCalculation.Attributes);

        Types =
        [
            .. entities.GroupBy(EntityTypes.TypeOf)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => EntityTypes.Slug(g.Key), StringComparer.Ordinal)
                .Select(g => g.Key),
        ];

        var rows = Vocabulary.StatCalculation.Attributes;
        var named = rows.Count(row => row.Value.DisplayName is { Length: > 0 });
        var unnamed = spelled.Count(key => !rows.ContainsKey(key));

        logger.LogInformation(
            "Export loaded from {Path}: {Entities} entities under {Ids} ids, {Ambiguous} of which read differently per hero. " +
            "{Heroes} heroes, {Rarities} rarity rungs, {Formats} print formats, {Colors} colours, {Styles} text styles, {Stats} stat names",
            paths.Export, Count, byId.Count, byId.Count(e => e.Value.Length > 1), Vocabulary.Heroes.Count,
            Vocabulary.Rarities.Count, Vocabulary.PrintFormats.Count, Vocabulary.Colors.Count,
            Vocabulary.TextStyles.Count, Stats.Names.Count);

        // The two halves of the naming, logged rather than assumed, because both are facts about a
        // build rather than about this code and both go stale silently. The first says how much of
        // the shipped table is usable: 24 of 24 rows print a word today, and a row that stops
        // carrying one takes its stat back to the run together spelling with nothing on screen
        // saying so. The second says how far the table reaches: the export spells 82 attribute names
        // and 67 of them sit in no row at all, so a jump in that number is the build renaming
        // something rather than this app losing it.
        logger.LogInformation(
            "Attribute names: {Named} of {Attributes} shipped rows print a word, and {Unnamed} of the {Spelled} attribute " +
            "names the export spells have no row, so they print spaced out",
            named, rows.Count, unnamed, spelled.Count);
    }

    /// <summary>
    /// Cached by document runtime type and property name, so the reflection <see cref="Field{T}"/>
    /// runs only once per document class per field rather than once per row.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Fields = new();

    /// <summary>
    /// One list-valued field, read off a document by its own runtime type rather than by a
    /// hand-kept list of which document classes carry it. A document class with no property of that
    /// name, or one of a different shape, contributes nothing rather than throwing.
    /// </summary>
    private static IReadOnlyList<T> Field<T>(EntityDocument document, string name) =>
        Fields.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property))
            ?.GetValue(document) as IReadOnlyList<T> ?? [];

    /// <summary>
    /// Every attribute name one row spells, over the three fields that carry one: the stat slots an
    /// item rolls, the scaling list a description's numbers grow with, and the tuning row behind each
    /// {Token}. The census is what the startup log counts the build's naming against.
    ///
    /// Read off each document's own runtime type rather than off one shape. Only <see cref="Item"/>
    /// carries <see cref="Item.Stats"/>; a smaller family of document types carries a
    /// <c>ScalesOff</c> list and, on most of those, a <c>Placeholders</c> list besides.
    /// <see cref="Field{T}"/> asks a document's own runtime type for the property by name rather
    /// than working from a list of which types carry it, so a document type that gains one of these
    /// fields is counted from the day it ships rather than the day someone remembers to add it here.
    ///
    /// Nothing here is nested to skip. An item names the power it grants and the legendary stats it
    /// carries by id - <see cref="Item.GrantsId"/> and <see cref="Item.LegendaryStatIds"/> - and the
    /// line behind each id is exported on its own, which this walks when it reaches that line rather
    /// than through the item pointing at it. <see cref="GemType.OverflowPlaceholders"/> is the one
    /// token-bearing field this does not walk, matching this store's predecessor, which never
    /// counted a gem type's overflow sentence either.
    ///
    /// Measured over the current export: 82 attribute names.
    /// </summary>
    private static void Census(EntityDocument document, HashSet<string> stats, HashSet<string> spelled)
    {
        foreach (var slot in Field<StatSlot>(document, nameof(Item.Stats)))
        {
            stats.Add(slot.Stat);
            spelled.Add(slot.Stat);
        }

        foreach (var attribute in Field<string>(document, nameof(Ability.ScalesOff))) spelled.Add(attribute);

        foreach (var token in Field<Placeholder>(document, nameof(Ability.Placeholders)))
        {
            if (token.Attribute is { } attribute) spelled.Add(attribute);
            foreach (var scaling in token.ScalesOff ?? []) spelled.Add(scaling);
        }
    }

    /// <summary>
    /// The word the game prints for a stat key, or the key spaced out where the build names no
    /// display form for it.
    ///
    /// The shipped table names 24 attributes and prints a word for all 24, which is why nothing here
    /// spells a stat itself: CritChance reads "Critical Strike", Mana reads "Max Mana", and a
    /// renderer working the words out from the key would print names no player has been shown.
    ///
    /// The 67 keys with no row are the reason this stopped answering null. The export spells 82
    /// attribute names across its stat slots, its scaling lists and its tuning tokens, and the 15 in
    /// the table are the ones an item rolls; the other 67 are DamageScale,
    /// IncomingDamageMultiplier, PrimaryAttributeFlatBonus and the like, tuning knobs a reader came
    /// to a database like this to read. Dropping them hid data; what has to stay hidden is the run
    /// together spelling, and that is exactly what <see cref="Codex.Spaced"/> takes off. A caller
    /// that still has to tell a named attribute from an unnamed one asks
    /// <see cref="StatDisplay"/> instead.
    /// </summary>
    public string StatName(string key) =>
        Vocabulary.StatCalculation.Attributes.TryGetValue(key, out var attribute) &&
        attribute.DisplayName is { Length: > 0 } printed
            ? printed
            : Codex.Spaced(key);

    /// <summary>
    /// The shipped row behind a stat key, and null where the build names none.
    ///
    /// The one way left to ask whether the game has a word of its own for an attribute, now that
    /// <see cref="StatName"/> always answers with something. The row is also what
    /// <see cref="StatValue"/> reads for its decimals and its percent sign, so the lookup is written
    /// once and a caller wanting both makes one call.
    /// </summary>
    public AttributeDisplay? StatDisplay(string? key) =>
        key is not null && Vocabulary.StatCalculation.Attributes.TryGetValue(key, out var attribute)
            ? attribute
            : null;

    /// <summary>
    /// A magnitude written the way the shipped attribute row says to write it: rounded to that row's
    /// decimals and carrying its percent sign.
    ///
    /// The number is taken as the game's own display unit rather than as a fraction, which is what
    /// separates a magnitude from a coefficient: the CritChance slot on item 5202 works out to 13
    /// and the tooltip reads "+13 Critical Strike", where a token's 0.4 coefficient is printed as
    /// "40% of Intellect". Multiplying by a hundred here would restate a number the arithmetic has
    /// already put in the units the row is written for.
    ///
    /// All 8 stats an item slot can hold round to whole numbers and none of them is a percent, so
    /// both fields exist for the other 16 rows: Dodge, Parry and Move Speed keep 4 places and print
    /// a sign, Health Regen Rate and Mana Regen Rate keep 2.
    ///
    /// A key the build states no precision for falls to the app's own number convention rather than
    /// to nought decimals, because rounding one whole is not a poorer reading of it but a different
    /// number: the 67 tuning knobs with no row are coefficients and multipliers around one, so
    /// DamageScale 1.5 would print as 2 and IncomingDamageMultiplier 0.85 as 1.
    /// </summary>
    public string StatValue(string? key, double value)
    {
        if (StatDisplay(key) is not { } display) return Codex.Number(value);

        var rounded = Math.Round((decimal)value, display.RoundToDecimals, MidpointRounding.AwayFromZero);

        return rounded.Normalize().ToString(CultureInfo.InvariantCulture) +
            (display.IsPercent ? "%" : string.Empty);
    }

    /// <summary>The hero a name names, or null. The name the game prints, never the codename.</summary>
    public HeroEntry? Hero(string? name) =>
        name is not null && heroesByName.TryGetValue(name, out var hero) ? hero : null;

    /// <summary>Every reading of an id, in export order, or empty when the build has no such id.</summary>
    public IReadOnlyList<EntityDocument> All(Fellowship.SDK.EntityType type, int id) =>
        byId.TryGetValue((type, id), out var found) ? found : [];

    /// <summary>
    /// The one reading of an id a caller means. A hero narrows on the two things an
    /// <see cref="Item"/> entry is split by and on the hero list it names, which between them leave
    /// at most one entry on every one of the 188 item ids that split. <see cref="Reads"/> answers
    /// true for every hero on anything that is not an <see cref="Item"/>, so on the 19 talent-slot
    /// ids that split nothing narrows and the first reading answers regardless of which hero, if
    /// any, the caller named.
    /// </summary>
    public Lookup Resolve(
        Fellowship.SDK.EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero)
    {
        document = null;
        hero = Hero(heroName);

        var candidates = All(type, id);
        if (candidates.Count == 0) return Lookup.NoSuchId;

        if (candidates.Count == 1)
        {
            document = candidates[0];
            return Lookup.Found;
        }

        if (hero is null) return heroName is null ? Lookup.NeedsHero : Lookup.NotForThatHero;

        var named = hero;
        var matches = candidates.Where(e => Reads(e, named)).ToArray();

        if (matches.Length == 0) return Lookup.NotForThatHero;

        document = matches[0];
        return Lookup.Found;
    }

    /// <summary>
    /// Whether one reading of an id is the one a hero sees, and the only place that question is
    /// answered.
    ///
    /// Only <see cref="Item"/> carries the three fields this tests; a document of any other type
    /// reads for every hero, because nothing else in the corpus reads differently by armour type,
    /// primary stat or wearer.
    ///
    /// A line that names its heroes has already answered it, so nothing else is asked.
    /// <see cref="Item.Heroes"/> is the field tested, not <see cref="Item.Hero"/>:
    /// <see cref="Item.Hero"/> is null on exactly the rows this clause exists for, the ones naming
    /// several heroes at once. Item 2055, the Expedition Cloak, names Elarion, Meiko and Mara
    /// together with no singular <see cref="Item.Hero"/> and an armour type of Cloth, which is none
    /// of the three heroes' own (Mail, Leather, Leather); testing <see cref="Item.Hero"/> here would
    /// fall through to the armour pair below and match none of them.
    ///
    /// The armour type and the primary stat on a line that names its heroes describe the item as
    /// that hero wears it rather than narrowing which hero it is for, and demanding they equal the
    /// hero's own is how every dynamic cloak used to answer 404 for nine heroes in twelve: a cloak
    /// is Cloth for everyone who wears one, so the three Cloth heroes matched and Mail, Leather and
    /// Plate heroes were turned away from their own item.
    ///
    /// The two remaining tests are for the lines that name no hero at all, where the pair is the
    /// only discriminator there is. The hero's own words are parsed through the same generated
    /// enums the document carries, so a vocabulary spelling that no longer names a member fails
    /// loudly rather than comparing two strings that have quietly drifted apart.
    ///
    /// Public because the page and the endpoint both ask it and disagreeing is what they did. The
    /// browser carried its own copy without the first clause, so items 5127 to 5144 - eighteen
    /// Back-slot cloaks whose twelve readings are every one of them Cloth - matched no hero at all:
    /// the page drew a hero picker with nobody picked, printed no line saying whose tooltip was on
    /// screen, and advertised a tooltip address that answered 400 for want of the hero it had just
    /// declined to name.
    /// </summary>
    public static bool Reads(EntityDocument document, HeroEntry hero) =>
        document is not Item item ||
        (item.Heroes is { } heroes
            ? heroes.Contains(ParseHero(hero.Name))
            : (item.ArmorType is null || item.ArmorType == ParseArmorType(hero.ArmorType)) &&
              (item.PrimaryStat is null || item.PrimaryStat == ParsePrimaryStat(hero.PrimaryStat)));

    private static Hero ParseHero(string name) =>
        Enum.TryParse<Hero>(name, out var hero) ? hero : throw new FormatException($"'{name}' names no {nameof(Hero)}.");

    private static ArmorType? ParseArmorType(string? name) =>
        name is null ? null
            : Enum.TryParse<ArmorType>(name, out var armorType) ? armorType
            : throw new FormatException($"'{name}' names no {nameof(ArmorType)}.");

    private static PrimaryStat ParsePrimaryStat(string name) =>
        Enum.TryParse<PrimaryStat>(name, out var primaryStat)
            ? primaryStat
            : throw new FormatException($"'{name}' names no {nameof(PrimaryStat)}.");

    /// <summary>
    /// The hero a reading is drawn for when the caller named none: the first of the roster who sees
    /// it, in the order the build lists them.
    ///
    /// Every one of the 2,143 readings behind the 188 split item ids names exactly one hero, so on
    /// those ids the answer is that hero rather than a choice between several. A reading naming
    /// none falls to the armour and stat pair, which is why this can still answer for the 42
    /// single-reading items that name a roster without splitting by it.
    ///
    /// Meaningful for items only. On a document that is not an <see cref="Item"/>,
    /// <see cref="Reads"/> answers true for every hero, so this answers with whichever hero the
    /// roster lists first rather than the one the row is actually for.
    /// </summary>
    public HeroEntry? Wearer(EntityDocument document) =>
        Vocabulary.Heroes.FirstOrDefault(hero => Reads(document, hero));

    /// <summary>
    /// The rarity rung a caller's word names, out of the words the game prints. A rung has more than
    /// one printed word: the ordinal the build files as Champion prints as "Heroic" on an item and
    /// "Flawless" on a gem, so the word alone does not identify a rung, and which column answers
    /// depends on what the caller is asking about rather than on the word.
    /// </summary>
    public RarityEntry? Rarity(string? word, bool gem)
    {
        if (word is null) return null;

        // A rung's ordinal answers for the rung whichever column its words fall in, which is what a
        // caller reaches for when they do not know which column they are asking about: "Heroic" is
        // rung 4's word on an item and rung 5's on a gem, so that one word names two rungs across the
        // two columns while the number names one.
        if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            return Vocabulary.Rarities.FirstOrDefault(r => r.Tier == tier);
        }

        // The printed word, and no other. The word the build files a rung under used to be accepted
        // after it, which made ?rarity=Champion a working address for the rung a player is shown as
        // Heroic: an internal spelling that resolves is one a reader can find, and this one answered
        // with a rung printing a word other than the one asked for. Withdrawing it costs no rung,
        // because within a column every printed word names exactly one, so an item is asked for by
        // Heroic or Regal, a gem by Flawless or Heroic, and either by tier.
        return Vocabulary.Rarities.FirstOrDefault(r =>
            string.Equals(gem ? r.GemDisplayName : r.DisplayName, word, StringComparison.OrdinalIgnoreCase));
    }
}
