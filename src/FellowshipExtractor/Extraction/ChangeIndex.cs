using System.Text.Json;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class ChangeIndex
{
    public static readonly ChangeIndex None = new();

    public const string Set = "Set";
    public const string Add = "Add";
    public const string Subtract = "Subtract";
    public const string Multiply = "Multiply";

    private static string Field(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);

    private static readonly string CooldownField = Field(nameof(AbilityDocument.Cooldown));

    private static readonly IReadOnlyDictionary<ModifierType, (string Field, string Op)> Fields =
        new Dictionary<ModifierType, (string, string)>
        {
            [ModifierType.CastTime] = (Field(nameof(AbilityDocument.CastTime)), Set),
            [ModifierType.CastTimeIncrease] = (Field(nameof(AbilityDocument.CastTime)), Add),
            [ModifierType.CastTimeReduction] = (Field(nameof(AbilityDocument.CastTime)), Subtract),
            [ModifierType.CastTimeMultiplier] = (Field(nameof(AbilityDocument.CastTime)), Multiply),
            [ModifierType.ChannelTime] = (Field(nameof(AbilityDocument.ChannelTime)), Set),
            [ModifierType.ChannelTick] = (Field(nameof(AbilityDocument.ChannelTick)), Set),
            [ModifierType.Duration] = (Field(nameof(AbilityDocument.Duration)), Set),
            [ModifierType.DurationIncrease] = (Field(nameof(AbilityDocument.Duration)), Add),
            [ModifierType.DurationMultiplier] = (Field(nameof(AbilityDocument.Duration)), Multiply),
            [ModifierType.Period] = (Field(nameof(AbilityDocument.Period)), Set),
            [ModifierType.MaxStacks] = (Field(nameof(EffectDocument.MaxStacks)), Set),
            [ModifierType.Charges] = (Field(nameof(AbilityDocument.Charges)), Set),
            [ModifierType.Range] = (Field(nameof(AbilityDocument.Range)), Set),
            [ModifierType.MinRange] = (Field(nameof(AbilityDocument.MinRange)), Set),
            [ModifierType.Radius] = (Field(nameof(AbilityDocument.Radius)), Set),
            [ModifierType.Cost] = (Field(nameof(AbilityDocument.Cost)), Set),
            [ModifierType.CostIncrease] = (Field(nameof(AbilityDocument.Cost)), Add),
            [ModifierType.CostReduction] = (Field(nameof(AbilityDocument.Cost)), Subtract),
        };

    private readonly Dictionary<(EntityType Type, int Id), List<Change>> _changes = [];
    private readonly Dictionary<(EntityType Type, int Id), List<EntityReference>> _changedBy = [];
    private readonly Dictionary<string, int> _unprojected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _ungranted = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, SortedSet<string>> Ungranted => _ungranted;

    private ChangeIndex()
    {
    }

    public ChangeIndex(IReadOnlyList<Entity> entities, ModifierIndex modifiers, DocumentProjection.References references)
    {
        foreach (var entity in entities)
        {
            if (entity is not { Type: (EntityType.Ability or EntityType.Effect) and var type, NetId: { } id }) continue;
            if (references.Of(type, id) is not { } target) continue;

            foreach (var term in entity.CooldownTerms ?? [])
            {
                if (term.Tag is not { } tag)
                {
                    Count($"{term.Array} entry with no required tag");
                    continue;
                }

                Gated(modifiers, references, tag, new Change(target, CooldownField, term.Array, term.Value));
            }

            foreach (var (tag, gated) in entity.GatedModifiers ?? new Dictionary<string, IReadOnlyList<EffectModifier>>())
            {
                foreach (var modifier in gated)
                {
                    if (tag.StartsWith(EffectModifiers.TargetMark, StringComparison.Ordinal))
                    {
                        Count("effect modifier gated on a tag the target holds");
                        continue;
                    }

                    if (modifier is not { Value: { } value, Op: { } op })
                    {
                        Count("effect modifier with no stated value");
                        continue;
                    }

                    Gated(modifiers, references, tag, new Change(target, modifier.Attribute, op, value));
                }
            }
        }

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Talent or EntityType.LegendaryPower, NetId: { } id } source) continue;
            if (references.Of(source.Type!.Value, id) is not { } named) continue;

            foreach (var record in modifiers.Of(source, references) ?? [])
            {
                if (Projected(record) is not { } projected) continue;

                foreach (var target in record.Targets!)
                {
                    if (target.Type is not (EntityType.Ability or EntityType.Effect))
                    {
                        Count($"{record.Type} aimed at a {EntityTypes.Slug(target.Type)}");
                        continue;
                    }

                    Record(named, new Change(target, projected.Field, projected.Op, record.Value!.Value)
                    {
                        While = record.Condition?.Effects,
                    });
                }
            }
        }
    }

    public int Sources => _changes.Count;

    public int Changes => _changes.Values.Sum(changes => changes.Count);

    public int Targets => _changedBy.Count;

    public IReadOnlyDictionary<string, int> Unprojected() => _unprojected;

    public IReadOnlyList<Change>? Of(Entity entity) =>
        entity is { Type: { } type, NetId: { } id } && _changes.TryGetValue((type, id), out var changes) ? changes : null;

    public IReadOnlyList<EntityReference>? ChangedBy(Entity entity, DocumentProjection.References references) =>
        entity is { Type: { } type, NetId: { } id } && references.Of(type, id) is { } self &&
        _changedBy.TryGetValue((self.Type, self.Id), out var sources)
            ? sources
            : null;

    private void Gated(ModifierIndex modifiers, DocumentProjection.References references, string tag, Change change)
    {
        var holding = modifiers.Holding(tag);

        if (holding.Count == 0)
        {
            var reason = $"{change.Op} on {change.Field} gated on a tag no talent or legendary power grants";
            Count(reason);

            if (!_ungranted.TryGetValue(reason, out var tags)) _ungranted[reason] = tags = new(StringComparer.Ordinal);
            tags.Add(tag);
            return;
        }

        foreach (var (type, id) in holding)
        {
            if (references.Of(type, id) is { } source) Record(source, change);
        }
    }

    private void Record(EntityReference source, Change change)
    {
        var key = (source.Type, source.Id);

        if (!_changes.TryGetValue(key, out var changes)) _changes[key] = changes = [];
        if (changes.Any(held => Same(held, change))) return;

        changes.Add(change);

        var target = (change.Target.Type, change.Target.Id);

        if (!_changedBy.TryGetValue(target, out var sources)) _changedBy[target] = sources = [];
        if (!sources.Any(held => held.Type == source.Type && held.Id == source.Id)) sources.Add(source);
    }

    private (string Field, string Op)? Projected(SourceChange record)
    {
        var reason = record switch
        {
            { Type: null } => "untyped",
            { Targets: null or { Count: 0 } } => $"{record.Type} with no target",
            { Value: null } => $"{record.Type} without a value",
            { Basis: ModifierBasis.Keyword } => $"{record.Type} classified by keyword alone",
            { PerCount: not null } or { CountsCall: not null } => $"{record.Type} scaled by a count",
            { Per: not null } => $"{record.Type} per combat event",
            { Condition: { } condition } when !OnlyEffects(condition) =>
                $"{record.Type} under a condition other than held effects",
            _ when !Fields.ContainsKey(record.Type!.Value) => $"{record.Type} with no document field",
            _ => null,
        };

        if (reason is null) return Fields[record.Type!.Value];

        Count(reason);
        return null;
    }

    private static bool OnlyEffects(ModifierCondition condition) =>
        condition with { Effects = null } == new ModifierCondition();

    private void Count(string reason) => _unprojected[reason] = _unprojected.GetValueOrDefault(reason) + 1;

    private static bool Same(Change first, Change second) =>
        first.Target.Type == second.Target.Type && first.Target.Id == second.Target.Id &&
        first.Field == second.Field && first.Op == second.Op && first.Value == second.Value &&
        (first.While ?? []).Select(held => (held.Type, held.Id))
            .SequenceEqual((second.While ?? []).Select(held => (held.Type, held.Id)));
}
