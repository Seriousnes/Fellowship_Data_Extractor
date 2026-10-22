using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record GemKind(string DisplayName)
{
    public string? Attribute { get; init; }

    public GemOverflow? Overflow { get; init; }
}

public sealed record GemSize(string TypeName, int Tier, string DisplayName, int Power)
{
    public int? RarityTier { get; init; }

    public string? Rarity { get; init; }

    public string? IconPath { get; init; }
}

public sealed record GemNode(string TypeName, int Node, int RequiredPower)
{
    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    public string? IconPath { get; init; }

    public int? Replaces { get; init; }
}

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
    private readonly Dictionary<string, GemNode> _byAugmentation = new(StringComparer.OrdinalIgnoreCase);

    public int TypeCount => _byType.Count;
    public int SizeCount => _byTierTag.Count;
    public int NodeCount => _byAugmentation.Count;

    public IReadOnlyList<string> Types =>
        [.. _byType.Values.Select(kind => kind.DisplayName).Order(StringComparer.Ordinal)];

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
                ReadNodes(row, name, constants, augmentations);
            }
        }

        if (_byType.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so no gem type, size or node exports anything at all.");
        }
    }

    public GemKind? ForType(string tagName) => _byType.GetValueOrDefault(tagName);

    public GemSize? ForSize(string tagName) => _byTierTag.GetValueOrDefault(tagName);

    public IReadOnlyDictionary<int, string> RarityByTier(RarityNames rarities)
    {
        var byTier = new Dictionary<int, string>();

        foreach (var size in _byTierTag.Values)
        {
            if (rarities.Label(size.RarityTier) is not { } rarity) continue;

            if (byTier.TryGetValue(size.Tier, out var stored) && !stored.Equals(rarity, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Gem tier {size.Tier} names both {stored} and {rarity}, so a score the config states per tier " +
                    "cannot be stored under the rarity a gem prints.");
            }

            byTier[size.Tier] = rarity;
        }

        if (byTier.Count == 0)
        {
            throw new InvalidOperationException(
                "No gem size names both a tier and a rarity, so nothing joins the per tier scores the item " +
                "settings state to the rarity a gem has.");
        }

        return byTier;
    }

    public GemNode? ForAugmentation(string assetName) => _byAugmentation.GetValueOrDefault(assetName);

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

            var separator = tag.LastIndexOf(TierSegment, StringComparison.OrdinalIgnoreCase);
            if (separator < 0 || !int.TryParse(tag[(separator + TierSegment.Length)..], out var ordinal)) continue;

            var rarityTier = rarities.Tier(PropertyWalk.Get(tier, RarityField)?.GenericValue);

            _byTierTag[tag] = new GemSize(typeName, ordinal, name, power)
            {
                RarityTier = rarityTier,
                Rarity = rarities.GemLabel(rarityTier),
                IconPath = PropertyWalk.GetString(tier, TierIconField),
            };
        }
    }

    private void ReadNodes(
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
                new GemNode(typeName, index + 1, required)
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

public sealed class RarityNames
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemLootRarityRuleSet";

    private const string TierField = "RarityTier";
    private const string EnumSeparator = "::";

    private readonly Dictionary<int, string> _byOrdinal = [];

    public int Count => _byOrdinal.Count;

    public IEnumerable<int> Tiers => _byOrdinal.Keys.Order();

    private static readonly Dictionary<string, string> Renamed =
        new(StringComparer.OrdinalIgnoreCase) { ["Champion"] = "Heroic", ["Heroic"] = "Regal" };

    private static readonly Dictionary<string, string> GemRenamed =
        new(StringComparer.OrdinalIgnoreCase) { ["Champion"] = "Flawless" };

    public RarityNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var declared = new HashSet<int>();
        string? enumName = null;

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                var value = PropertyWalk.Get(row, TierField)?.GenericValue;

                declared.Add(PropertyWalk.AsOrdinal(value) ?? 0);

                if (PropertyWalk.AsString(value) is { } text &&
                    text.IndexOf(EnumSeparator, StringComparison.Ordinal) is var separator and > 0)
                {
                    enumName ??= text[..separator];
                }
            }
        }

        if (declared.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so nothing says what a rarity ordinal is and every gem would " +
                "export without one.");
        }

        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException(
                $"No mappings are loaded, so nothing names the {TierField} ordinals.");

        if (enumName is null)
        {
            throw new InvalidOperationException(
                $"No {RowStructure} row writes its {TierField} as an enum member, so nothing names the enum the " +
                "rarity ladder is read from.");
        }

        var members = EnumOrdinals.Members(mappings, enumName);

        foreach (var ordinal in declared)
        {
            _byOrdinal[ordinal] = members.TryGetValue(ordinal, out var name)
                ? name
                : throw new InvalidOperationException(
                    $"A loot rule set row declares {TierField} {ordinal}, and {enumName} has no member at it.");
        }
    }

    public string? Label(int? ordinal) => ordinal is { } tier ? _byOrdinal.GetValueOrDefault(tier) : null;

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

    public string? GemLabel(int? ordinal) =>
        Label(ordinal) is { } name ? GemRenamed.GetValueOrDefault(name) ?? name : null;

    public string Printed(int ordinal)
    {
        if (Label(ordinal) is not { } name)
        {
            throw new InvalidOperationException(
                $"Rarity tier {ordinal} names nothing. The loot rule set rows no longer cover the whole ladder.");
        }

        return Renamed.GetValueOrDefault(name) ?? name;
    }

    public IEnumerable<string> MissingRenames =>
        Renamed.Keys.Where(name => !_byOrdinal.ContainsValue(name));
}
