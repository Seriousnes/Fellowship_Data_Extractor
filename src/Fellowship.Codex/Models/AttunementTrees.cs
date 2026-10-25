using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class AttunementTrees
{
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

    public static IReadOnlyList<AttunementTreeNode> Infusable(AttunementTreeEntry tree) =>
        [.. tree.Nodes.Where(node => !Root(node)).OrderBy(node => node.UITreeIndex)];

    public static IReadOnlyList<HeroDocument> Heroes(IEntityStoreService store) =>
    [
        .. store.Entities
            .OfType<HeroDocument>()
            .DistinctBy(hero => hero.Id)
            .OrderBy(hero => hero.Name, StringComparer.OrdinalIgnoreCase),
    ];

    public static IReadOnlyList<ItemDocument> Items(AttunementTreeEntry tree, HeroDocument hero, IEntityStoreService store) =>
    [
        .. store.Entities
            .OfType<ItemDocument>()
            .Where(item => tree.Tag.Equals(item.AttunementTree, StringComparison.OrdinalIgnoreCase) &&
                           Wears(hero, item))
            .DistinctBy(item => item.Id)
            .OrderBy(item => PublicTypes.NameOf(item), StringComparer.OrdinalIgnoreCase),
    ];

    private static bool Wears(HeroDocument hero, ItemDocument item) =>
        item.Hero?.ToString() == hero.Name || (item.Heroes ?? []).Any(each => each.ToString() == hero.Name);

    public static AttunementTreePlan Read(string? code, AttunementTreeEntry tree, IEntityStoreService store)
    {
        var nodes = Infusable(tree);
        var bytes = PlanCode.Decode(code);
        var numbers = PlanCode.Numbers(bytes, PlanCode.MaskLength(nodes.Count));

        var hero = numbers.Count > 0 && numbers[0] > 0
            ? store.All(EntityType.Hero, numbers[0]).OfType<HeroDocument>().FirstOrDefault()
            : null;

        var item = hero is not null && numbers.Count > 1 && numbers[1] > 0
            ? Items(tree, hero, store).FirstOrDefault(each => each.Id == numbers[1])
            : null;

        var path = nodes
            .Where((node, at) => PlanCode.Bit(bytes, at))
            .Select(node => node.UITreeIndex)
            .ToHashSet();

        var assigned = new Dictionary<int, AugmentationDocument>();

        foreach (var (node, id) in nodes.Zip(numbers.Skip(2)))
        {
            if (id > 0 && Offered(node, store, hero?.Name).FirstOrDefault(augmentation => augmentation.Id == id) is { } augmentation)
            {
                assigned[node.UITreeIndex] = augmentation;
            }
        }

        return Planned(tree, hero, item, assigned, path);
    }

    public static AttunementTreePlan Planned(
        AttunementTreeEntry tree,
        HeroDocument? hero,
        ItemDocument? item,
        IReadOnlyDictionary<int, AugmentationDocument> assigned,
        IReadOnlySet<int> path)
    {
        var kept = new Dictionary<int, AugmentationDocument>();

        foreach (var node in Infusable(tree))
        {
            if (!assigned.TryGetValue(node.UITreeIndex, out var augmentation)) continue;
            if (!node.AugmentationCategories.Contains(augmentation.CategoryTag ?? string.Empty, StringComparer.OrdinalIgnoreCase)) continue;
            if (kept.Values.Any(taken => taken.Id == augmentation.Id)) continue;

            kept[node.UITreeIndex] = augmentation;
        }

        var followed = new HashSet<int>();

        foreach (var node in Rows(tree).SelectMany(row => row))
        {
            if (path.Contains(node.UITreeIndex) && Allocatable(tree, kept, followed, node)) followed.Add(node.UITreeIndex);
        }

        return new AttunementTreePlan(hero, item, kept, followed);
    }

    public static bool Allocatable(AttunementTreeEntry tree, AttunementTreePlan plan, AttunementTreeNode node) =>
        Allocatable(tree, plan.Assigned, plan.Path, node);

    private static bool Allocatable(
        AttunementTreeEntry tree,
        IReadOnlyDictionary<int, AugmentationDocument> assigned,
        IReadOnlySet<int> path,
        AttunementTreeNode node) =>
        !Root(node) &&
        assigned.ContainsKey(node.UITreeIndex) &&
        !tree.Nodes.Any(other => other.AugmentationRowIndex == node.AugmentationRowIndex && path.Contains(other.UITreeIndex)) &&
        node.ParentNodes.Any(parent => tree.Nodes.FirstOrDefault(other => other.UITreeIndex == parent) is { } linked &&
                                       (Root(linked) || path.Contains(parent)));

    public static bool Lit(AttunementTreeNode node, AttunementTreePlan plan) =>
        Root(node) || plan.Path.Contains(node.UITreeIndex);

    public static string Write(AttunementTreeEntry tree, AttunementTreePlan plan)
    {
        var nodes = Infusable(tree);

        return PlanCode.Encode(
        [
            .. PlanCode.Mask([.. nodes.Select(node => plan.Path.Contains(node.UITreeIndex))]),
            .. PlanCode.Numbers(
            [
                plan.Hero?.Id ?? 0,
                plan.Item?.Id ?? 0,
                .. nodes.Select(node => plan.Assigned.GetValueOrDefault(node.UITreeIndex)?.Id ?? 0),
            ]),
        ]);
    }
}

public sealed record AttunementTreePlan(
    HeroDocument? Hero,
    ItemDocument? Item,
    IReadOnlyDictionary<int, AugmentationDocument> Assigned,
    IReadOnlySet<int> Path)
{
    public static AttunementTreePlan Empty { get; } = new(null, null, new Dictionary<int, AugmentationDocument>(), new HashSet<int>());
}
