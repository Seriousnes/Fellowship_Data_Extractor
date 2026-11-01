using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>Effects each ability applies, its parts and the abilities it grants, keyed by the ability's reference.</summary>
public sealed class AppliedLinks
{
    public static readonly AppliedLinks None = new();

    private readonly Dictionary<(EntityType Type, int Id), List<EntityReference>> _applied = [];
    private readonly Dictionary<(EntityType Type, int Id), List<EntityReference>> _parts = [];
    private readonly Dictionary<(EntityType Type, int Id), List<EntityReference>> _granted = [];

    public IReadOnlyList<EntityReference>? Applied(EntityReference? ability) => Of(_applied, ability);

    public IReadOnlyList<EntityReference>? Parts(EntityReference? ability) => Of(_parts, ability);

    public IReadOnlyList<EntityReference>? Granted(EntityReference? ability) => Of(_granted, ability);

    public void Apply(EntityReference ability, EntityReference effect) => Add(_applied, ability, effect);

    public void Part(EntityReference ability, EntityReference part) => Add(_parts, ability, part);

    /// <summary>Each ability grants the abilities its applied effects grant.</summary>
    public void GrantThroughEffects(IReadOnlyDictionary<(EntityType Type, int Id), IReadOnlyList<EntityReference>> grantedBy)
    {
        foreach (var (ability, effects) in _applied)
        {
            foreach (var effect in effects)
            {
                foreach (var granted in grantedBy.GetValueOrDefault((effect.Type, effect.Id)) ?? [])
                {
                    if (granted.Type == ability.Type && granted.Id == ability.Id) continue;

                    Add(_granted, new EntityReference(ability.Type, ability.Id, null), granted);
                }
            }
        }
    }

    private static IReadOnlyList<EntityReference>? Of(
        Dictionary<(EntityType Type, int Id), List<EntityReference>> index, EntityReference? ability) =>
        ability is not null && index.TryGetValue((ability.Type, ability.Id), out var held) ? held : null;

    private static void Add(
        Dictionary<(EntityType Type, int Id), List<EntityReference>> index, EntityReference ability, EntityReference linked)
    {
        if (linked.Type == ability.Type && linked.Id == ability.Id) return;
        if (!index.TryGetValue((ability.Type, ability.Id), out var held)) index[(ability.Type, ability.Id)] = held = [];
        if (!held.Any(entry => entry.Type == linked.Type && entry.Id == linked.Id)) held.Add(linked);
    }
}
