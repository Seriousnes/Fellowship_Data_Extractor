namespace Fellowship.SDK.Calculations;

public sealed record AttunementUpgradeRun(int First, int Last, double Cost, int Currency);

public static class AttunementTrees
{
    public static AttunementTreeEntry? Find(string? tag, SettingsFile settings) =>
        tag is null
            ? null
            : settings.AttunementTrees.FirstOrDefault(tree => tree.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

    /// <summary>Augmentations the tree attunes: one per row past the root.</summary>
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

    public static IReadOnlyList<AttunementTreeNode> Infusable(AttunementTreeEntry tree) =>
        [.. tree.Nodes.Where(node => !Root(node)).OrderBy(node => node.UITreeIndex)];

    public static IReadOnlyList<AttunementUpgradeRun> UpgradeRuns(SettingsFile settings)
    {
        var runs = new List<AttunementUpgradeRun>();

        foreach (var cost in settings.AttunementNodeUpgradeCosts.OrderBy(cost => cost.Rank))
        {
            if (runs.Count > 0 && runs[^1] is var last &&
                last.Last + 1 == cost.Rank && last.Cost == cost.Cost && last.Currency == cost.Currency)
            {
                runs[^1] = last with { Last = cost.Rank };
                continue;
            }

            runs.Add(new AttunementUpgradeRun(cost.Rank, cost.Rank, cost.Cost, cost.Currency));
        }

        return runs;
    }
}
