using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

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
    private const string DefaultDropRow = "Default";

    private const string LegendaryDropRow = "Legendary";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new EntityDocumentConverter() },
    };

    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Fields = new();

    private static IReadOnlyList<T> Field<T>(EntityDocument document, string name) =>
        Fields.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property))
            ?.GetValue(document) as IReadOnlyList<T> ?? [];

    private readonly Dictionary<(Fellowship.SDK.EntityType Type, int Id), EntityDocument[]> byId = new();

    private readonly Dictionary<string, HeroEntry> heroesByName = new(StringComparer.OrdinalIgnoreCase);

    public VocabularyFile Vocabulary { get; }

    public IReadOnlyList<EntityDocument> Entities { get; }

    public IReadOnlyList<Fellowship.SDK.EntityType> Types { get; }

    public StatParameters Stats { get; }

    public IReadOnlySet<string> PrimaryAttributes { get; }

    public int Count { get; }

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

        var groups = new Dictionary<(Fellowship.SDK.EntityType, int), List<EntityDocument>>();
        var stats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spelled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<EntityDocument>();

        var number = 0;

        foreach (var line in File.ReadLines(paths.Entities))
        {
            number++;
            if (line.Length == 0) continue;

            var document = Read(line, number, paths.Entities);

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

        logger.LogInformation(
            "Attribute names: {Named} of {Attributes} shipped rows print a word, and {Unnamed} of the {Spelled} attribute " +
            "names the export spells have no row, so they print spaced out",
            named, rows.Count, unnamed, spelled.Count);
    }

    private static EntityDocument Read(string line, int number, string path)
    {
        try
        {
            return JsonSerializer.Deserialize<EntityDocument>(line, Options)
                ?? throw new InvalidOperationException($"Line {number} of {path} deserialised to nothing.");
        }
        catch (JsonException failure)
        {
            throw new InvalidOperationException(
                $"Line {number} of {path} does not read as a document: {failure.Message} An export written " +
                "before a document carried the type it is read back as reads this way. Write it again with " +
                "'dotnet run --project src/FellowshipExtractor -- export-all'.",
                failure);
        }
    }

    private static void Census(EntityDocument document, HashSet<string> stats, HashSet<string> spelled)
    {
        foreach (var slot in Field<StatSlot>(document, nameof(ItemDocument.Stats)))
        {
            stats.Add(slot.Stat);
            spelled.Add(slot.Stat);
        }

        foreach (var attribute in Field<string>(document, nameof(AbilityDocument.ScalesOff))) spelled.Add(attribute);

        foreach (var token in Field<Placeholder>(document, nameof(AbilityDocument.Placeholders)))
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

    public IReadOnlyList<EntityDocument> All(Fellowship.SDK.EntityType type, int id) =>
        byId.TryGetValue((type, id), out var found) ? found : [];

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

    public static bool Reads(EntityDocument document, HeroEntry hero) =>
        document switch
        {
            ItemDocument item => item.Heroes is { } heroes
                ? heroes.Contains(ParseEnum<Hero>(hero.Name))
                : (item.ArmorType is null || item.ArmorType == ParseEnumOrNull<ArmorType>(hero.ArmorType)) &&
                  (item.PrimaryStat is null || item.PrimaryStat == ParseEnum<PrimaryStat>(hero.PrimaryStat)),
            TalentSlotDocument slot => slot.Hero == ParseEnum<Hero>(hero.Name),
            _ => true,
        };

    private static T ParseEnum<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, out var value) ? value : throw new FormatException($"'{name}' names no {typeof(T).Name}.");

    private static T? ParseEnumOrNull<T>(string? name) where T : struct, Enum =>
        name is null ? null : ParseEnum<T>(name);

    public HeroEntry? Wearer(EntityDocument document) =>
        Vocabulary.Heroes.FirstOrDefault(hero => Reads(document, hero));

    public int DropItemLevel(EntityDocument document) =>
        Plateau(document is ItemDocument { Rarity: Fellowship.SDK.Rarity.Legendary }
            ? LegendaryDropRow
            : DefaultDropRow);

    private int Plateau(string row) =>
        Vocabulary.DropItemLevels.FirstOrDefault(
            plateau => plateau.Row.Equals(row, StringComparison.OrdinalIgnoreCase))?.ItemLevel
        ?? throw new InvalidOperationException(
            $"The vocabulary states no {row} drop plateau, so there is no item level to answer a request that " +
            "names none. Write it again with 'dotnet run --project src/FellowshipExtractor -- export-all'.");

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
