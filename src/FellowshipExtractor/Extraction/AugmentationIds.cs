namespace FellowshipExtractor.Extraction;

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

    public int? For(string? assetPath) =>
        assetPath is not null &&
        _byName.TryGetValue(AbilityPresentationIndex.AssetName(assetPath), out var netId)
            ? netId
            : null;
}
