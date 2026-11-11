using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Fellowship.SDK.Client;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Fellowship.SDK.Records;

namespace Fellowship.SDK.Calculations;

/// <summary>
/// One build's settings.json and entities.jsonl, with the item level scaling, tempering, rarity and
/// augmentation rank rules the codex reads its records at.
/// </summary>
public sealed class FellowshipExport
{
    private const string TrimWarning =
        "A document is read into a type named at run time, so trimming can take that type away.";

    private const string DefaultDropRow = "Default";

    private const string LegendaryDropRow = "Legendary";

    private readonly Dictionary<string, HeroEntry> heroesByName = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<(EntityType, int), EntityDocument[]> byId = [];

    public FellowshipExport(SettingsFile settings, IReadOnlyList<EntityDocument> entities)
    {
        Settings = Normalized(settings);
        Entities = entities;

        var attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hero in Settings.Heroes)
        {
            heroesByName[hero.Name] = hero;
            attributes.Add(hero.PrimaryStat);
        }

        PrimaryAttributes = attributes;

        var groups = new Dictionary<(EntityType, int), List<EntityDocument>>();

        foreach (var document in entities)
        {
            var key = (EntityTypes.TypeOf(document), document.Id);

            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            list.Add(document);
        }

        foreach (var (key, list) in groups) byId[key] = [.. list];
    }

    public SettingsFile Settings { get; }

    public IReadOnlyList<EntityDocument> Entities { get; }

    /// <summary>Distinct type and id pairs.</summary>
    public int Ids => byId.Count;

    /// <summary>Type and id pairs with more than one reading, one per hero.</summary>
    public int PerHeroIds => byId.Count(entry => entry.Value.Length > 1);

    /// <summary>Stats a hero reads as a primary stat. An item's row for one of these, and its Stamina row, read the primary pool.</summary>
    public IReadOnlySet<string> PrimaryAttributes { get; }

    /// <summary>
    /// Reads the export in a folder holding settings.json and entities.jsonl, or the highest build in a
    /// folder of export-&lt;build&gt; folders.
    /// </summary>
    [RequiresUnreferencedCode(TrimWarning)]
    public static FellowshipExport Load(string folder)
    {
        var export = Holds(folder)
            ? folder
            : Directory.Exists(folder)
                ? new DirectoryInfo(folder).EnumerateDirectories(ExportFolders.Pattern)
                    .Where(child => Holds(child.FullName))
                    .OrderByDescending(child => ExportFolders.Build(child.Name))
                    .FirstOrDefault()?.FullName
                : null;

        if (export is null)
        {
            throw new DirectoryNotFoundException(
                $"{Path.GetFullPath(folder)} has no {ExportReader.EntitiesName} and {ExportReader.SettingsName}, " +
                $"and no {ExportFolders.Pattern} folder with both.");
        }

        return Read(
            File.ReadAllText(Path.Combine(export, ExportReader.SettingsName)),
            File.ReadLines(Path.Combine(export, ExportReader.EntitiesName)));
    }

    private static bool Holds(string folder) =>
        File.Exists(Path.Combine(folder, ExportReader.SettingsName)) &&
        File.Exists(Path.Combine(folder, ExportReader.EntitiesName));

    /// <summary>Reads settings.json text and the lines of entities.jsonl.</summary>
    [RequiresUnreferencedCode(TrimWarning)]
    public static FellowshipExport Read(string settings, IEnumerable<string> entities) =>
        new(ExportReader.ReadSettings(settings), ExportReader.ReadEntities(entities));

    /// <summary>The settings with every name-keyed table matching names regardless of case.</summary>
    public static SettingsFile Normalized(SettingsFile file) => file with
    {
        TextStyles = new Dictionary<string, TextStyle>(file.TextStyles, StringComparer.OrdinalIgnoreCase),
        Colors = new Dictionary<string, string>(file.Colors, StringComparer.OrdinalIgnoreCase),
        SlotIcons = new Dictionary<string, string>(file.SlotIcons, StringComparer.OrdinalIgnoreCase),
        RarityOrnaments = new Dictionary<string, string>(file.RarityOrnaments, StringComparer.OrdinalIgnoreCase),
        RarityColorRows = new Dictionary<string, string>(file.RarityColorRows, StringComparer.OrdinalIgnoreCase),
        SectionHeadingRows = new Dictionary<string, string>(file.SectionHeadingRows, StringComparer.OrdinalIgnoreCase),
        MaxTempers = new Dictionary<string, IReadOnlyList<TemperCeiling>>(file.MaxTempers, StringComparer.OrdinalIgnoreCase),
        StatCalculation = file.StatCalculation with
        {
            SlotWeights = new Dictionary<string, SlotWeight>(
                file.StatCalculation.SlotWeights, StringComparer.OrdinalIgnoreCase),
            ArmorWeights = new Dictionary<string, ArmorTypeWeight>(
                file.StatCalculation.ArmorWeights, StringComparer.OrdinalIgnoreCase),
            Attributes = new Dictionary<string, AttributeDisplay>(
                file.StatCalculation.Attributes, StringComparer.OrdinalIgnoreCase),
            RarityAdditions = new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(
                file.StatCalculation.RarityAdditions, StringComparer.OrdinalIgnoreCase),
        },
        Items = file.Items with
        {
            SlotNames = new Dictionary<string, string>(file.Items.SlotNames, StringComparer.OrdinalIgnoreCase),
        },
    };

    public HeroEntry? Hero(string? name) =>
        name is not null && heroesByName.TryGetValue(name, out var hero) ? hero : null;

    /// <summary>Every reading of one id. An id with one reading per hero has several.</summary>
    public IReadOnlyList<EntityDocument> All(EntityType type, int id) =>
        byId.TryGetValue((type, id), out var found) ? found : [];

    /// <summary>The reading of the entity the hero reads, or its first reading with no hero named.</summary>
    public TDocument? Find<TDocument>(EntityReference reference, string? hero = null) where TDocument : EntityDocument =>
        Find<TDocument>(reference.Type, reference.Id, hero);

    /// <summary>The reading of the entity the hero reads, or its first reading with no hero named.</summary>
    public TDocument? Find<TDocument>(EntityType type, int id, string? hero = null) where TDocument : EntityDocument
    {
        var readings = All(type, id).OfType<TDocument>();

        if (hero is null) return readings.FirstOrDefault();

        var entry = Hero(hero) ?? throw new ArgumentException(
            $"'{hero}' is not a hero. The heroes are {string.Join(", ", Settings.Heroes.Select(each => each.Name))}.",
            nameof(hero));

        return readings.FirstOrDefault(reading => EntityStore.Reads(reading, entry));
    }

    /// <summary>
    /// The one reading of a type and id. An id with one reading per hero needs the hero, and a hero the
    /// id has no reading for is refused.
    /// </summary>
    public Lookup Resolve(EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero)
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

        if (candidates.FirstOrDefault(candidate => EntityStore.Reads(candidate, named)) is not { } match)
        {
            return Lookup.NotForThatHero;
        }

        document = match;
        return Lookup.Found;
    }

    /// <summary>The refusal the codex API answers a reading with, or null for a reading it reads.</summary>
    public ReadingRefusal? Refusal(RecordReading reading)
    {
        if (reading.Hero is { } hero && Hero(hero) is null)
        {
            return new ReadingRefusal(
                "No such hero.",
                $"'{hero}' is not a hero. The roster is " +
                    string.Join(", ", Settings.Heroes.Select(entry => entry.Name).Order()) + ".");
        }

        if (reading.Rarity is { } rarity && Rarity(rarity, gem: false) is null)
        {
            return new ReadingRefusal(
                "No such rarity.",
                $"'{rarity}' is not a rarity. The rarities are " +
                    string.Join(", ", Settings.Rarities.Select(entry => $"{entry.DisplayName} ({entry.Tier})")) + ".");
        }

        if (reading.ItemLevel is { } level && (level < 1 || level > Settings.Items.MaxItemLevel))
        {
            return new ReadingRefusal(
                "No such item level.",
                $"The item level is {Number(level)}, and an item level runs from 1 to " +
                    Number(Settings.Items.MaxItemLevel) + ".");
        }

        if (reading.Upgrades is < 0)
        {
            return new ReadingRefusal("No such tempering.", $"The tempering is {Number(reading.Upgrades.Value)}, below 0.");
        }

        if (reading.RankSources is { Count: > 0 } sources)
        {
            var types = AugmentationRanks.Text(Settings).ItemTypes.Select(type => type.Type).ToArray();

            if (sources.FirstOrDefault(source => !types.Contains(source, StringComparer.OrdinalIgnoreCase)) is { } unknown)
            {
                return new ReadingRefusal(
                    "No such rank source.",
                    $"'{unknown}' is not an item type. The item types are {string.Join(", ", types)}.");
            }
        }

        if (reading.Difficulty is { } difficulty && Difficulties.At(Settings, difficulty) is null)
        {
            return new ReadingRefusal(
                "No such difficulty.",
                $"The difficulty is {Number(difficulty)}. The difficulties are " +
                    string.Join(", ", Difficulties.Ladder(Settings).Select(step => Number(step.Difficulty))) + ".");
        }

        if (reading.EquippedTier is { } tier && (tier < 0 || tier > Settings.Glyphs.Tiers))
        {
            return new ReadingRefusal(
                "No such tier.",
                $"The equipped tier is {Number(tier)}, and a glyph's tiers run from 0 to {Number(Settings.Glyphs.Tiers)}.");
        }

        return null;
    }

    /// <summary>The refusal the codex API answers an item scaling with, or null for a scaling it reads.</summary>
    public ReadingRefusal? Refusal(ItemScaling scaling) => Refusal(new RecordReading
    {
        Hero = scaling.Hero,
        ItemLevel = scaling.ItemLevel,
        Upgrades = scaling.Upgrades,
        Rarity = scaling.Rarity,
    });

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The augmentation with the id, read for the wearer where it reads differently per hero.</summary>
    public AugmentationDocument? Augmentation(int id, string? wearer)
    {
        AugmentationDocument[] readings =
        [
            .. AugmentationTypes.All.SelectMany(type => All(type, id)).OfType<AugmentationDocument>(),
        ];

        var hero = Hero(wearer);

        return (hero is null ? null : readings.FirstOrDefault(reading => EntityStore.Reads(reading, hero)))
            ?? readings.FirstOrDefault();
    }

    /// <summary>Item level a document drops at: the legendary row for a legendary item, the default row otherwise.</summary>
    public int DropItemLevel(EntityDocument document) =>
        DropItemLevel(document is ItemDocument { Rarity: SDK.Rarity.Legendary } ? LegendaryDropRow : DefaultDropRow);

    private int DropItemLevel(string row) =>
        Settings.DropItemLevels.FirstOrDefault(
            level => level.Row.Equals(row, StringComparison.OrdinalIgnoreCase))?.ItemLevel
        ?? throw new InvalidOperationException(
            $"The settings state no {row} drop item level. Write it again with 'dotnet run --project " +
            "src/FellowshipExtractor'.");

    /// <summary>The rarity a tier number or display name names. A gem reads the gem display names.</summary>
    public RarityEntry? Rarity(string? word, bool gem)
    {
        if (word is null) return null;

        if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            return Settings.Rarities.FirstOrDefault(rarity => rarity.Tier == tier);
        }

        return Settings.Rarities.FirstOrDefault(rarity =>
            string.Equals(gem ? rarity.GemDisplayName : rarity.DisplayName, word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The rarity a document is read at: the one named, or else the document's own, or else its highest.</summary>
    public RarityEntry? Rarity(EntityDocument document, string? word)
    {
        var tier = document switch
        {
            ItemDocument { Rarity: { } only } => (int?)only,
            ItemDocument { Rarities: { Count: > 0 } rarities } => (int)rarities.Max(),
            CurrencyDocument { Rarity: { } coin } => (int)coin,
            GemDocument gem => (int)gem.Rarity,
            _ => null,
        };

        return Rarity(word, document is GemDocument)
            ?? (tier is { } found ? Settings.Rarities.FirstOrDefault(rarity => rarity.Tier == found) : null);
    }

    /// <summary>The printed name of a rarity on the document: a gem's rarity has a name of its own.</summary>
    public static string RarityName(EntityDocument document, RarityEntry rarity) =>
        document is GemDocument ? rarity.GemDisplayName : rarity.DisplayName;

    /// <summary>Item level the item is read at: the one named, or else its drop item level.</summary>
    public int ItemLevel(ItemDocument item, ItemScaling scaling) => scaling.ItemLevel ?? DropItemLevel(item);

    /// <summary>Tempering ceiling: the one named, or else the rarity's at the item level.</summary>
    public int? MaxUpgrades(ItemDocument item, ItemScaling scaling) =>
        scaling.MaxUpgrades
        ?? Tempering.Ceiling(Settings.MaxTempers, Rarity(item, scaling.Rarity)?.Name, ItemLevel(item, scaling));

    /// <summary>The item's stat rows, its armor, and the stats its enchants add, at the scaling.</summary>
    public IReadOnlyList<StatLine> Stats(ItemDocument item, ItemScaling scaling)
    {
        var level = ItemLevel(item, scaling);

        return StatMagnitudes.For(
            item,
            Settings.StatCalculation,
            Settings.Items,
            PrimaryAttributes,
            Rarity(item, scaling.Rarity),
            level,
            scaling,
            [.. Enchanted(scaling).SelectMany(enchant => AugmentationRanks.Granted(enchant, Settings, level))]);
    }

    /// <summary>The item's stat rows, its armor, and the stats its enchants add, at the scaling.</summary>
    /// <exception cref="RefusedReadingException">The codex API refuses the reading.</exception>
    public IReadOnlyList<StatLine> Stats(EntityReference item, ItemScaling scaling) => Stats(Item(item, scaling), scaling);

    /// <summary>The item's potential power at the scaling, or null for an item with no potential power.</summary>
    public decimal? PotentialPower(ItemDocument item, ItemScaling scaling)
    {
        var level = ItemLevel(item, scaling);

        return ItemPower.Of(item, Settings, Rarity(item, scaling.Rarity), scaling with { ItemLevel = level }, level);
    }

    /// <summary>The item's item level, tempering, rarity, potential power and stats at the scaling.</summary>
    public ItemMagnitudesRecord Magnitudes(ItemDocument item, ItemScaling scaling)
    {
        var level = ItemLevel(item, scaling);
        var at = scaling with { ItemLevel = level };
        var rarity = Rarity(item, scaling.Rarity);

        return new ItemMagnitudesRecord
        {
            ItemLevel = level,
            Upgrades = scaling.Upgrades ?? 0,
            MaxUpgrades = MaxUpgrades(item, scaling),
            Rarity = rarity is null ? null : RarityName(item, rarity),
            PotentialPower = ItemPower.Of(item, Settings, rarity, at, level),
            Stats =
            [
                .. Stats(item, at).Select(line => new StatMagnitudeRecord(
                    line.Stat is null ? null : line.DisplayName, line.Value, line.Kind)),
            ],
        };
    }

    /// <summary>The item's item level, tempering, rarity, potential power and stats at the scaling.</summary>
    /// <exception cref="RefusedReadingException">The codex API refuses the reading.</exception>
    public ItemMagnitudesRecord Magnitudes(EntityReference item, ItemScaling scaling) =>
        Magnitudes(Item(item, scaling), scaling);

    private ItemDocument Item(EntityReference reference, ItemScaling scaling)
    {
        if (reference.Type != EntityType.Item)
        {
            throw new ArgumentException($"{reference.Type} {reference.Id} is not an item.", nameof(reference));
        }

        if (Refusal(scaling) is { } refusal) throw new RefusedReadingException(refusal);

        var outcome = Resolve(reference.Type, reference.Id, scaling.Hero, out var document, out _);

        return outcome switch
        {
            Lookup.Found => (ItemDocument)document!,
            Lookup.NoSuchId => throw new RefusedReadingException(
                new ReadingRefusal("No such id.", $"The build has no item {Number(reference.Id)}.")),
            Lookup.NeedsHero => throw new RefusedReadingException(new ReadingRefusal(
                "This id reads differently per hero.",
                $"The build has {All(reference.Type, reference.Id).Count} readings of item {Number(reference.Id)} " +
                    "and no hero was named. Name a hero to pick one.")),
            _ => throw new RefusedReadingException(new ReadingRefusal(
                "Not for that hero.", $"The build has no reading of item {Number(reference.Id)} for {scaling.Hero}.")),
        };
    }

    private IEnumerable<EnchantDocument> Enchanted(ItemScaling scaling) =>
        scaling.Modifiers
            .Where(modifier => ItemCraft.TypeOf(modifier.Kind, Settings) == EntityType.Enchant)
            .Select(modifier => modifier.Id)
            .Concat(scaling.Attunements.Select(attuned => attuned.Id))
            .Select(id => Augmentation(id, scaling.Hero))
            .OfType<EnchantDocument>();
}
