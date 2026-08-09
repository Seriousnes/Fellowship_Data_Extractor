using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface IEntityStoreService
{
    SettingsFile Settings { get; }

    IReadOnlyList<EntityDocument> Entities { get; }

    IReadOnlyList<EntityType> Types { get; }

    StatParameters Stats { get; }

    IReadOnlySet<string> PrimaryAttributes { get; }

    int Count { get; }

    int Ids { get; }

    string StatName(string key);

    AttributeDisplay? StatDisplay(string? key);

    string StatValue(string? key, double value);

    HeroEntry? Hero(string? name);

    IReadOnlyList<EntityDocument> All(EntityType type, int id);

    (EntityType Type, int Id)? GrantedPower(int id);

    Lookup Resolve(EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero);

    HeroEntry? Wearer(EntityDocument document);

    int DropItemLevel(EntityDocument document);

    RarityEntry? Rarity(string? word, bool gem);
}

public sealed class EntityStoreService : IEntityStoreService
{
    private const string DefaultDropRow = "Default";

    private const string LegendaryDropRow = "Legendary";

    private static readonly JsonSerializerOptions Options = EntityDocuments.Options;

    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Fields = new();

    private static IReadOnlyList<T> Field<T>(EntityDocument document, string name) =>
        Fields.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property))
            ?.GetValue(document) as IReadOnlyList<T> ?? [];

    private readonly Dictionary<(EntityType Type, int Id), EntityDocument[]> byId = new();

    private readonly Dictionary<string, HeroEntry> heroesByName = new(StringComparer.OrdinalIgnoreCase);

    public SettingsFile Settings { get; }

    public IReadOnlyList<EntityDocument> Entities { get; }

    public IReadOnlyList<EntityType> Types { get; }

    public StatParameters Stats { get; }

    public IReadOnlySet<string> PrimaryAttributes { get; }

    public int Count { get; }

    public int Ids { get; }

    public EntityStoreService(IExportPathService paths, ILogger<EntityStoreService> logger)
    {
        var file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(paths.Settings), Options)
            ?? throw new InvalidOperationException($"{paths.Settings} deserialised to nothing.");

        Settings = file with
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

        foreach (var hero in Settings.Heroes)
        {
            heroesByName[hero.Name] = hero;
            attributes.Add(hero.PrimaryStat);
        }

        PrimaryAttributes = attributes;

        var groups = new Dictionary<(EntityType, int), List<EntityDocument>>();
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

        Ids = byId.Count;

        Entities = entities;
        Stats = new StatParameters(Settings.StatCalculation.Attributes);

        Types =
        [
            .. entities.GroupBy(EntityTypes.TypeOf)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => EntityTypes.Slug(g.Key), StringComparer.Ordinal)
                .Select(g => g.Key),
        ];

        var rows = Settings.StatCalculation.Attributes;
        var named = rows.Count(row => row.Value.DisplayName is { Length: > 0 });
        var unnamed = spelled.Count(key => !rows.ContainsKey(key));

        logger.LogInformation(
            "Export loaded from {Path}: {Entities} entities under {Ids} ids, {Ambiguous} of which read differently per hero. " +
            "{Heroes} heroes, {Rarities} rarity rungs, {Formats} print formats, {Colors} colours, {Styles} text styles, {Stats} stat names",
            paths.Export, Count, Ids, byId.Count(e => e.Value.Length > 1), Settings.Heroes.Count,
            Settings.Rarities.Count, Settings.PrintFormats.Count, Settings.Colors.Count,
            Settings.TextStyles.Count, Stats.Names.Count);

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
        catch (Exception failure) when (failure is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Line {number} of {path} does not read as a document: {failure.Message} An export written " +
                $"before a document carried the '{EntityDocuments.Discriminator}' it is read back as reads this " +
                "way. Write it again with 'dotnet run --project src/FellowshipExtractor -- export-all'.",
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
        Settings.StatCalculation.Attributes.TryGetValue(key, out var attribute) &&
        attribute.DisplayName is { Length: > 0 } printed
            ? printed
            : Codex.Spaced(key);

    public AttributeDisplay? StatDisplay(string? key) =>
        key is not null && Settings.StatCalculation.Attributes.TryGetValue(key, out var attribute)
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

    public IReadOnlyList<EntityDocument> All(EntityType type, int id) =>
        byId.TryGetValue((type, id), out var found) ? found : [];

    private static readonly EntityType[] GrantedPowers =
        [EntityType.RelicPower, EntityType.WeaponPower, EntityType.LegendaryPower, EntityType.Talent];

    public (EntityType Type, int Id)? GrantedPower(int id)
    {
        foreach (var type in GrantedPowers)
        {
            if (All(type, id).Count > 0) return (type, id);
        }

        return null;
    }

    public Lookup Resolve(
        EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero)
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
        var matches = candidates.Where(e => EntityStore.Reads(e, named)).ToArray();

        if (matches.Length == 0) return Lookup.NotForThatHero;

        document = matches[0];
        return Lookup.Found;
    }

    public HeroEntry? Wearer(EntityDocument document) =>
        Settings.Heroes.FirstOrDefault(hero => EntityStore.Reads(document, hero));

    public int DropItemLevel(EntityDocument document) =>
        Plateau(document is ItemDocument { Rarity: Fellowship.SDK.Rarity.Legendary }
            ? LegendaryDropRow
            : DefaultDropRow);

    private int Plateau(string row) =>
        Settings.DropItemLevels.FirstOrDefault(
            plateau => plateau.Row.Equals(row, StringComparison.OrdinalIgnoreCase))?.ItemLevel
        ?? throw new InvalidOperationException(
            $"The settings state no {row} drop plateau, so there is no item level to answer a request that " +
            "names none. Write it again with 'dotnet run --project src/FellowshipExtractor -- export-all'.");

    public RarityEntry? Rarity(string? word, bool gem)
    {
        if (word is null) return null;

        if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            return Settings.Rarities.FirstOrDefault(r => r.Tier == tier);
        }

        return Settings.Rarities.FirstOrDefault(r =>
            string.Equals(gem ? r.GemDisplayName : r.DisplayName, word, StringComparison.OrdinalIgnoreCase));
    }
}
