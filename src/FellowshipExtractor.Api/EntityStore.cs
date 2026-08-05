using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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
/// An id does not always name one thing. 207 of the 13,083 ids in the build expand into several
/// entries, up to twelve, because one item reads as a different item per armour type and primary
/// stat: id 5146 is a Webbed Robe in cloth, a Tribal Hauberk in leather and a Venom Pitted
/// Chestpiece in plate. A hero settles it, and settles all 207, because a hero fixes both
/// discriminators at once. Without one the request is genuinely ambiguous and is answered as such
/// rather than by picking a variant and hoping.
/// </summary>
public sealed class EntityStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<(string Table, int NetId), Entity[]> byId = new();

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
    /// row of its own here: id 5146 is three rows because it is three differently named items, and
    /// a browser that showed one of them would be hiding two real entries.
    /// </summary>
    public IReadOnlyList<Entity> Entities { get; }

    /// <summary>
    /// The tag tables the export contains, largest first. Derived rather than listed, so a table the
    /// extractor starts exporting appears in the browser's type tabs without being named here.
    /// </summary>
    public IReadOnlyList<string> Tables { get; }

    /// <summary>The stat names an item slot can hold, as a caller spells them.</summary>
    public StatParameters Stats { get; }

    /// <summary>The three attributes a wearer brings to an item, which draw on the primary pool.</summary>
    public IReadOnlySet<string> PrimaryAttributes { get; }

    public int Count { get; }

    /// <summary>
    /// How many of those rows a reader can reach, which is what every surface stating a corpus size
    /// states. 757 of the 15,235 rows sit in the 12 tables <see cref="PublicTypes"/> files no slug
    /// for, and a footer counting them told a reader about entries no page, tab, facet or search can
    /// answer with.
    /// </summary>
    public int PublicCount { get; }

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

        var groups = new Dictionary<(string, int), List<Entity>>();
        var stats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spelled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<Entity>();

        foreach (var line in File.ReadLines(paths.Entities))
        {
            if (line.Length == 0) continue;

            var entity = JsonSerializer.Deserialize<Entity>(line, Options);
            if (entity?.Table is not { } table || entity.NetId is not { } netId) continue;

            if (!groups.TryGetValue((table, netId), out var list)) groups[(table, netId)] = list = [];
            list.Add(entity);
            entities.Add(entity);

            foreach (var slot in entity.Stats ?? []) stats.Add(slot.Stat);

            Census(entity, spelled);

            if (PublicTypes.IsPublic(table)) PublicCount++;

            Count++;
        }

        foreach (var (key, list) in groups) byId[key] = [.. list];

        Entities = entities;
        Stats = new StatParameters(stats, Vocabulary.StatCalculation.Attributes);

        Tables =
        [
            .. entities.GroupBy(e => e.Table!, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key),
        ];

        var rows = Vocabulary.StatCalculation.Attributes;
        var named = rows.Count(row => row.Value.DisplayName is { Length: > 0 });
        var unnamed = spelled.Count(key => !rows.ContainsKey(key));

        logger.LogInformation(
            "Export loaded from {Path}: {Entities} entities under {Ids} ids, {Ambiguous} of which read differently per hero, " +
            "{Public} of them public. " +
            "{Heroes} heroes, {Rarities} rarity rungs, {Formats} print formats, {Colors} colours, {Styles} text styles, {Stats} stat names",
            paths.Export, Count, byId.Count, byId.Count(e => e.Value.Length > 1), PublicCount, Vocabulary.Heroes.Count,
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
    /// Every attribute name one row spells, over the three fields that carry one: the stat slots an
    /// item rolls, the scaling list a description's numbers grow with, and the tuning row behind each
    /// {Token}. The census is what the startup log counts the build's naming against.
    ///
    /// The rows nested inside a row are not walked, because they name nothing new: the 200 granted
    /// abilities and 111 legendary stat lines spell no attribute across these three fields that the
    /// row carrying them does not already spell, so the census is the same 82 names either way.
    /// </summary>
    private static void Census(Entity entity, HashSet<string> spelled)
    {
        foreach (var slot in entity.Stats ?? []) spelled.Add(slot.Stat);
        foreach (var attribute in entity.ScalesOff ?? []) spelled.Add(attribute);

        foreach (var token in entity.Placeholders?.Values ?? [])
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
    public IReadOnlyList<Entity> All(string table, int netId) =>
        byId.TryGetValue((table, netId), out var found) ? found : [];

    /// <summary>
    /// The one reading of an id a caller means. A hero narrows on the two things an entry is split
    /// by and on the hero list an entry names, which between them leave at most one entry in every
    /// case the build contains.
    /// </summary>
    public Lookup Resolve(string table, int netId, string? heroName, out Entity? entity, out HeroEntry? hero)
    {
        entity = null;
        hero = Hero(heroName);

        var candidates = All(table, netId);
        if (candidates.Count == 0) return Lookup.NoSuchId;

        if (candidates.Count == 1)
        {
            entity = candidates[0];
            return Lookup.Found;
        }

        if (hero is null) return heroName is null ? Lookup.NeedsHero : Lookup.NotForThatHero;

        var named = hero;
        var matches = candidates.Where(e => Reads(e, named)).ToArray();

        if (matches.Length == 0) return Lookup.NotForThatHero;

        entity = matches[0];
        return Lookup.Found;
    }

    /// <summary>
    /// Whether one reading of an id is the one a hero sees, and the only place that question is
    /// answered.
    ///
    /// A line that names its heroes has already answered it, so nothing else is asked. The armour
    /// type and the primary stat on such a line describe the item as that hero wears it rather than
    /// narrowing which hero it is for, and demanding they equal the hero's own is how every dynamic
    /// cloak used to answer 404 for nine heroes in twelve: a cloak is Cloth for everyone who wears
    /// one, so the three Cloth heroes matched and Mail, Leather and Plate heroes were turned away
    /// from their own item.
    ///
    /// The two remaining tests are for the lines that name no hero at all, where the pair is the
    /// only discriminator there is.
    ///
    /// Public because the page and the endpoint both ask it and disagreeing is what they did. The
    /// browser carried its own copy without the first clause, so items 5127 to 5144 - eighteen
    /// Back-slot cloaks whose twelve readings are every one of them Cloth - matched no hero at all:
    /// the page drew a hero picker with nobody picked, printed no line saying whose tooltip was on
    /// screen, and advertised a tooltip address that answered 400 for want of the hero it had just
    /// declined to name.
    /// </summary>
    public static bool Reads(Entity entity, HeroEntry hero) =>
        entity.Heroes is { } heroes
            ? heroes.Contains(hero.Name)
            : (entity.ArmorType is null || entity.ArmorType == hero.ArmorType) &&
              (entity.PrimaryStat is null || entity.PrimaryStat == hero.PrimaryStat);

    /// <summary>
    /// The hero a reading is drawn for when the caller named none: the first of the roster who sees
    /// it, in the order the build lists them.
    ///
    /// Every one of the 2,143 readings behind the 188 split ids names exactly one hero, so on the
    /// ids this is asked about the answer is that hero rather than a choice between several. A
    /// reading naming none falls to the armour and stat pair, which is why this can still answer for
    /// the 42 single-reading items that name a roster without splitting by it.
    /// </summary>
    public HeroEntry? Wearer(Entity entity) =>
        Vocabulary.Heroes.FirstOrDefault(hero => Reads(entity, hero));

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
