using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>One of the six gem types, as the thing a player accumulates rather than an item.</summary>
public sealed record GemKind(string DisplayName)
{
    /// <summary>The CRGemAttributeSet attribute this type's socketed power adds up in.</summary>
    public string? Attribute { get; init; }

    public GemOverflow? Overflow { get; init; }
}

/// <summary>One gem a player can hold: a type and a size, worth a flat amount of power.</summary>
public sealed record GemSize(string TypeName, int Tier, string DisplayName, int Power)
{
    public string? Rarity { get; init; }

    public string? IconPath { get; init; }
}

/// <summary>One rung of a gem type's unlock ladder.</summary>
public sealed record GemLadderNode(string TypeName, int Node, int RequiredPower)
{
    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    public string? IconPath { get; init; }

    /// <summary>The netId of the lower node this one supersedes, where the row says it does.</summary>
    public int? Replaces { get; init; }
}

/// <summary>
/// Everything gems are, from the one table that defines them.
///
/// The model is not "a gem has traits". A gem is a type and a size; its size is worth a flat
/// GemPower; equipping gems sums that power into a per-type attribute such as RubyPower; and the
/// type's own ten-node ladder unlocks a node each time the running total passes its threshold. So
/// the bonuses hang off the type, not off any gem, and a gem's whole content is which type it feeds
/// and by how much.
///
/// Three shipped facts about the ladder that the export states rather than infers: every type's
/// thresholds are 80 to 1500 in the same ten steps, every node grants a CRCharacterAugmentationAsset
/// that already exports as a line of its own, and the upper five nodes of each type replace the
/// lower five rather than stacking with them.
/// </summary>
public sealed class GemDefinitions
{
    public const string RowStructure = "/Script/fellowship.CRGemsDefinitionRow";

    private const string TypeTagField = "GemTypeID";
    private const string AttributeField = "GameplayAttribute";
    private const string AttributeNameField = "AttributeName";
    private const string DisplayNameField = "DisplayName";
    private const string OverflowField = "MaxRankOverflowData";
    private const string OverflowNameField = "OverflowName";
    private const string OverflowIconField = "UIIcon";
    private const string DescriptionField = "Description";
    private const string TiersField = "GemTierDefinitions";
    private const string TierTagField = "GemID";
    private const string TierTagNameField = "TagName";
    private const string RarityField = "Rarity";
    private const string PowerField = "GemPower";
    private const string TierIconField = "GemIcon";
    private const string NodesField = "GemDefinitionsData";
    private const string RequiredPowerField = "RequiredGemPower";
    private const string GrantedField = "GrantedAugmentation";
    private const string RankNameField = "RankName";
    private const string NodeIconField = "UIIcon";
    private const string TierSegment = ".Tier";
    private const string SupersededSuffix = " - II";
    private const string UpperSuffix = "Medium";
    private const string LowerSuffix = "Small";

    private readonly Dictionary<string, GemKind> _byType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GemSize> _byTierTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GemLadderNode> _byAugmentation = new(StringComparer.OrdinalIgnoreCase);

    public int TypeCount => _byType.Count;
    public int SizeCount => _byTierTag.Count;
    public int NodeCount => _byAugmentation.Count;

    /// <summary>How many ladder nodes state which lower node they take the place of.</summary>
    public int ReplacementCount { get; private set; }

