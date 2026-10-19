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

    public static IReadOnlyList<IReadOnlyList<TraitTreeNode>> Rows(TraitTreeEntry tree) =>
    [
        .. tree.Nodes
            .GroupBy(node => node.TraitRowIndex)
            .OrderBy(row => row.Key)
            .Select(row => (IReadOnlyList<TraitTreeNode>)[.. row.OrderBy(node => node.UITreeIndex)]),
    ];
}
