using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class EffectRoles
{
    private const string AbilityPrefix = "GA_";
    private const string EffectPrefix = "GE_";

    private readonly Dictionary<string, int> _abilities = new(StringComparer.Ordinal);

    private readonly HashSet<string> _codenames;

    public EffectRoles(IReadOnlyList<Entity> entities, IEnumerable<string> codenames)
    {
        _codenames = new HashSet<string>(codenames, StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Ability, NetId: { } id, Name: { } tag }) continue;
            if (!tag.StartsWith(AbilityPrefix, StringComparison.Ordinal)) continue;

            var core = tag[AbilityPrefix.Length..];

            if (!_abilities.TryGetValue(core, out var held) || id < held) _abilities[core] = id;
        }

        var parts = 0;
        var roles = 0;
        var withheld = 0;

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Effect, Name: { } tag }) continue;
            if (Stem(tag) is not { } stem) continue;

            parts++;

            if (Named(stem.Tail) is null) withheld++;
            else roles++;
        }

        Parts = parts;
        Roles = roles;
        Withheld = withheld;
    }

    public int Abilities => _abilities.Count;

    public int Parts { get; }

    public int Roles { get; }

    public int Withheld { get; }

    public int? PartOf(string? tag) => Stem(tag)?.Ability;

    public string? Role(string? tag) => Stem(tag) is { } stem ? Named(stem.Tail) : null;

    private string? Named(string tail)
    {
        var role = string.Empty;

        foreach (var word in Words(tail))
        {
            if (_codenames.Contains(word)) return null;

            role += word;
        }

        return role.Length > 0 ? role : null;
    }

    private static IEnumerable<string> Words(string tail)
    {
        foreach (var part in tail.Split('_'))
        {
            var start = 0;

            for (var at = 1; at <= part.Length; at++)
            {
                if (at < part.Length && (!char.IsUpper(part[at]) || char.IsUpper(part[at - 1]))) continue;

                if (at > start) yield return part[start..at];

                start = at;
            }
        }
    }

    private (int Ability, string Tail)? Stem(string? tag)
    {
        if (tag is null || !tag.StartsWith(EffectPrefix, StringComparison.Ordinal)) return null;

        var core = tag[EffectPrefix.Length..];

        for (var at = core.LastIndexOf('_'); at > 0; at = core.LastIndexOf('_', at - 1))
        {
            if (_abilities.TryGetValue(core[..at], out var ability)) return (ability, core[(at + 1)..]);
        }

        return null;
    }
}