    public GemDefinitions(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ConstantsTable constants,
        AugmentationIds augmentations,
        RarityNames rarities)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TypeTagField) is not { } typeTag) continue;
                if (PropertyWalk.GetString(row, DisplayNameField) is not { } name) continue;

                _byType[typeTag] = new GemKind(name)
                {
                    Attribute = PropertyWalk.GetStruct(row, AttributeField) is { } attribute
                        ? PropertyWalk.GetString(attribute, AttributeNameField)
                        : null,
                    Overflow = Overflow(row, constants),
                };

                ReadSizes(row, name, rarities);
                ReadLadder(row, name, constants, augmentations);
            }
        }

        if (_byType.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so no gem type, size or ladder node exports anything at all.");
        }
    }

    /// <summary>The gem type an ItemID.GemType.X tag names, or null when it names none.</summary>
    public GemKind? ForType(string tagName) => _byType.GetValueOrDefault(tagName);

    /// <summary>The gem an ItemID.GemType.X.TierN tag names, or null when it names none.</summary>
    public GemSize? ForSize(string tagName) => _byTierTag.GetValueOrDefault(tagName);

    /// <summary>The ladder node an augmentation asset grants, or null when it grants none.</summary>
    public GemLadderNode? ForAugmentation(string assetName) => _byAugmentation.GetValueOrDefault(assetName);

    private static GemOverflow? Overflow(IPropertyHolder row, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(row, OverflowField) is not { } overflow) return null;
        if (PropertyWalk.GetText(overflow, DescriptionField) is not { } description) return null;

        return new GemOverflow(description)
        {
            Placeholders = FormatArguments.Read(overflow, DescriptionField, OverflowNameField, constants),
            Icon = PropertyWalk.GetString(overflow, OverflowIconField),
        };
    }

    private void ReadSizes(IPropertyHolder row, string typeName, RarityNames rarities)
    {
        foreach (var tier in Structs(row, TiersField))
        {
            if (PropertyWalk.GetStruct(tier, TierTagField) is not { } id) continue;
            if (PropertyWalk.GetString(id, TierTagNameField) is not { } tag) continue;
            if (PropertyWalk.GetString(tier, DisplayNameField) is not { } name) continue;
            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(tier, PowerField)?.GenericValue) is not { } power) continue;

            // The struct carries a Tier ordinal beside the tag and the two say the same thing, so
            // the tag is read and the ordinal ignored. The tag is the join to the exported line
            // anyway, and reading one field rather than two cannot have them disagree.
            var separator = tag.LastIndexOf(TierSegment, StringComparison.OrdinalIgnoreCase);
            if (separator < 0 || !int.TryParse(tag[(separator + TierSegment.Length)..], out var ordinal)) continue;

            _byTierTag[tag] = new GemSize(typeName, ordinal, name, power)
            {
                Rarity = rarities.GemLabel(PropertyWalk.AsOrdinal(PropertyWalk.Get(tier, RarityField)?.GenericValue)),
                IconPath = PropertyWalk.GetString(tier, TierIconField),
            };
        }
    }

    private void ReadLadder(
        IPropertyHolder row, string typeName, ConstantsTable constants, AugmentationIds augmentations)
    {
        var nodes = Structs(row, NodesField);

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (PropertyWalk.GetString(node, GrantedField) is not { } granted) continue;
            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(node, RequiredPowerField)?.GenericValue) is not { } required)
            {
                continue;
            }

            var replaces = Replaced(nodes, index, augmentations);
            if (replaces is not null) ReplacementCount++;

            _byAugmentation[AbilityPresentationIndex.AssetName(granted)] =
                new GemLadderNode(typeName, index + 1, required)
                {
                    DisplayName = PropertyWalk.GetString(node, RankNameField),
                    Description = PropertyWalk.GetText(node, DescriptionField),
                    Placeholders = PropertyWalk.GetStruct(node, DescriptionField) is { } text
                        ? FormatArguments.Read(text, constants)
                        : null,
                    IconPath = PropertyWalk.GetString(node, NodeIconField),
                    Replaces = replaces,
                };
        }
    }

    /// <summary>
    /// The node a node takes the place of. Nothing in the row structure says so, and three separate
    /// conventions each say it independently: the upper node's RankName is the lower one's plus
    /// " - II", their augmentation names differ only by Medium against Small, and the upper node's
    /// format string ends by naming the lower one outright. All three are required to agree, over
    /// all 30 pairs they do, and a patch that breaks one of them drops the link rather than
    /// inventing a supersession that is no longer stated.
    ///
    /// The offset is half the ladder rather than a fixed five, because that is what makes the
    /// second half of the ladder the second half rather than a coincidence about its length.
    /// </summary>
    private int? Replaced(IReadOnlyList<IPropertyHolder> nodes, int index, AugmentationIds augmentations)
    {
        var offset = nodes.Count / 2;
        if (offset == 0 || index < offset || nodes.Count % 2 != 0) return null;

        var lower = nodes[index - offset];

        if (PropertyWalk.GetString(lower, RankNameField) is not { } lowerName) return null;
        if (PropertyWalk.GetString(nodes[index], RankNameField) != lowerName + SupersededSuffix) return null;

        if (PropertyWalk.GetString(lower, GrantedField) is not { } lowerAsset) return null;
        if (PropertyWalk.GetString(nodes[index], GrantedField) is not { } upperAsset) return null;

        if (Family(upperAsset, UpperSuffix) is not { } family || family != Family(lowerAsset, LowerSuffix))
        {
            return null;
        }

        var declared = $"(Replaces <rt.absorb>{lowerName}</>)";
        if (PropertyWalk.GetText(nodes[index], DescriptionField)?.TrimEnd().EndsWith(declared, StringComparison.Ordinal)
            is not true)
        {
            return null;
        }

        return augmentations.For(lowerAsset);
    }

    /// <summary>
    /// An augmentation's name with the size word that distinguishes the pair taken off, so the two
    /// halves of one bonus reduce to the same string. Sapphire and Diamond separate the word with
    /// an underscore and the other four types do not.
    /// </summary>
    private static string? Family(string assetPath, string suffix)
    {
        var name = AbilityPresentationIndex.AssetName(assetPath);
        if (!name.EndsWith(suffix, StringComparison.Ordinal)) return null;

        name = name[..^suffix.Length];
        return name.EndsWith('_') ? name[..^1] : name;
    }

    private static List<IPropertyHolder> Structs(IPropertyHolder holder, string field) =>
        PropertyWalk.Get(holder, field)?.GenericValue is UScriptArray array
            ? [.. array.Properties
                .Select(p => p?.GenericValue)
                .OfType<FScriptStruct>()
                .Select(s => s.StructType)
                .OfType<IPropertyHolder>()]
            : [];
}

