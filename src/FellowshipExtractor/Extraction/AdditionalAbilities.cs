namespace FellowshipExtractor.Extraction;

public sealed record FoldedAbilities(
    IReadOnlyList<Entity> Entities,
    IReadOnlyDictionary<(EntityType Type, int Id), int> Redirects);

public static class AdditionalAbilities
{
    public static FoldedAbilities Fold(IReadOnlyList<Entity> entities, AbilityPresentationIndex presentation)
    {
        var hosts = entities
            .Where(entity => entity is { Type: EntityType.Ability, Name: not null })
            .GroupBy(entity => entity.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var helpers = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
        var redirects = new Dictionary<(EntityType, int), int>();
        var folded = new HashSet<Entity>(ReferenceEqualityComparer.Instance);

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Ability, Name: { } name, NetId: { } id }) continue;
            if (presentation.HostOf(name) is not { } host || !hosts.TryGetValue(host, out var records)) continue;
            if (records.Exists(record => record.NetId == id)) continue;

            if (!helpers.TryGetValue(host, out var list)) helpers[host] = list = [];

            list.Add(entity);
            folded.Add(entity);
            redirects[(EntityType.Ability, id)] = records[0].NetId!.Value;
        }

        return new FoldedAbilities(
            [
                .. entities
                    .Where(entity => !folded.Contains(entity))
                    .Select(entity => entity is { Type: EntityType.Ability, Name: { } name } &&
                        helpers.TryGetValue(name, out var list)
                            ? EntityResolver.FillFrom(entity, list)
                            : entity),
            ],
            redirects);
    }
}
