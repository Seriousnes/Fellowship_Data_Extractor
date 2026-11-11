using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface IEntityStoreService
{
    FellowshipExport Export { get; }

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

    TalentSlotDocument? Slot(int talentId);

    IReadOnlyList<TalentSlotDocument> Tree(Hero hero);

    Lookup Resolve(EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero);

    HeroEntry? Wearer(EntityDocument document);

    int DropItemLevel(EntityDocument document);

    int Difficulty(string? value, EntityDocument? entity = null);

    IReadOnlyList<DifficultyStep> Ladder(EntityDocument? entity);

    CreatureDocument? Menacing(EntityDocument? document);

    RarityEntry? Rarity(string? word, bool gem);
}

public sealed class EntityStoreService : IEntityStoreService
{
    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Fields = new();

    private static IReadOnlyList<T> Field<T>(EntityDocument document, string name) =>
        Fields.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property))
            ?.GetValue(document) as IReadOnlyList<T> ?? [];

    private readonly Dictionary<int, TalentSlotDocument> slotsByTalent = [];

    private readonly Dictionary<Hero, List<TalentSlotDocument>> treesByHero = [];

    public FellowshipExport Export { get; }

    public SettingsFile Settings { get; }

    public IReadOnlyList<EntityDocument> Entities { get; }

    public IReadOnlyList<EntityType> Types { get; }

    public StatParameters Stats { get; }

    public IReadOnlySet<string> PrimaryAttributes { get; }

    public int Count { get; }

    public int Ids { get; }

    public EntityStoreService(FellowshipExport export, ILogger<EntityStoreService> logger)
    {
        Export = export;
        Settings = Export.Settings;
        PrimaryAttributes = Export.PrimaryAttributes;

        var stats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var attributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = export.Entities;

        foreach (var document in entities)
        {
            if (document is TalentSlotDocument slot)
            {
                slotsByTalent.TryAdd(slot.Talent.Id, slot);

                if (!treesByHero.TryGetValue(slot.Hero, out var tree)) treesByHero[slot.Hero] = tree = [];

                tree.Add(slot);
            }

            Census(document, stats, attributeNames);

            Count++;
        }

        Ids = Export.Ids;

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
        var unnamed = attributeNames.Count(key => !rows.ContainsKey(key));

        logger.LogInformation(
            "Export snapshot loaded: {Entities} entities under {Ids} ids, {Ambiguous} of which read differently per hero. " +
            "{Heroes} heroes, {Rarities} rarities, {Formats} print formats, {Colors} colours, {Styles} text styles, {Stats} stat names",
            Count, Ids, Export.PerHeroIds, Settings.Heroes.Count,
            Settings.Rarities.Count, Settings.PrintFormats.Count, Settings.Colors.Count,
            Settings.TextStyles.Count, Stats.Names.Count);

        logger.LogInformation(
            "Attribute names: {Named} of {Attributes} shipped rows print a word, and {Unnamed} of the {Spelled} attribute " +
            "names the export spells have no row, so they print spaced out",
            named, rows.Count, unnamed, attributeNames.Count);
    }

    private static void Census(EntityDocument document, HashSet<string> stats, HashSet<string> attributeNames)
    {
        foreach (var slot in Field<StatSlot>(document, nameof(ItemDocument.Stats)))
        {
            stats.Add(slot.Stat);
            attributeNames.Add(slot.Stat);
        }

        foreach (var attribute in Field<string>(document, nameof(AbilityDocument.ScalesOff)))
        {
            attributeNames.Add(attribute);
        }

        foreach (var token in Field<Placeholder>(document, nameof(AbilityDocument.Placeholders)))
        {
            if (token.Attribute is { } attribute) attributeNames.Add(attribute);
            foreach (var scaling in token.ScalesOff ?? []) attributeNames.Add(scaling);
        }
    }

    public string StatName(string key) =>
        Settings.StatCalculation.Attributes.TryGetValue(key, out var attribute) &&
        attribute.DisplayName is { Length: > 0 } printed
            ? printed
            : FellowshipCodex.Spaced(key);

    public AttributeDisplay? StatDisplay(string? key) =>
        key is not null && Settings.StatCalculation.Attributes.TryGetValue(key, out var attribute)
            ? attribute
            : null;

    public string StatValue(string? key, double value)
    {
        if (StatDisplay(key) is not { } display) return FellowshipCodex.Number(value);

        var rounded = Math.Round((decimal)value, display.RoundToDecimals, MidpointRounding.AwayFromZero);

        return rounded.Normalize().ToString(CultureInfo.InvariantCulture) +
            (display.IsPercent ? "%" : string.Empty);
    }

    public HeroEntry? Hero(string? name) => Export.Hero(name);

    public IReadOnlyList<EntityDocument> All(EntityType type, int id) =>
        Export.All(type, id);

    public TalentSlotDocument? Slot(int talentId) =>
        slotsByTalent.TryGetValue(talentId, out var slot) ? slot : null;

    public IReadOnlyList<TalentSlotDocument> Tree(Hero hero) =>
        treesByHero.TryGetValue(hero, out var tree) ? tree : [];

    public Lookup Resolve(
        EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero) =>
        Export.Resolve(type, id, heroName, out document, out hero);

    public HeroEntry? Wearer(EntityDocument document) =>
        Settings.Heroes.FirstOrDefault(hero => EntityStore.Reads(document, hero));

    public int DropItemLevel(EntityDocument document) => Export.DropItemLevel(document);

    public int Difficulty(string? value, EntityDocument? entity = null)
    {
        var ladder = Ladder(entity);

        return Difficulties.Read(value, Settings) is { } read && ladder.Any(step => step.Difficulty == read)
            ? read
            : ladder.Select(step => step.Difficulty).DefaultIfEmpty(1).First();
    }

    public IReadOnlyList<DifficultyStep> Ladder(EntityDocument? entity) =>
        Difficulties.Ladder(Settings, entity switch
        {
            DungeonDocument dungeon => [dungeon],
            _ when Menacing(entity) is { FoundIn: { Count: > 0 } found } =>
                [.. found.SelectMany(dungeon => All(dungeon.Type, dungeon.Id)).OfType<DungeonDocument>()],
            _ => [],
        });

    public CreatureDocument? Menacing(EntityDocument? document) => document switch
    {
        CreatureDocument creature => creature,
        AbilityDocument { Owner: { Type: EntityType.Creature } owner } =>
            All(EntityType.Creature, owner.Id).OfType<CreatureDocument>().FirstOrDefault(),
        _ => null,
    };

    public RarityEntry? Rarity(string? word, bool gem) => Export.Rarity(word, gem);
}
