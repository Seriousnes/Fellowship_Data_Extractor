namespace FellowshipExtractor.Extraction;

public sealed record FoldedPowers(
    IReadOnlyList<Entity> Entities,
    IReadOnlyDictionary<(EntityType Type, int Id), int> Redirects);

public static class AugmentationPowers
{
    public static FoldedPowers Fold(IReadOnlyList<Entity> entities)
    {
        var rows = entities
            .Where(entity => entity is { Type: { } type, Table: RowTable } && AugmentationTypes.Is(type))
            .ToArray();

        var powers = entities
            .Where(entity => entity is { Type: { } type, Table: { } table } && AugmentationTypes.Is(type) &&
                             table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            .ToLookup(entity => (entity.NetId!.Value, entity.PrimaryStat));

        var redirects = new Dictionary<(EntityType, int), int>();
        var folded = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        var filled = new Dictionary<Entity, Entity>(ReferenceEqualityComparer.Instance);
        var ids = rows.Select(row => (row.Type!.Value, row.NetId!.Value)).ToHashSet();

        foreach (var row in rows)
        {
            if (row.Augmentation is not { } granted) continue;
            if (powers[(granted, row.PrimaryStat)].SingleOrDefault() is not { } power) continue;

            if (ids.Contains((row.Type!.Value, granted)) && granted != row.NetId)
            {
                throw new InvalidOperationException(
                    $"{row.Type} {row.NetId} grants augmentation {granted}, and {granted} is also the id of another " +
                    $"{row.Type}, so a reference to the power could not be told from a reference to that row.");
            }

            folded.Add(power);
            filled[row] = row with
            {
                Description = row.Description ?? power.Description,
                DescriptionsByHero = row.DescriptionsByHero ?? power.DescriptionsByHero,
                Placeholders = row.Placeholders ?? power.Placeholders,
                Icon = row.Icon ?? power.Icon,
                Cooldown = row.Cooldown ?? power.Cooldown,
                Duration = row.Duration ?? power.Duration,
                ScalesOff = row.ScalesOff ?? power.ScalesOff,
                Granted = row.Granted ?? power.Granted,
                Generates = row.Generates ?? power.Generates,
            };
            redirects[(row.Type!.Value, granted)] = row.NetId!.Value;
        }

        var unfolded = powers.SelectMany(group => group).Where(power => !folded.Contains(power)).ToArray();

        if (unfolded.Length > 0)
        {
            throw new InvalidOperationException(
                $"{unfolded.Length} augmentation powers no {RowTable} row grants: " +
                string.Join(", ", unfolded.Select(power => $"{power.NetId} {power.DisplayName ?? power.Name}")));
        }

        return new FoldedPowers(
            [.. entities.Where(entity => !folded.Contains(entity)).Select(entity => filled.GetValueOrDefault(entity, entity))],
            redirects);
    }

    private const string RowTable = AugmentationRows.Table;
}
