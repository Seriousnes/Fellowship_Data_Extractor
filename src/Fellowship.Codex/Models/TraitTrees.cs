using System.Globalization;

using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class TraitTrees
{
    private const char PairSeparator = ',';
    private const char NodeSeparator = '.';

    public static TraitTreeEntry? Find(string? tag, SettingsFile settings) =>
        tag is null
            ? null
            : settings.TraitTrees.FirstOrDefault(tree => tree.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<TraitDocument> InCategory(string categoryTag, IEntityStoreService store) =>
    [
        .. store.Entities
            .OfType<TraitDocument>()
            .Where(trait => categoryTag.Equals(trait.CategoryTag, StringComparison.OrdinalIgnoreCase))
            .OrderBy(trait => PublicTypes.NameOf(trait), StringComparer.Ordinal),
    ];

    public static IReadOnlyList<TraitDocument> Offered(TraitTreeNode node, IEntityStoreService store) =>
        [.. node.TraitCategories.SelectMany(category => InCategory(category, store)).DistinctBy(trait => trait.Id)];

    public static string? CategoryName(string categoryTag, IEntityStoreService store) =>
        InCategory(categoryTag, store).Select(trait => trait.Category).FirstOrDefault(name => name is not null);

    public static int? Capacity(string? tag, SettingsFile settings) =>
        Find(tag, settings) is { } tree ? Imbued(tree) : null;

    public static int? LargestCapacity(SettingsFile settings) =>
        settings.TraitTrees.Count == 0 ? null : settings.TraitTrees.Max(Imbued);

    private static int Imbued(TraitTreeEntry tree) =>
        Rows(tree).Count(row => row.Any(node => node.ParentNodes.Count > 0));

    public static IReadOnlyList<IReadOnlyList<TraitTreeNode>> Rows(TraitTreeEntry tree) =>
    [
        .. tree.Nodes
            .GroupBy(node => node.TraitRowIndex)
            .OrderBy(row => row.Key)
            .Select(row => (IReadOnlyList<TraitTreeNode>)[.. row.OrderBy(node => node.UITreeIndex)]),
    ];

    public static bool Root(TraitTreeNode node) => node.ParentNodes.Count == 0;

    public static IReadOnlyList<ItemDocument> Items(IEntityStoreService store) =>
    [
        .. store.Entities
            .OfType<ItemDocument>()
            .Where(item => Find(item.TraitTree, store.Settings) is not null)
            .DistinctBy(item => item.Id)
            .OrderBy(item => PublicTypes.NameOf(item), StringComparer.OrdinalIgnoreCase),
    ];

    public static IReadOnlyDictionary<int, TraitDocument> Read(
        string? picks, TraitTreeEntry tree, IEntityStoreService store)
    {
        var asked = new Dictionary<int, TraitDocument>();

        foreach (var pair in (picks ?? string.Empty).Split(
                     PairSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (pair.Split(NodeSeparator) is not [var nodeText, var traitText] ||
                !int.TryParse(nodeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var node) ||
                !int.TryParse(traitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                store.All(EntityType.Trait, id).OfType<TraitDocument>().FirstOrDefault() is not { } trait)
            {
                continue;
            }

            asked.TryAdd(node, trait);
        }

        return Allocated(tree, asked);
    }

    public static IReadOnlyDictionary<int, TraitDocument> Allocated(
        TraitTreeEntry tree, IReadOnlyDictionary<int, TraitDocument> asked)
    {
        var kept = new Dictionary<int, TraitDocument>();

        foreach (var row in Rows(tree))
        {
            foreach (var node in row)
            {
                if (Root(node) || !asked.TryGetValue(node.UITreeIndex, out var trait)) continue;
                if (!Allocatable(tree, kept, node)) continue;
                if (!node.TraitCategories.Contains(trait.CategoryTag ?? string.Empty, StringComparer.OrdinalIgnoreCase)) continue;
                if (kept.Values.Any(taken => taken.Id == trait.Id)) continue;

                kept[node.UITreeIndex] = trait;
            }
        }

        return kept;
    }

    public static bool Allocatable(
        TraitTreeEntry tree, IReadOnlyDictionary<int, TraitDocument> allocated, TraitTreeNode node) =>
        !Root(node) &&
        !tree.Nodes.Any(other => other.TraitRowIndex == node.TraitRowIndex && allocated.ContainsKey(other.UITreeIndex)) &&
        node.ParentNodes.Any(parent => tree.Nodes.FirstOrDefault(other => other.UITreeIndex == parent) is { } linked &&
                                       (Root(linked) || allocated.ContainsKey(parent)));

    public static bool Lit(TraitTreeNode node, IReadOnlyDictionary<int, TraitDocument> allocated) =>
        Root(node) || allocated.ContainsKey(node.UITreeIndex);

    public static string Write(IReadOnlyDictionary<int, TraitDocument> allocated) =>
        string.Join(
            PairSeparator,
            allocated.OrderBy(pick => pick.Key).Select(pick => string.Create(
                CultureInfo.InvariantCulture, $"{pick.Key}{NodeSeparator}{pick.Value.Id}")));
}
