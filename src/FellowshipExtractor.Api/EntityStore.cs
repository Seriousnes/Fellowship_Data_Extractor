using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// Everything the build states about turning an item level into stat magnitudes, as the sidecar
/// carries it. Every number here is shipped; the arithmetic that puts them together is
/// <see cref="StatMagnitudes"/>.
/// </summary>
public sealed record StatCalculationFile
{
    public PrimaryStatPool? PrimaryPool { get; init; }
    public SecondaryStatPool? SecondaryPool { get; init; }

    /// <summary>
    /// One, in this build. A rarity does not scale a stat's magnitude; it grants extra slots.
    /// </summary>
    public double StatRarityBase { get; init; }

    public SlotStatSettings BaseSlot { get; init; } = new();
    public SlotStatSettings FixedSlot { get; init; } = new();
    public SlotStatSettings DynamicSlot { get; init; } = new();

    /// <summary>What each equipment position is worth, keyed by the slot name the game prints.</summary>
    public IReadOnlyDictionary<string, SlotWeight> SlotWeights { get; init; } =
        new Dictionary<string, SlotWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AttributeDisplay> Attributes { get; init; } =
        new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The stats a slot rolled at random can land on.</summary>
    public IReadOnlyList<string> RandomStatPool { get; init; } = [];

    /// <summary>The slots each rarity adds, keyed by the rule set an item names and then by tier.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityAdditions { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The sidecar the extractor writes beside the entity stream, as it deserialises. The entry records
/// are the extractor's own, so a column added there arrives here without being restated; only the
/// envelope is declared, because the extractor's Vocabulary class builds itself from cooked game
/// files and cannot read its own output back.
/// </summary>
public sealed record VocabularyFile
{
    public IReadOnlyList<HeroEntry> Heroes { get; init; } = [];

    public IReadOnlyList<RarityEntry> Rarities { get; init; } = [];

    public IReadOnlyDictionary<string, string> PrintFormats { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The game's named colours, keyed as the shipped table keys them.</summary>
    public IReadOnlyDictionary<string, string> Colors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The rich text style rows, keyed by the tag a description writes: rt.warning.</summary>
    public IReadOnlyDictionary<string, TextStyle> TextStyles { get; init; } =
        new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase);

    public StatCalculationFile StatCalculation { get; init; } = new();
}

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
/// The export, read once at startup and indexed by the two things a route carries.
///
/// An id does not always name one thing. 168 of the 13,083 ids in the build expand into several
/// entries, up to twelve, because one item reads as a different item per armour type and primary
/// stat: id 5146 is a Webbed Robe in cloth, a Tribal Hauberk in leather and a Venom Pitted
/// Chestpiece in plate. A hero settles it, and settles all 168, because a hero fixes both
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
    private readonly Dictionary<string, HeroEntry> heroesByAnyName = new(StringComparer.OrdinalIgnoreCase);

    public VocabularyFile Vocabulary { get; }

    /// <summary>The stat names an item slot can hold, which are the ones a caller may send.</summary>
    public IReadOnlySet<string> KnownStats { get; }

