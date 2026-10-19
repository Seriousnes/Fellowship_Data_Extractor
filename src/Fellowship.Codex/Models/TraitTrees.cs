using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class TraitTrees
{
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
}
