using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

public sealed record StatCalculationFile
{
    public PrimaryStatPool? PrimaryPool { get; init; }
    public SecondaryStatPool? SecondaryPool { get; init; }

    public double StatRarityBase { get; init; }

    public SlotStatSettings BaseSlot { get; init; } = new();
    public SlotStatSettings FixedSlot { get; init; } = new();
    public SlotStatSettings DynamicSlot { get; init; } = new();

    public IReadOnlyDictionary<string, SlotWeight> SlotWeights { get; init; } =
        new Dictionary<string, SlotWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AttributeDisplay> Attributes { get; init; } =
        new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> RandomStatPool { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityAdditions { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(StringComparer.OrdinalIgnoreCase);
}

public sealed record VocabularyFile
{
    public IReadOnlyList<HeroEntry> Heroes { get; init; } = [];

    public IReadOnlyList<RarityEntry> Rarities { get; init; } = [];

    public IReadOnlyDictionary<string, string> PrintFormats { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Colors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TextStyle> TextStyles { get; init; } =
        new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> SlotIcons { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<TemperCeiling>> MaxTempers { get; init; } =
        new Dictionary<string, IReadOnlyList<TemperCeiling>>(StringComparer.OrdinalIgnoreCase);

    public StatCalculationFile StatCalculation { get; init; } = new();
}

public enum Lookup
{
    Found,
    NoSuchId,

    NeedsHero,

    NotForThatHero,
}

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

    public IReadOnlyList<string> Names { get; }

    public string? Resolve(string parameter) =>
        byName.TryGetValue(Compact(parameter), out var stat) ? stat : null;

    public AttributeDisplay Row(string stat) => byStat[stat];

    private static string Compact(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal);
}

public sealed class EntityStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<(string Table, int NetId), Entity[]> byId = new();

    private readonly Dictionary<string, HeroEntry> heroesByName = new(StringComparer.OrdinalIgnoreCase);

    public VocabularyFile Vocabulary { get; }

    public IReadOnlyList<Entity> Entities { get; }

    public IReadOnlyList<string> Tables { get; }

    public StatParameters Stats { get; }

    public IReadOnlySet<string> PrimaryAttributes { get; }

    public int Count { get; }

    public int PublicCount { get; }

    public EntityStore(ExportPaths paths, ILogger<EntityStore> logger)
    {
        var file = JsonSerializer.Deserialize<VocabularyFile>(File.ReadAllText(paths.Vocabulary), Options)
            ?? throw new InvalidOperationException($"{paths.Vocabulary} deserialised to nothing.");

        Vocabulary = file with
        {
            TextStyles = new Dictionary<string, TextStyle>(file.TextStyles, StringComparer.OrdinalIgnoreCase),
            Colors = new Dictionary<string, string>(file.Colors, StringComparer.OrdinalIgnoreCase),
            SlotIcons = new Dictionary<string, string>(file.SlotIcons, StringComparer.OrdinalIgnoreCase),
            MaxTempers = new Dictionary<string, IReadOnlyList<TemperCeiling>>(
                file.MaxTempers, StringComparer.OrdinalIgnoreCase),
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

        logger.LogInformation(
            "Attribute names: {Named} of {Attributes} shipped rows print a word, and {Unnamed} of the {Spelled} attribute " +
            "names the export spells have no row, so they print spaced out",
            named, rows.Count, unnamed, spelled.Count);
    }

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

    public string StatName(string key) =>
        Vocabulary.StatCalculation.Attributes.TryGetValue(key, out var attribute) &&
        attribute.DisplayName is { Length: > 0 } printed
            ? printed
            : Codex.Spaced(key);

    public AttributeDisplay? StatDisplay(string? key) =>
        key is not null && Vocabulary.StatCalculation.Attributes.TryGetValue(key, out var attribute)
            ? attribute
            : null;

    public string StatValue(string? key, double value)
    {
        if (StatDisplay(key) is not { } display) return Codex.Number(value);

        var rounded = Math.Round((decimal)value, display.RoundToDecimals, MidpointRounding.AwayFromZero);

        return rounded.Normalize().ToString(CultureInfo.InvariantCulture) +
            (display.IsPercent ? "%" : string.Empty);
    }

    public HeroEntry? Hero(string? name) =>
        name is not null && heroesByName.TryGetValue(name, out var hero) ? hero : null;

    public IReadOnlyList<Entity> All(string table, int netId) =>
        byId.TryGetValue((table, netId), out var found) ? found : [];

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

    public static bool Reads(Entity entity, HeroEntry hero) =>
        entity.Heroes is { } heroes
            ? heroes.Contains(hero.Name)
            : (entity.ArmorType is null || entity.ArmorType == hero.ArmorType) &&
              (entity.PrimaryStat is null || entity.PrimaryStat == hero.PrimaryStat);

    public HeroEntry? Wearer(Entity entity) =>
        Vocabulary.Heroes.FirstOrDefault(hero => Reads(entity, hero));

    public RarityEntry? Rarity(string? word, bool gem)
    {
        if (word is null) return null;

        if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            return Vocabulary.Rarities.FirstOrDefault(r => r.Tier == tier);
        }

        return Vocabulary.Rarities.FirstOrDefault(r =>
            string.Equals(gem ? r.GemDisplayName : r.DisplayName, word, StringComparison.OrdinalIgnoreCase));
    }
}
