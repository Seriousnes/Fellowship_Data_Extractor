using System.Text.Json;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>One hero, as the three things an item row cannot say for itself plus a colour.</summary>
public sealed record HeroEntry(string Codename, string Name, string? ArmorType, string PrimaryStat)
{
    /// <summary>The hero's accent colour as six hex digits, from the palette row their asset names.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// One rung of the rarity ladder, under the name the build files it and the word each vocabulary
/// prints for it. Item and gem are separate vocabularies rather than two guesses at one, so the two
/// display columns part company wherever <see cref="RarityNames.GemLabel"/> renames a tier the item
/// ladder renames differently: tier 4 is filed Champion, prints Heroic on an item and Flawless on a
/// gem. A gem's exported rarity is already the printed gem word rather than the filed one, so a
/// reader holding one can join it on this column and on no other.
/// </summary>
public sealed record RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName);

/// <summary>
/// The lookup tables a reader of entities.jsonl needs and no entity line can carry, written beside
/// it as one small file.
///
/// Three of them, and each closes a question the entity stream leaves open. An item expands into
/// rows keyed by armour type and primary stat and carries no hero at all, so hero=Ardeos picks a
/// row only through an index that says Ardeos is Cloth and Intellect. A rarity arrives as a request
/// parameter spelled the way the build files it rather than the way the game prints it. And a
/// coefficient plus a spread is a band of numbers with no sentence around it until the shipped
/// print formats say how a band is written.
///
/// These are tables rather than entities, which is why they are not lines in the entity stream: a
/// reader asking for every hero should not have to filter twelve rows out of fourteen thousand.
/// </summary>
public sealed class Vocabulary
{
    public const string FileName = "vocabulary.json";

    private const string ColorRowStructure = "/Script/fellowship.CRDSNamedColorDataRow";
    private const string FormatRowStructure = "/Script/fellowship.CRDynamicTooltipDataRow";
    private const string ColorField = "Color";
    private const string TextField = "Text";

    public IReadOnlyList<HeroEntry> Heroes { get; }
    public IReadOnlyList<RarityEntry> Rarities { get; }

    /// <summary>
    /// How the game writes a substituted number, keyed by the row name the UI asks for. A banded
    /// number is DamageRange and its two damage-school variants; the sentence around it decides
    /// which, and a token says whether it wants the school spoken at all through
    /// <see cref="Placeholder.ShowDamageType"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> PrintFormats { get; }

    /// <summary>
    /// The game's named colours, under the names the design system files them by. Every colour a
    /// renderer needs is in here: the rarity ladder, the six gem types, the twelve heroes, the
    /// three roles and the core text and surface palette.
    ///
    /// A rarity colour is keyed by the name the build files the rung under rather than the word it
    /// prints, and those diverge at the top of the ladder. The gold under rarity-champion belongs
    /// to the rung that prints as "Heroic", and the red under rarity-heroic to the one that prints
    /// as "Regal", so a reader joining a printed word straight to a colour name gets the wrong one.
    /// Go through <see cref="RarityEntry.Name"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Colors { get; }

    /// <summary>
    /// The styles a description's own markup names, so a renderer draws a tagged run in the colour
    /// and weight the build states rather than one it picked.
    /// </summary>
    public IReadOnlyDictionary<string, TextStyleEntry> TextStyles { get; }

    /// <summary>
    /// Everything the build states about turning an item level into stat magnitudes: the two pool
    /// curves, the multipliers each kind of slot applies, what each equipment position is worth,
    /// and the slots a rarity adds on top of the ones an item declares.
    /// </summary>
    public StatCalculation StatCalculation { get; }

    /// <summary>The stats a slot rolled at random can land on.</summary>
    public IReadOnlyList<string> RandomStatPool { get; }

    public Vocabulary(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        HeroNames heroes,
        RarityNames rarities,
        StatCalculation statCalculation,
        IReadOnlyList<string> randomStatPool)
    {
        StatCalculation = statCalculation;
        RandomStatPool = randomStatPool;

        var colours = Colours(provider, assets);

        Heroes =
        [
            .. heroes.Profiles.Select(hero => new HeroEntry(
                hero.Codename, hero.ShippedName, hero.ArmorType, hero.PrimaryStat)
            {
                Color = hero.ColorRow is null ? null : colours.GetValueOrDefault(hero.ColorRow),
            }),
        ];

        if (rarities.MissingRenames.ToArray() is { Length: > 0 } missing)
        {
            throw new InvalidOperationException(
                $"The rarity ladder no longer has a tier named {string.Join(" or ", missing)}, so the rename that " +
                "turns the filed names into the printed ones no longer applies to it.");
        }

        Rarities = [.. rarities.Tiers.Select(tier => new RarityEntry(
            tier, rarities.Label(tier)!, rarities.Printed(tier), rarities.GemLabel(tier)!))];

        PrintFormats = Rows(provider, assets, FormatRowStructure, TextField);
        Colors = colours;
        TextStyles = new RichTextStyles(provider, assets).Styles;

        if (Heroes.Count == 0 || Rarities.Count == 0 || PrintFormats.Count == 0 ||
            Colors.Count == 0 || TextStyles.Count == 0)
        {
            throw new InvalidOperationException(
                $"The vocabulary is missing a whole section: {Heroes.Count} heroes, {Rarities.Count} rarities, " +
                $"{PrintFormats.Count} print formats, {Colors.Count} colours, {TextStyles.Count} text styles. " +
                "A reader would silently resolve nothing.");
        }
    }

    /// <summary>Writes the file beside the entity stream and answers with its size in bytes.</summary>
    public async Task<long> WriteAsync(string folder)
    {
        var target = Path.Combine(folder, FileName);

        await File.WriteAllTextAsync(target, JsonSerializer.Serialize(
            new
            {
                heroes = Heroes,
                rarities = Rarities,
                printFormats = PrintFormats,
                colors = Colors,
                textStyles = TextStyles,
                statCalculation = new
                {
                    primaryPool = StatCalculation.Primary,
                    secondaryPool = StatCalculation.Secondary,
                    primaryPowerBase = StatCalculation.PrimaryPowerBase,
                    primaryAndStaminaMultiplier = StatCalculation.PrimaryAndStaminaMultiplier,
                    statRarityBase = StatCalculation.StatRarityBase,
                    baseSlot = StatCalculation.BaseSlot,
                    fixedSlot = StatCalculation.FixedSlot,
                    dynamicSlot = StatCalculation.DynamicSlot,
                    slotWeights = StatCalculation.SlotWeights,
                    armorWeights = StatCalculation.ArmorWeights,
                    attributes = StatCalculation.Attributes,
                    randomStatPool = RandomStatPool,
                    rarityAdditions = StatCalculation.RarityAdditions,
                },
            },
            TooltipCommand.JsonOptions));

        return new FileInfo(target).Length;
    }

    /// <summary>
    /// The design system palette, every table of it. The rows are split across five tables by
    /// purpose and a hero's asset names one row without saying which table it is in, so they are
    /// read as one set.
    /// </summary>
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
