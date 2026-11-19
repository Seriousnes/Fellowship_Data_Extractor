namespace FellowshipExtractor.Extraction;

public static class AdditionalAbilities
{
    /// <summary>
    /// Fills each listing ability from the abilities its presentation row lists under
    /// AdditionalGameplayAbilityAssets, and links each of those abilities back to the listing ability by the
    /// call that casts it: ReplaceAbility, a SendGameplayEvent it is triggered by, or CrTriggerUtilityAbility.
    /// The listing ability keeps the filled fields because the row's tooltip describes them.
    /// </summary>
    public static IReadOnlyList<Entity> Fold(IReadOnlyList<Entity> entities, AbilityPresentationIndex presentation)
    {
        var hosts = entities
            .Where(entity => entity is { Type: EntityType.Ability, Name: not null })
            .GroupBy(entity => entity.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var replaced = entities
            .SelectMany(entity => entity.Replacements ?? [])
            .Select(pair => pair.Split(AbilityReplacements.Separator))
            .SelectMany(parts => new[] { (parts[0], parts[1]), (parts[1], parts[0]) })
            .ToHashSet();

        var helpers = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
        var linked = new Dictionary<Entity, Entity>(ReferenceEqualityComparer.Instance);

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Ability, Name: { } name, NetId: { } id }) continue;
            if (presentation.HostOf(name) is not { } host || !hosts.TryGetValue(host, out var records)) continue;
            if (records.Exists(record => record.NetId == id)) continue;

            if (!helpers.TryGetValue(host, out var list)) helpers[host] = list = [];

            list.Add(entity);
            linked[entity] = Linked(entity, records[0], replaced);
        }

        return
        [
            .. entities.Select(entity => linked.TryGetValue(entity, out var helper)
                ? helper
                : entity is { Type: EntityType.Ability, Name: { } name } && helpers.TryGetValue(name, out var list)
                    ? EntityResolver.FillFrom(entity, list) with
                    {
                        AdditionalAbilities = [.. list.OrderBy(helper => helper.NetId).Select(Source)],
                    }
                    : entity),
        ];
    }

    private static Entity Linked(Entity helper, Entity host, HashSet<(string, string)> replaced)
    {
        var source = Source(host);

        if (replaced.Contains((host.Name!, helper.Name!))) return helper with { Replaces = host.NetId };

        if (host.UtilityAbilities?.Contains(helper.Name!, StringComparer.OrdinalIgnoreCase) == true)
        {
            return helper with { SubcastOf = source };
        }

        var received = (helper.TriggerTags ?? []).Concat(helper.WaitedEvents ?? []);

        return received.Intersect(host.SentEvents ?? [], StringComparer.Ordinal).Any()
            ? helper with { TriggeredBy = source }
            : helper;
    }

    private static IconSource Source(Entity entity) => new(entity.Table!, entity.NetId!.Value, entity.Name!);
}