/// <summary>
/// The rarity ladder, from the only shipped table that names it.
///
/// The usmap carries no enum tables, so a Rarity field arrives as a bare ordinal. The loot rule sets
/// are the decode: every row repeats its own RarityTier and its row key begins with the name of that
/// tier, and all 43 rows of the seven tiers agree on the name. That gives Common 0 through Legendary
/// 6, and the four gem sizes at 1 to 4 read Uncommon, Rare, Epic and Champion.
///
/// These are the names the build files the ladder under, which are not always the names it prints:
/// the locres ships Generic_Rarity_Tier_Regal and _Heroic and no _Champion at all. A join on the
/// name would map internal Heroic to "Heroic" and internal Champion to nothing, which is the wrong
/// answer in both directions, so the two spellings are carried side by side and <see cref="Label"/>
/// stays the internal one every other field in the export speaks.
/// </summary>
public sealed class RarityNames
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemLootRarityRuleSet";

    private const string TierField = "RarityTier";
    private const string EnumSeparator = "::";

    private readonly Dictionary<int, string> _byOrdinal = [];

    public int Count => _byOrdinal.Count;

    /// <summary>The ladder in ascending order, from the ordinal each rule set row declares.</summary>
    public IEnumerable<int> Tiers => _byOrdinal.Keys.Order();

    /// <summary>
    /// What each tier prints as, where that is not the name the build files it under. Two of the
    /// seven are renamed and the rename is off by one across the top of the ladder, so a reader
    /// joining on the name gets both of them wrong.
    ///
    /// Three shipped things establish it. The backend stat
    /// STAT_Seasonal_Account_TimesFullyTemperedRegalItemsOfItemLevel315 filters ItemRarity against
    /// the string "heroic" while its own Description reads "Times fully tempered regal items of item
    /// level 315", which binds internal Heroic to printed Regal outright. The locres ships
    /// Generic_Rarity_Tier_Regal and _Heroic and no _Champion, so Heroic is a printed name with
    /// nowhere else to go once tier 5 is spoken for. And the design system spells the ladder
    /// internally - game.game.rarity-champion and game.game.rarity-heroic beside game.rarity-common
    /// and the rest - so the colour rows corroborate the internal names rather than the printed
    /// ones. That leaves internal Champion at tier 4 as the only home for printed Heroic.
    ///
    /// Keyed by the internal name rather than by the ordinal, so a patch that renumbers the ladder
    /// carries the rename with it and one that renames a tier is caught by <see cref="Printed"/>
    /// throwing rather than quietly printing the internal word.
    ///
    /// This is the ladder an item is on. A gem at the same ordinal prints a different word again,
    /// which is <see cref="GemRenamed"/>, and the two are separate tables because they are separate
    /// vocabularies rather than two guesses at one.
    /// </summary>
    private static readonly Dictionary<string, string> Renamed =
        new(StringComparer.OrdinalIgnoreCase) { ["Champion"] = "Heroic", ["Heroic"] = "Regal" };

    /// <summary>
    /// What a tier prints as on a gem, which is not what the same tier prints as on an item. Both
    /// renames are user-confirmed and they are distinct: internal Champion is "Heroic" on an item
    /// and "Flawless" on a gem, so one table cannot serve both and neither is a correction of the
    /// other.
    ///
    /// The gem side is corroborated by the gem rows themselves. Every tier 4 gem's own DisplayName
    /// reads "Flawless Ruby", "Flawless Topaz" and so on, and the locres ships a bare "Flawless"
    /// beside a bare "Splendid" with no rarity key of their own, so Flawless is a word the gem
    /// screens print while no shipped string prints Champion at a gem anywhere.
    ///
    /// Only the tier the user ruled on is renamed. The other three gem tiers keep the internal
    /// names Uncommon, Rare and Epic, which say which loot table a gem drops from and are a
    /// different fact from the size word its DisplayName already carries.
    /// </summary>
    private static readonly Dictionary<string, string> GemRenamed =
        new(StringComparer.OrdinalIgnoreCase) { ["Champion"] = "Flawless" };

    public RarityNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                // An unversioned property left at its default is not serialized, so a row with no
                // RarityTier is tier 0. Common is the only tier that reaches here that way.
                var ordinal = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, TierField)?.GenericValue) ?? 0;

                var separator = rowName.Text.IndexOf('_');
                var name = separator < 0 ? rowName.Text : rowName.Text[..separator];

                if (_byOrdinal.TryGetValue(ordinal, out var existing))
                {
                    if (existing.Equals(name, StringComparison.Ordinal)) continue;

                    throw new InvalidOperationException(
                        $"Loot rule set rows put both {existing} and {name} at rarity {ordinal}, so the row keys no " +
                        "longer name the ladder and nothing else in the build does.");
                }

                _byOrdinal[ordinal] = name;
            }
        }

        if (_byOrdinal.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so nothing says what a rarity ordinal is and every gem would " +
                "export without one.");
        }
    }

    /// <summary>The name of a rarity ordinal, or null when the ladder has no such tier.</summary>
    public string? Label(int? ordinal) => ordinal is { } tier ? _byOrdinal.GetValueOrDefault(tier) : null;

    /// <summary>
    /// The tier a stored ECRItemRarity value names, or null when it names none. A dynamic item
    /// lists the rarities it drops at as enum entries rather than as ordinals, and the same enum
    /// reaches other properties as a bare number, so both readings answer here.
    /// </summary>
    public int? Tier(object? value)
    {
        if (PropertyWalk.AsOrdinal(value) is { } ordinal) return _byOrdinal.ContainsKey(ordinal) ? ordinal : null;

        var text = PropertyWalk.AsString(value);
        if (text is null) return null;

        var separator = text.LastIndexOf(EnumSeparator, StringComparison.Ordinal);
        var leaf = separator < 0 ? text : text[(separator + EnumSeparator.Length)..];

        foreach (var (tier, name) in _byOrdinal)
        {
            if (name.Equals(leaf, StringComparison.OrdinalIgnoreCase)) return tier;
        }

        return null;
    }

    /// <summary>
    /// The name of a rarity ordinal on a gem. A tier the gem vocabulary does not rename answers
    /// with the internal name, exactly as <see cref="Label"/> does.
    /// </summary>
    public string? GemLabel(int? ordinal) =>
        Label(ordinal) is { } name ? GemRenamed.GetValueOrDefault(name) ?? name : null;

    /// <summary>
    /// What a tier prints as. A tier the rename table does not mention prints as itself, and a
    /// renamed one that has vanished from the ladder throws, because a silent fall through to the
    /// internal word is the failure this table exists to prevent.
    /// </summary>
    public string Printed(int ordinal)
    {
        if (Label(ordinal) is not { } name)
        {
            throw new InvalidOperationException(
                $"Rarity tier {ordinal} names nothing. The loot rule set rows no longer cover the whole ladder.");
        }

        return Renamed.GetValueOrDefault(name) ?? name;
    }

    /// <summary>Whether both renamed tiers are still on the ladder the rule sets describe.</summary>
    public IEnumerable<string> MissingRenames =>
        Renamed.Keys.Where(name => !_byOrdinal.ContainsValue(name));
}