    /// <summary>The three attributes a wearer brings to an item, which draw on the primary pool.</summary>
    public IReadOnlySet<string> PrimaryAttributes { get; }

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
            heroesByAnyName[hero.Name] = hero;
            heroesByAnyName[hero.Codename] = hero;
            attributes.Add(hero.PrimaryStat);
        }

        // The attributes a wearer's own slot can hold, which is what tells the two stat pools
        // apart. Taken from the heroes rather than listed here, because the heroes are the whole
        // of what the build allows in that slot.
        PrimaryAttributes = attributes;

        var groups = new Dictionary<(string, int), List<Entity>>();
        var stats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(paths.Entities))
        {
            if (line.Length == 0) continue;

            var entity = JsonSerializer.Deserialize<Entity>(line, Options);
            if (entity?.Table is not { } table || entity.NetId is not { } netId) continue;

            if (!groups.TryGetValue((table, netId), out var list)) groups[(table, netId)] = list = [];
            list.Add(entity);

            foreach (var slot in entity.Stats ?? []) stats.Add(slot.Stat);

            Count++;
        }

        foreach (var (key, list) in groups) byId[key] = [.. list];

        KnownStats = stats;

        logger.LogInformation(
            "Export loaded from {Path}: {Entities} entities under {Ids} ids, {Ambiguous} of which read differently per hero. " +
            "{Heroes} heroes, {Rarities} rarity rungs, {Formats} print formats, {Colors} colours, {Styles} text styles, {Stats} stat names",
            paths.Export, Count, byId.Count, byId.Count(e => e.Value.Length > 1), Vocabulary.Heroes.Count,
            Vocabulary.Rarities.Count, Vocabulary.PrintFormats.Count, Vocabulary.Colors.Count,
            Vocabulary.TextStyles.Count, KnownStats.Count);
    }

    public HeroEntry? Hero(string? name) =>
        name is not null && heroesByAnyName.TryGetValue(name, out var hero) ? hero : null;

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
        var matches = candidates.Where(e => Matches(e, named)).ToArray();

        if (matches.Length == 0) return Lookup.NotForThatHero;

        entity = matches[0];
        return Lookup.Found;
    }

    /// <summary>
    /// Whether one reading of an id is the one a hero sees.
    ///
    /// A line that names its heroes has already answered the question, so nothing else is asked. The
    /// armour type and the primary stat on such a line describe the item as that hero wears it
    /// rather than narrowing which hero it is for, and demanding they equal the hero's own is how
    /// every dynamic cloak used to answer 404 for nine heroes in twelve: a cloak is Cloth for
    /// everyone who wears one, so the three Cloth heroes matched and Mail, Leather and Plate heroes
    /// were turned away from their own item.
    ///
    /// The two remaining tests are for the lines that name no hero at all, where the pair is the
    /// only discriminator there is.
    /// </summary>
    private static bool Matches(Entity entity, HeroEntry hero) =>
        entity.Heroes is { } heroes
            ? heroes.Contains(hero.Name)
            : (entity.ArmorType is null || entity.ArmorType == hero.ArmorType) &&
              (entity.PrimaryStat is null || entity.PrimaryStat == hero.PrimaryStat);

    /// <summary>
    /// The rarity rung a caller's word names. A rung has more than one word: the ordinal the build
    /// files as Champion prints as "Heroic" on an item and "Flawless" on a gem, and the ordinal
    /// filed as Heroic prints as "Regal". So the word alone does not identify a rung, and which
    /// column answers depends on what the caller is asking about rather than on the word.
    /// </summary>
    public RarityEntry? Rarity(string? word, bool gem)
    {
        if (word is null) return null;

        // A rung's ordinal answers for the rung whichever column its words fall in, which is what
        // makes tier 5 sayable at all: "Heroic" is the printed name of rung 4 and the filed name of
        // rung 5, so the word cannot reach rung 5 and the number is the only way to ask for it.
        if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            return Vocabulary.Rarities.FirstOrDefault(r => r.Tier == tier);
        }

        var printed = Vocabulary.Rarities.FirstOrDefault(r =>
            string.Equals(gem ? r.GemDisplayName : r.DisplayName, word, StringComparison.OrdinalIgnoreCase));

        // The word the build files the rung under is accepted after that, for a caller reading the
        // export rather than the game. The printed word deliberately wins, because a caller types
        // what the game showed them, and that costs one word: "Heroic" is the printed name of rung
        // 4 and the filed name of rung 5, so it always means rung 4 here and rung 5 has to be asked
        // for as "Regal" or as tier 5. Every other word is unambiguous across both columns.
        return printed ?? Vocabulary.Rarities.FirstOrDefault(r =>
            string.Equals(r.Name, word, StringComparison.OrdinalIgnoreCase));
    }
}
