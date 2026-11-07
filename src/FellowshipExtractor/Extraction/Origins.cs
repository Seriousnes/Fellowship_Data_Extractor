using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class Origins
{
    private static readonly IReadOnlySet<EntityType> Held =
        new HashSet<EntityType> { EntityType.Ability, EntityType.Effect, EntityType.Talent };

    public static IReadOnlyDictionary<(EntityType Type, int Id), IReadOnlyList<(EntityType Type, int Id)>> Of(
        IReadOnlyList<EntityDocument> documents)
    {
        var reachers = Reachability.Reachers(documents);
        var byKey = new Dictionary<(EntityType Type, int Id), EntityDocument>();

        foreach (var document in documents) byKey.TryAdd((EntityTypes.TypeOf(document), document.Id), document);

        var heroes = documents
            .OfType<HeroDocument>()
            .Where(hero => hero.Name is not null)
            .GroupBy(hero => hero.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (EntityType.Hero, group.First().Id), StringComparer.OrdinalIgnoreCase);

        // Talent-slot ids repeat for each hero, so a talent reads its hero from the slots naming it, never by slot id.
        var slotted = documents
            .OfType<TalentSlotDocument>()
            .ToLookup(slot => (slot.Talent.Type, slot.Talent.Id), slot => slot.Hero);

        var resolved = new Dictionary<(EntityType Type, int Id), IReadOnlyList<(EntityType Type, int Id)>>();
        var visiting = new HashSet<(EntityType Type, int Id)>();

        foreach (var key in byKey.Keys.Where(key => Held.Contains(key.Type)))
        {
            if (Roots(key) is { Count: > 0 } roots) resolved[key] = roots;
        }

        return resolved;

        IReadOnlyList<(EntityType Type, int Id)> Roots((EntityType Type, int Id) key)
        {
            if (!Held.Contains(key.Type)) return [key];
            if (resolved.TryGetValue(key, out var known)) return known;
            if (!visiting.Add(key)) return [];

            var document = byKey.GetValueOrDefault(key);

            IReadOnlyList<(EntityType Type, int Id)> found = document switch
            {
                AbilityDocument { Heroes: [_, ..] owners } => [.. owners.SelectMany(Hero)],
                EffectDocument { Heroes: [_, ..] owners } => [.. owners.SelectMany(Hero)],
                _ =>
                [
                    .. slotted[key].SelectMany(Hero),
                    .. reachers.GetValueOrDefault(key, [])
                        .Where(reacher => reacher.Type != EntityType.TalentSlot)
                        .SelectMany(Roots),
                ],
            };

            visiting.Remove(key);

            IReadOnlyList<(EntityType Type, int Id)> roots =
                [.. found.Distinct().OrderBy(root => root.Type).ThenBy(root => root.Id)];

            if (roots.Count > 0) resolved[key] = roots;

            return roots;
        }

        IReadOnlyList<(EntityType Type, int Id)> Hero(Hero hero) =>
            heroes.TryGetValue(hero.ToString(), out var found) ? [found] : [];
    }
}
