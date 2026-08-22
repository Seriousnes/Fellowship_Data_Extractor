using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class EffectRoles
{
    private const string AbilityPrefix = "GA_";
    private const string EffectPrefix = "GE_";

    private static readonly HashSet<string> Vocabulary = new(
        [
            "Absorb", "Active", "AllyBuff", "AoeBuff", "AoeDamage", "AoeDebuff", "AoeDot", "AoeHeal",
            "Bleed", "Buff", "CastingCost", "Charge", "Charges", "CleaveDamage", "Cost", "Damage",
            "DamageReduction", "Debuff", "Dispel", "Dot", "Fear", "FlashHeal", "Freeze", "Heal", "Hot",
            "Immobilize", "Immune", "Interrupt", "Knockdown", "LightningDamage", "Lockout", "ManaCost",
            "Mark", "Poison", "Proc", "ProjectileDamage", "Pull", "RemoveStagger", "ResourceCost", "Root",
            "SelfBuff", "SelfDamage", "SelfHeal", "SelfSlow", "SelfStun", "Silence", "Slow", "SpiritCost",
            "SpiritPointCost", "Stun", "Taunt", "TickDamage", "Vanish", "Visual",
        ],
        StringComparer.Ordinal);

    private readonly Dictionary<string, int> _abilities = new(StringComparer.Ordinal);

    public EffectRoles(IReadOnlyList<Entity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Ability, NetId: { } id, Name: { } tag }) continue;
            if (!tag.StartsWith(AbilityPrefix, StringComparison.Ordinal)) continue;

            var core = tag[AbilityPrefix.Length..];

            if (!_abilities.TryGetValue(core, out var held) || id < held) _abilities[core] = id;
        }

        var parts = 0;
        var roles = 0;

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Effect, Name: { } tag }) continue;
            if (Stem(tag) is not { } stem) continue;

            parts++;
            if (Vocabulary.Contains(stem.Tail)) roles++;
        }

        Parts = parts;
        Roles = roles;
    }

    public int Abilities => _abilities.Count;

    public int Parts { get; }

    public int Roles { get; }

    public int? PartOf(string? tag) => Stem(tag)?.Ability;

    public string? Role(string? tag) =>
        Stem(tag) is { } stem && Vocabulary.Contains(stem.Tail) ? stem.Tail : null;

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
