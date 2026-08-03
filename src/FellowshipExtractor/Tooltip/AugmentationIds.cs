namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The netId behind a CRCharacterAugmentationAsset reference.
///
/// A gem node, an item trait and a set bonus all name their mechanics by asset path, and the export
/// carries no paths. The augmentation already exports as a line of its own, so the id of that line
/// is the whole reference: it points a reader at the constants, granted tags and effects without
/// repeating any of them.
/// </summary>
public sealed class AugmentationIds
{
    private readonly Dictionary<string, int> _byName = new(StringComparer.OrdinalIgnoreCase);

    public AugmentationIds(IReadOnlyList<TagRecord> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            {
                _byName.TryAdd(tag.Name, tag.NetId);
            }
        }
    }

    /// <summary>The netId of the augmentation a path names, or null when no tag names it.</summary>
    public int? For(string? assetPath) =>
        assetPath is not null &&
        _byName.TryGetValue(AbilityPresentationIndex.AssetName(assetPath), out var netId)
            ? netId
            : null;
}
