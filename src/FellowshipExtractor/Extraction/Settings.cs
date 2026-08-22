using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed class Settings
{
    public const string FileName = "settings.json";

    private const string ColorRowStructure = "/Script/fellowship.CRDSNamedColorDataRow";
    private const string FormatRowStructure = "/Script/fellowship.CRDynamicTooltipDataRow";
    private const string ColorField = "Color";
    private const string TextField = "Text";

    public IReadOnlyList<HeroEntry> Heroes { get; }
    public IReadOnlyList<RarityEntry> Rarities { get; }

    public IReadOnlyList<AbilityCategoryEntry> AbilityCategories { get; }

    public IReadOnlyDictionary<string, string> PrintFormats { get; }

    public IReadOnlyDictionary<string, string> Colors { get; }

    public IReadOnlyDictionary<string, TextStyleEntry> TextStyles { get; }

    public StatCalculation StatCalculation { get; }

    public IReadOnlyList<string> RandomStatPool { get; }

    public IReadOnlyList<string> GemTypes { get; }

    public IReadOnlyDictionary<string, string> SlotIcons { get; }

    public IReadOnlyDictionary<string, string> RarityOrnaments { get; }

    public IReadOnlyDictionary<int, string> TalentTierPlates { get; }

    public IReadOnlyDictionary<string, string> RarityColorRows { get; }

    public IReadOnlyDictionary<string, string> SectionHeadingRows { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<TemperCeiling>> MaxTempers { get; }

    public IReadOnlyList<DropPlateau> DropItemLevels { get; }

    public BlessingEntry Blessings { get; }

    public DifficultyScaling DifficultyScaling { get; }

    public IReadOnlyList<LeagueEntry> Leagues { get; }

    public Settings(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        HeroNames heroes,
        RarityNames rarities,
        AbilityCategories categories,
        StatCalculation statCalculation,
        IReadOnlyList<string> randomStatPool,
        IReadOnlyList<string> gemTypes,
        SlotIcons slotIcons,
        RarityOrnaments ornaments,
        TalentTierPlates tierPlates,
        RarityColorRows colorRows,
        SectionHeadingRows headings,
        TemperCeilings tempers,
        DropItemLevels drops,
        Blessings blessings,
        CreatureStats creatures,
        Leagues leagues)
    {
        DifficultyScaling = creatures.Scaling;
        Leagues = leagues.All;

        StatCalculation = statCalculation;
        RandomStatPool = randomStatPool;
        GemTypes = gemTypes;
        SlotIcons = slotIcons.BySlot;
        RarityOrnaments = ornaments.ByRarity;
        TalentTierPlates = tierPlates.ByRow;
        MaxTempers = tempers.ByRarity;
        DropItemLevels = drops.Plateaus;
        Blessings = blessings.Wording;

        var colours = Colours(provider, assets);
        var moved = HeroRows(colours, heroes);

        RarityColorRows = Drawn(colorRows.ByRarity, moved);
        SectionHeadingRows = Drawn(headings.BySection, moved);

        Resolvable(RarityColorRows, colours, colorRows.Source);
        Resolvable(SectionHeadingRows, colours, headings.Source);

        Heroes =
        [
            .. heroes.Profiles.Select(hero => new HeroEntry(
                hero.ShippedName, hero.ArmorType, hero.PrimaryStat)
            {
                Color = hero.ColorRow is null
                    ? null
                    : colours.GetValueOrDefault(moved.GetValueOrDefault(hero.ColorRow, hero.ColorRow)),
            }),
        ];

        _heroByName = Heroes.ToDictionary(hero => hero.Name, StringComparer.OrdinalIgnoreCase);

        if (rarities.MissingRenames.ToArray() is { Length: > 0 } missing)
        {
            throw new InvalidOperationException(
                $"The rarity ladder no longer has a tier named {string.Join(" or ", missing)}, so the rename that " +
                "turns the filed names into the printed ones no longer applies to it.");
        }

        Rarities = [.. rarities.Tiers.Select(tier => new RarityEntry(
            tier, rarities.Label(tier)!, rarities.Printed(tier), rarities.GemLabel(tier)!))];

        AbilityCategories = categories.Members;

        PrintFormats = Rows(provider, assets, FormatRowStructure, TextField);
        Colors = colours;
        TextStyles = new RichTextStyles(provider, assets).Styles;

        if (Heroes.Count == 0 || Rarities.Count == 0 || PrintFormats.Count == 0 ||
            Colors.Count == 0 || TextStyles.Count == 0)
        {
            throw new InvalidOperationException(
                $"The settings are missing a whole section: {Heroes.Count} heroes, {Rarities.Count} rarities, " +
                $"{PrintFormats.Count} print formats, {Colors.Count} colours, {TextStyles.Count} text styles. " +
                "A reader would silently resolve nothing.");
        }
    }

    private readonly Dictionary<string, HeroEntry> _heroByName;

    public HeroEntry? Hero(string name) => _heroByName.GetValueOrDefault(name);

    public SettingsFile ToFile() => new()
    {
        Heroes = Heroes,
        Rarities = Rarities,
        AbilityCategories = AbilityCategories,
        GemTypes = GemTypes,
        MaxTempers = MaxTempers,
        DropItemLevels = DropItemLevels,
        Blessings = Blessings,
        DifficultyScaling = DifficultyScaling,
        Leagues = Leagues,
        SlotIcons = SlotIcons,
        RarityOrnaments = RarityOrnaments,
        TalentTierPlates = TalentTierPlates,
        RarityColorRows = RarityColorRows,
        SectionHeadingRows = SectionHeadingRows,
        PrintFormats = PrintFormats,
        Colors = Colors,
        TextStyles = TextStyles.ToDictionary(
            style => style.Key,
            style => new TextStyle(style.Value.Color, style.Value.Weight, style.Value.Size),
            StringComparer.OrdinalIgnoreCase),
        StatCalculation = new StatCalculationFile
        {
            PrimaryPool = StatCalculation.Primary,
            SecondaryPool = StatCalculation.Secondary,
            PrimaryPowerBase = StatCalculation.PrimaryPowerBase,
            PrimaryAndStaminaMultiplier = StatCalculation.PrimaryAndStaminaMultiplier,
            StatRarityBase = StatCalculation.StatRarityBase,
            BaseSlot = StatCalculation.BaseSlot,
            FixedSlot = StatCalculation.FixedSlot,
            DynamicSlot = StatCalculation.DynamicSlot,
            SlotWeights = StatCalculation.SlotWeights,
            ArmorWeights = StatCalculation.ArmorWeights,
            Attributes = StatCalculation.Attributes,
            RandomStatPool = RandomStatPool,
            RarityAdditions = StatCalculation.RarityAdditions,
        },
    };

    private static Dictionary<string, string> HeroRows(Dictionary<string, string> colours, HeroNames heroes)
    {
        var moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hero in heroes.Profiles)
        {
            if (hero.ColorRow is not { } row) continue;
            if (!colours.Remove(row, out var hex)) continue;

            var named = row[..^hero.Codename.Length] + hero.ShippedName.ToLowerInvariant();

            colours[named] = hex;
            moved[row] = named;
        }

        return moved;
    }

    private static Dictionary<string, string> Drawn(
        IReadOnlyDictionary<string, string> rows, IReadOnlyDictionary<string, string> moved) =>
        rows.ToDictionary(
            row => row.Key,
            row => moved.GetValueOrDefault(row.Value, row.Value),
            StringComparer.OrdinalIgnoreCase);

    private static void Resolvable(
        IReadOnlyDictionary<string, string> rows,
        IReadOnlyDictionary<string, string> colours,
        string source)
    {
        foreach (var (key, row) in rows)
        {
            if (colours.ContainsKey(row)) continue;

            throw new InvalidOperationException(
                $"{source} draws {key} in {row}, which no named colour table declares, so it would resolve to no " +
                "hex at all.");
        }
    }

    private static Dictionary<string, string> Colours(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets) =>
        Rows(provider, assets, ColorRowStructure, ColorField);

    private static Dictionary<string, string> Rows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, string rowStructure, string field)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var objectPath in TableFinder.ByRowStructure(assets, rowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.GetText(row, field) is { } value) found.TryAdd(rowName.Text, value);
            }
        }

        return found;
    }
}
