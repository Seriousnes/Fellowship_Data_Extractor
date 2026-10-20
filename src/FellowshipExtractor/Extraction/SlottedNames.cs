namespace FellowshipExtractor.Extraction;

public static class SlottedNames
{
    public static IReadOnlyList<Entity> Mark(IReadOnlyList<Entity> entities, Func<string, bool> slotted)
    {
        var holders = entities
            .Where(entity => entity is { Type: EntityType.Ability, Name: { } name, Table: not null, NetId: not null } &&
                slotted(name))
            .SelectMany(entity => Keys(entity).Select(key => (Key: key, Entity: entity)))
            .GroupBy(pair => pair.Key)
            .Where(group => group.Select(pair => pair.Entity.NetId).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.First().Entity);

        return
        [
            .. entities.Select(entity =>
                entity is { Type: EntityType.Ability, Name: { } name } && !slotted(name) &&
                Keys(entity).Select(key => holders.GetValueOrDefault(key)).Distinct().ToArray() is [{ } holder]
                    ? entity with { NamedAfter = new IconSource(holder.Table!, holder.NetId!.Value, string.Empty) }
                    : entity),
        ];
    }

    private static IEnumerable<(string Hero, string Name)> Keys(Entity entity) =>
        DocumentProjection.Name(entity) is { } shown && entity.OwningHeroes is { Count: > 0 } heroes
            ? heroes.Select(hero => (hero, shown))
            : [];
}
