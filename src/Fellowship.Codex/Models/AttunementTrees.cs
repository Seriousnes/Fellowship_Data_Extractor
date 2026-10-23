using System.Globalization;

using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class AttunementTrees
{
    private const char PairSeparator = ',';
    private const char NodeSeparator = '.';

    public static AttunementTreeEntry? Find(string? tag, SettingsFile settings) =>
        tag is null
            ? null
            : settings.AttunementTrees.FirstOrDefault(tree => tree.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<AugmentationDocument> InCategory(
        string categoryTag, IEntityStoreService store, string? wearer = null) =>
    [
        .. store.Entities
            .OfType<AugmentationDocument>()
            .Where(augmentation => categoryTag.Equals(augmentation.CategoryTag, StringComparison.OrdinalIgnoreCase))
            .Select(augmentation => augmentation.Id)
            .Distinct()
            .Select(id => TooltipView.Augmentation(store, id, wearer)!)
            .OrderBy(augmentation => PublicTypes.NameOf(augmentation), StringComparer.Ordinal),
    ];

    public static IReadOnlyList<AugmentationDocument> Offered(
        AttunementTreeNode node, IEntityStoreService store, string? wearer = null) =>
    [
        .. node.AugmentationCategories.SelectMany(category => InCategory(category, store, wearer)).DistinctBy(augmentation => augmentation.Id),
    ];

    public static string? CategoryName(string categoryTag, IEntityStoreService store) =>
        InCategory(categoryTag, store).Select(augmentation => augmentation.Category).FirstOrDefault(name => name is not null);

    public static int? Capacity(string? tag, SettingsFile settings) =>
        Find(tag, settings) is { } tree ? Attuned(tree) : null;

    public static int? LargestCapacity(SettingsFile settings) =>
        settings.AttunementTrees.Count == 0 ? null : settings.AttunementTrees.Max(Attuned);

    private static int Attuned(AttunementTreeEntry tree) =>
        Rows(tree).Count(row => row.Any(node => node.ParentNodes.Count > 0));

    public static IReadOnlyList<IReadOnlyList<AttunementTreeNode>> Rows(AttunementTreeEntry tree) =>
    [
        .. tree.Nodes
            .GroupBy(node => node.AugmentationRowIndex)
            .OrderBy(row => row.Key)
            .Select(row => (IReadOnlyList<AttunementTreeNode>)[.. row.OrderBy(node => node.UITreeIndex)]),
    ];

    public static bool Root(AttunementTreeNode node) => node.ParentNodes.Count == 0;

    public static IReadOnlyList<ItemDocument> Items(IEntityStoreService store) =>
    [
        .. store.Entities
            .OfType<ItemDocument>()
            .Where(item => Find(item.AttunementTree, store.Settings) is not null)
            .DistinctBy(item => item.Id)
            .OrderBy(item => PublicTypes.NameOf(item), StringComparer.OrdinalIgnoreCase),
    ];

    public static IReadOnlyList<(string Slot, AttunementTreeEntry Tree)> BySlot(IEntityStoreService store) =>
    [
        .. Items(store)
            .Where(item => item.Slot is not null)
            .GroupBy(item => item.Slot!, StringComparer.OrdinalIgnoreCase)
            .Select(slot => slot.Select(item => item.AttunementTree!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() is [var tag]
                ? (slot.Key, Find(tag, store.Settings)!)
                : throw new InvalidOperationException(
                    $"{slot.Key} items name {slot.Select(item => item.AttunementTree).Distinct().Count()} attunement trees, so " +
                    "the slot no longer names one tree."))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase),
    ];

    public static IReadOnlyDictionary<int, AugmentationDocument> Read(
        string? picks, AttunementTreeEntry tree, IEntityStoreService store, string? wearer = null)
    {
        var asked = new Dictionary<int, AugmentationDocument>();

        foreach (var pair in (picks ?? string.Empty).Split(
                     PairSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (pair.Split(NodeSeparator) is not [var nodeText, var augmentationText] ||
                !int.TryParse(nodeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var node) ||
                !int.TryParse(augmentationText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                TooltipView.Augmentation(store, id, wearer) is not { } augmentation)
            {
                continue;
            }

            asked.TryAdd(node, augmentation);
        }

        return Allocated(tree, asked);
    }

    public static IReadOnlyDictionary<int, AugmentationDocument> Allocated(
        AttunementTreeEntry tree, IReadOnlyDictionary<int, AugmentationDocument> asked)
    {
        var kept = new Dictionary<int, AugmentationDocument>();

        foreach (var row in Rows(tree))
        {
            foreach (var node in row)
            {
                if (Root(node) || !asked.TryGetValue(node.UITreeIndex, out var augmentation)) continue;
                if (!Allocatable(tree, kept, node)) continue;
                if (!node.AugmentationCategories.Contains(augmentation.CategoryTag ?? string.Empty, StringComparer.OrdinalIgnoreCase)) continue;
                if (kept.Values.Any(taken => taken.Id == augmentation.Id)) continue;

                kept[node.UITreeIndex] = augmentation;
            }
        }

        return kept;
    }

    public static bool Allocatable(
        AttunementTreeEntry tree, IReadOnlyDictionary<int, AugmentationDocument> allocated, AttunementTreeNode node) =>
        !Root(node) &&
        !tree.Nodes.Any(other => other.AugmentationRowIndex == node.AugmentationRowIndex && allocated.ContainsKey(other.UITreeIndex)) &&
        node.ParentNodes.Any(parent => tree.Nodes.FirstOrDefault(other => other.UITreeIndex == parent) is { } linked &&
                                       (Root(linked) || allocated.ContainsKey(parent)));

    public static bool Lit(AttunementTreeNode node, IReadOnlyDictionary<int, AugmentationDocument> allocated) =>
        Root(node) || allocated.ContainsKey(node.UITreeIndex);

    public static string Write(IReadOnlyDictionary<int, AugmentationDocument> allocated) =>
        string.Join(
            PairSeparator,
            allocated.OrderBy(pick => pick.Key).Select(pick => string.Create(
                CultureInfo.InvariantCulture, $"{pick.Key}{NodeSeparator}{pick.Value.Id}")));
}
