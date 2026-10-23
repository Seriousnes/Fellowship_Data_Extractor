using System.Text.RegularExpressions;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class SpiritAbilityReferences
{
    private static readonly EntityType[] Precedence = [EntityType.Ability, EntityType.Talent, EntityType.Effect];

    private static readonly Regex Quoted = new(
        @"(?<!\w)(?<quote>[""'])(?<name>[^""'\r\n]+)\k<quote>(?!\w)",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<Entity> Mark(IReadOnlyList<Entity> entities)
    {
        var candidates = entities
            .Where(entity => entity is { Type: { } type, Table: not null, NetId: not null, NamedAfter: null } &&
                Precedence.Contains(type))
            .ToArray();

        var named = First(candidates.SelectMany(entity => Keys(entity).Select(key => (Key: key, Entity: entity))));

        var quoted = First(
            candidates
                .Where(entity => DocumentProjection.Name(entity) is not null)
                .Select(entity => (Key: DocumentProjection.Name(entity)!, Entity: entity)),
            StringComparer.Ordinal);

        var byHero = named.Keys
            .GroupBy(key => key.Hero, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Pattern(group.Select(key => key.Name)), StringComparer.Ordinal);

        return
        [
            .. entities.Select(entity =>
                entity is { Type: EntityType.Hero, SpiritAbility: { Length: > 0 } text } &&
                DocumentProjection.Name(entity) is { } hero
                    ? entity with
                    {
                        SpiritAbilityReferences = Matches(text, hero, byHero.GetValueOrDefault(hero), named, quoted)
                            .Distinct()
                            .Select(target => new IconSource(target.Table!, target.NetId!.Value, string.Empty))
                            .ToArray() is { Length: > 0 } found
                            ? found
                            : null,
                    }
                    : entity),
        ];
    }

    private static IEnumerable<Entity> Matches(
        string text,
        string hero,
        Regex? pattern,
        Dictionary<(string Hero, string Name), Entity> named,
        Dictionary<string, Entity> quoted) =>
        (pattern?.Matches(text).Select(match => (match.Index, Entity: named[(hero, match.Value)])) ?? [])
            .Concat(
                Quoted.Matches(text)
                    .Where(match => !named.ContainsKey((hero, match.Groups["name"].Value)) &&
                        quoted.ContainsKey(match.Groups["name"].Value))
                    .Select(match => (match.Index, Entity: quoted[match.Groups["name"].Value])))
            .OrderBy(match => match.Index)
            .Select(match => match.Entity);

    private static Dictionary<TKey, Entity> First<TKey>(
        IEnumerable<(TKey Key, Entity Entity)> pairs,
        IEqualityComparer<TKey>? comparer = null) where TKey : notnull =>
        pairs
            .GroupBy(pair => pair.Key, comparer)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(pair => pair.Entity)
                    .OrderBy(entity => Array.IndexOf(Precedence, entity.Type!.Value))
                    .ThenBy(entity => entity.NetId)
                    .First(),
                comparer);

    private static Regex Pattern(IEnumerable<string> names) =>
        new(
            @"\b(?:" + string.Join("|", names.OrderByDescending(name => name.Length).Select(Regex.Escape)) +
            @")\b(?!\s+\p{Lu})",
            RegexOptions.CultureInvariant);

    private static IEnumerable<(string Hero, string Name)> Keys(Entity entity) =>
        DocumentProjection.Name(entity) is { } shown && entity.OwningHeroes is { Count: > 0 } heroes
            ? heroes.Select(hero => (hero, shown))
            : [];
}
