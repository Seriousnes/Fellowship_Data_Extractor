using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class ItemTraitTrees
{
    public const string RowStructure = "/Script/fellowship.CRItemTraitTreeConfiguration";
    public const string CostTable = "CT_TraitNodeUpgradeCosts";

    public const string ItemField = "TraitTreeConfigID";

    private const string TagField = "TreeConfigID";
    private const string TopologyField = "TraitTreeTopology";
    private const string WidgetField = "TreeWidget";

    private const string GraphClass = "CRTraitTreeGraph";
    private const string NodeClass = "CRTraitTreeGraphNode";

    private const string RecipeUnlocksField = "UseRecipeRowUnlocks";
    private const string GenerationUnlocksField = "RowsUnlockedAtItemGeneration";
    private const string RequirementField = "RowToUpgradeRequirement";
    private const string AllNodesField = "AllNodes";

    private const string RowField = "TraitRowIndex";
    private const string UiIndexField = "UITreeIndex";
    private const string ParentsField = "ParentNodes";
    private const string ChildrenField = "ChildrenNodes";
    private const string CategoriesField = "TraitCategories";
    private const string RandomizedField = "RandomizedTraitFromTraitCategories";
    private const string RatingField = "MinDungeonRatingAtGenerationTime";
    private const string ItemLevelField = "EnableAtItemLevel";
    private const string OverrideField = "OverrideMaxTraitRank";
    private const string OverrideRankField = "MaxTraitRankOverride";

    private const string CurrencyTable = "ItemID";
    private const int LinearInterpolation = 0;
    private const string InterpolationField = "InterpMode";

    private readonly Dictionary<string, TraitTreeEntry> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _widgetByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> _named = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TraitNodeUpgradeCost> UpgradeCosts { get; }

    public TraitTreeLayouts Layouts { get; }

    public int Count => _byTag.Count;

    public ItemTraitTrees(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        IReadOnlyList<TagRecord> tags,
        int maxTraitRank)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;
                if (PropertyWalk.GetString(row, TopologyField) is not { } topology) continue;

                if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(topology), out var package))
                {
                    throw new InvalidOperationException(
                        $"{RowStructure} row {rowName.Text} names {topology} as the topology of {tag}, and no " +
                        "package by that name loads.");
                }

                _byTag[tag] = Read(tag, [.. package.GetExports()]);

                if (PropertyWalk.GetString(row, WidgetField) is { Length: > 0 } widget)
                {
                    _widgetByTag[tag] = Path.GetFileNameWithoutExtension(AbilityPresentationIndex.PackagePath(widget));
                }
            }
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table of {RowStructure} rows in the build, so an item naming a trait tree names nothing.");
        }

        UpgradeCosts = Costs(provider, tags, maxTraitRank);
        Layouts = new TraitTreeLayouts(provider, _widgetByTag.Values);
    }

    public string? Named(string? tag)
    {
        if (tag is null) return null;

        if (!_byTag.ContainsKey(tag))
        {
            throw new InvalidOperationException(
                $"An item names {tag} as its {ItemField}, and no {RowStructure} row declares that tree.");
        }

        _named.Add(tag);

        return tag;
    }

    public IReadOnlyList<TraitTreeEntry> NamedTrees =>
        [.. _named.Select(tag => _byTag[tag]).Select(tree => tree with { Layout = Layouts.For(tree, Widget(tree)) })];

    private string Widget(TraitTreeEntry tree) =>
        _widgetByTag.TryGetValue(tree.Tag, out var widget)
            ? widget
            : throw new InvalidOperationException(
                $"An item names {tree.Tag}, and its {RowStructure} row names no {WidgetField} to draw it with.");

    private static TraitTreeEntry Read(string tag, IReadOnlyList<UObject> exports)
    {
        var graph = exports.FirstOrDefault(export => export.ExportType == GraphClass)
            ?? throw new InvalidOperationException($"The topology of {tag} serializes no {GraphClass}.");

        var nodes = exports.Where(export => export.ExportType == NodeClass).ToArray();

        var indexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var index = PropertyWalk.AsOrdinal(PropertyWalk.Get(node, UiIndexField)?.GenericValue) ?? 0;

            if (indexByPath.ContainsValue(index))
            {
                throw new InvalidOperationException(
                    $"Two nodes of {tag} share {UiIndexField} {index}, so a node cannot be named by its index.");
            }

            indexByPath[node.GetPathName()] = index;
        }

        if (PropertyWalk.Get(graph, AllNodesField)?.GenericValue is UScriptArray all && all.Properties.Count != nodes.Length)
        {
            throw new InvalidOperationException(
                $"{tag} lists {all.Properties.Count} nodes in {AllNodesField} and serializes {nodes.Length}.");
        }

        return new TraitTreeEntry(tag)
        {
            UseRecipeRowUnlocks = PropertyWalk.Get(graph, RecipeUnlocksField)?.GenericValue is true,
            RowsUnlockedAtItemGeneration =
                PropertyWalk.AsOrdinal(PropertyWalk.Get(graph, GenerationUnlocksField)?.GenericValue),
            RowToUpgradeRequirement = Requirements(graph),
            Nodes =
            [
                .. nodes.Select(node => new TraitTreeNode(
                        indexByPath[node.GetPathName()],
                        PropertyWalk.AsOrdinal(PropertyWalk.Get(node, RowField)?.GenericValue) ?? 0)
                    {
                        ParentNodes = Linked(node, ParentsField, indexByPath, tag),
                        ChildrenNodes = Linked(node, ChildrenField, indexByPath, tag),
                        TraitCategories = Categories(node),
                        RandomizedTraitFromTraitCategories =
                            PropertyWalk.Get(node, RandomizedField)?.GenericValue is true,
                        MinDungeonRatingAtGenerationTime =
                            PropertyWalk.AsOrdinal(PropertyWalk.Get(node, RatingField)?.GenericValue),
                        EnableAtItemLevel = PropertyWalk.AsOrdinal(PropertyWalk.Get(node, ItemLevelField)?.GenericValue),
                        MaxTraitRankOverride = PropertyWalk.Get(node, OverrideField)?.GenericValue is true
                            ? PropertyWalk.AsOrdinal(PropertyWalk.Get(node, OverrideRankField)?.GenericValue)
                            : null,
                    })
                    .OrderBy(node => node.UITreeIndex),
            ],
        };
    }

    private static IReadOnlyList<string> Categories(UObject node) =>
        PropertyWalk.Get(node, CategoriesField)?.GenericValue switch
        {
            UScriptArray listed => [.. listed.Properties.Select(tag => PropertyWalk.AsString(tag?.GenericValue)).OfType<string>()],
            var container => PropertyWalk.AsTags(container) ?? [],
        };

    private static IReadOnlyDictionary<int, int> Requirements(UObject graph)
    {
        var requirements = new SortedDictionary<int, int>();

        if (PropertyWalk.Get(graph, RequirementField)?.GenericValue is not UScriptMap map) return requirements;

        foreach (var (key, value) in map.Properties)
        {
            if (PropertyWalk.AsOrdinal(key?.GenericValue) is { } row &&
                PropertyWalk.AsOrdinal(value?.GenericValue) is { } upgrades)
            {
                requirements[row] = upgrades;
            }
        }

        return requirements;
    }

    private static IReadOnlyList<int> Linked(
        UObject node, string field, IReadOnlyDictionary<string, int> indexByPath, string tag)
    {
        if (PropertyWalk.Get(node, field)?.GenericValue is not UScriptArray linked) return [];

        var indices = new List<int>(linked.Properties.Count);

        foreach (var entry in linked.Properties)
        {
            if (PropertyWalk.AsString(entry?.GenericValue) is not { } path) continue;

            indices.Add(indexByPath.TryGetValue(path, out var index)
                ? index
                : throw new InvalidOperationException(
                    $"A node of {tag} links through {field} to {path}, which is no node of the tree."));
        }

        indices.Sort();

        return indices;
    }

    private static IReadOnlyList<TraitNodeUpgradeCost> Costs(
        AbstractFileProvider provider, IReadOnlyList<TagRecord> tags, int maxTraitRank)
    {
        var table = CurveRows.Load(provider, CostTable, "what upgrading a trait tree node costs");
        var costs = new List<TraitNodeUpgradeCost>();

        foreach (var (rowName, row) in table.RowMap)
        {
            var currency = tags.FirstOrDefault(tag =>
                tag.Table.Equals(CurrencyTable, StringComparison.OrdinalIgnoreCase) &&
                tag.Name.Equals(rowName.Text, StringComparison.OrdinalIgnoreCase));

            if (currency.Name is null)
            {
                throw new InvalidOperationException(
                    $"{CostTable} row {rowName.Text} names no {CurrencyTable} tag, so the currency a trait upgrade " +
                    "is paid in is unnamed.");
            }

            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, InterpolationField)?.GenericValue) is not LinearInterpolation)
            {
                throw new InvalidOperationException(
                    $"{CostTable}'s {rowName.Text} row no longer interpolates linearly, so the cost it states " +
                    "between two ranks is no longer the lerp it is read as.");
            }

            var keys = CurveRows.Keys(row);
            if (keys.Count == 0) continue;

            for (var rank = 1; rank <= maxTraitRank; rank++)
            {
                costs.Add(new TraitNodeUpgradeCost(rank, CurveRows.Evaluate(keys, rank), currency.NetId));
            }
        }

        return costs;
    }
}
