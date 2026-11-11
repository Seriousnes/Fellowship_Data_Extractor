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
    public const string Additive = "Additive";

    /// <summary>The game's name for a TimeRate mod on a cooldown tag.</summary>
    public const string CooldownAccelerationField = "cooldownAcceleration";

    private static string Field(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);

    private static readonly string CooldownField = Field(nameof(AbilityDocument.Cooldown));

    private static readonly string DurationField = Field(nameof(AbilityDocument.Duration));

    private static readonly string ChannelTimeField = Field(nameof(AbilityDocument.ChannelTime));

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
    private readonly Dictionary<(EntityType Type, int Id), List<Entity>> _targets = [];
    private readonly ModifierIndex _modifiers = ModifierIndex.None;
    public IReadOnlyDictionary<string, SortedSet<string>> Ungranted => _ungranted;

    private ChangeIndex()
    {
    }

    public ChangeIndex(IReadOnlyList<Entity> entities, ModifierIndex modifiers, DocumentProjection.References references)
    {
        _modifiers = modifiers;

        foreach (var entity in entities)
        {
            if (entity is not { Type: (EntityType.Ability or EntityType.Effect) and var type, NetId: { } id }) continue;
            if (references.Of(type, id) is not { } target) continue;

            if (!_targets.TryGetValue((target.Type, target.Id), out var read)) _targets[(target.Type, target.Id)] = read = [];
            read.Add(entity);

            foreach (var term in entity.CooldownTerms ?? [])
            {
                if (term.Tag is not { } tag)
                {
                    Count($"{term.Array} entry with no required tag");
                    continue;
                }

                Gated(modifiers, references, tag, new Change(target, CooldownField, term.Array, term.Value));
            }

            var onTarget = (entity.GatedModifiers ?? new Dictionary<string, IReadOnlyList<EffectModifier>>())
                .Where(entry => entry.Key.StartsWith(EffectModifiers.TargetMark, StringComparison.Ordinal))
                .SelectMany(entry => entry.Value)
                .ToHashSet();

            foreach (var (tag, gated) in entity.GatedModifiers ?? new Dictionary<string, IReadOnlyList<EffectModifier>>())
            {
                foreach (var modifier in gated)
                {
                    if (tag.StartsWith(EffectModifiers.TargetMark, StringComparison.Ordinal) || onTarget.Contains(modifier))
                    {
                        Count("effect modifier gated on a tag the target holds");
                        continue;
                    }

                    if (modifier is not { Value: { } value, Op: { } op })
                    {
                        Count("effect modifier with no stated value");
                        continue;
                    }

                    Gated(modifiers, references, tag, new Change(target, modifier.Attribute, op, value), modifier.Row);
                }
            }
        }

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Talent or EntityType.LegendaryPower, NetId: { } id } source) continue;
            if (references.Of(source.Type!.Value, id) is not { } named) continue;

            foreach (var record in modifiers.Of(source, references) ?? [])
            {
                if (!Projectable(record)) continue;

                foreach (var target in record.Targets!)
                {
                    if (target.Type is not (EntityType.Ability or EntityType.Effect))
                    {
                        Count($"{record.Type} aimed at a {EntityTypes.Slug(target.Type)}");
                        continue;
                    }

                    var projected = Projected(record, target);

                    if (projected.Count == 0)
                    {
                        Count($"{record.Type} with no document field");
                        continue;
                    }

                    foreach (var (field, op) in projected)
                    {
                        Record(named, new Change(target, field, op, record.Value!.Value)
                        {
                            While = record.Condition?.Effects,
                            WhileTarget = record.Condition?.TargetEffects,
                            Percent = record.Row is { } row && ModifierOps.Adds(op) &&
                                      modifiers.Percent(row, source.Type!.Value, id)
                                ? true
                                : null,
                        });
                    }
                }
            }
        }

        Accelerations(entities, references);
    }

    /// <summary>
    /// Records each effect's TimeRate mods as cooldown acceleration on every ability cooling down under the mod's tag.
    /// The change is the effect's own, and also each talent's granting the ability applying the effect, held while
    /// the effect is.
    /// </summary>
    private void Accelerations(IReadOnlyList<Entity> entities, DocumentProjection.References references)
    {
        var cooling = new Dictionary<string, List<EntityReference>>(StringComparer.Ordinal);
        var granters = new Dictionary<(EntityType Type, int Id), List<EntityReference>>();

        foreach (var entity in entities)
        {
            if (entity is { Type: EntityType.Ability, NetId: { } ability } && references.Of(EntityType.Ability, ability) is { } cooled)
            {
                foreach (var tag in entity.CooldownTags ?? [])
                {
                    if (!cooling.TryGetValue(tag, out var under)) cooling[tag] = under = [];
                    if (!under.Any(held => held.Id == cooled.Id)) under.Add(cooled);
                }
            }

            if (entity is not { Type: EntityType.Talent or EntityType.LegendaryPower, NetId: { } id } source) continue;
            if (references.Of(source.Type!.Value, id) is not { } named) continue;

            foreach (var granted in entity.Granted ?? [])
            {
                if (references.At(granted) is not { Type: EntityType.Ability } reached) continue;

                if (!granters.TryGetValue((reached.Type, reached.Id), out var by)) granters[(reached.Type, reached.Id)] = by = [];
                if (!by.Contains(named)) by.Add(named);
            }
        }

        foreach (var entity in entities)
        {
            if (entity is not { Type: EntityType.Effect, NetId: { } id, TimeRates: { } rates }) continue;
            if (references.Of(EntityType.Effect, id) is not { } effect) continue;

            List<EntityReference> talents =
            [
                .. references.At(entity.AppliedBy) is { } applier
                    ? granters.GetValueOrDefault((applier.Type, applier.Id)) ?? []
                    : [],
            ];

            foreach (var (type, gating) in _modifiers.GatingSources(entity))
            {
                if (references.Of(type, gating) is { } talent && !talents.Contains(talent)) talents.Add(talent);
            }

            foreach (var rate in rates)
            {
                if (!cooling.TryGetValue(rate.Tag, out var targets))
                {
                    Count("time rate on a tag no ability cools down under");
                    continue;
                }

                foreach (var target in targets)
                {
                    var change = new Change(target, CooldownAccelerationField, Additive, rate.Value) { Percent = true };

                    Record(effect, change);

                    foreach (var talent in talents) Record(talent, change with { While = [effect] });
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

    public IReadOnlyDictionary<string, double>? Constants(Entity source) => _modifiers.Rows(source);

    /// <summary>The constants row a tuning key names for <paramref name="context"/>.</summary>
    public string Row(string key, Entity context) => _modifiers.Row(key, context);

    public IReadOnlyList<(EntityType Type, int Id)> Holding(string tag) => _modifiers.Holding(tag);

    public IReadOnlyList<int> AbilitiesTagged(string tag) => _modifiers.AbilitiesTagged(tag);

    public IReadOnlyList<(Entity Ability, CommitTriggerSource Trigger)> CommitsOf(Entity effect) => _modifiers.CommitsOf(effect);

    public IReadOnlyList<ProcRoll> GatedRolls(Entity source) => _modifiers.GatedRolls(source);

    public bool Percent(string row, EntityType type, int id) => _modifiers.Percent(row, type, id);

    public EntityReference? PartOf(Entity ability, DocumentProjection.References references) =>
        _modifiers.PartOf(ability, references);

    public IReadOnlyList<EntityReference>? Named(
        Entity entity, IEnumerable<string>? names, EntityType type, DocumentProjection.References references) =>
        _modifiers.Named(entity, names, type, references);

    public EntityReference? TalentNamed(Entity context, string name, DocumentProjection.References references) =>
        _modifiers.TalentNamed(context, name, references);

    public TuningReference? Reference(string row, Entity context, DocumentProjection.References references) =>
        _modifiers.Resolved(row, context, references);

    /// <summary>The row's reference whether or not a namespace names it, for a row the entity's own asset binds.</summary>
    public TuningReference Referenced(string row, Entity context, DocumentProjection.References references) =>
        _modifiers.Reference(row, context, references);

    public IReadOnlyList<EntityReference>? ChangedBy(Entity entity, DocumentProjection.References references) =>
        entity is { Type: { } type, NetId: { } id } && references.Of(type, id) is { } self &&
        _changedBy.TryGetValue((self.Type, self.Id), out var sources)
            ? sources
            : null;

    private void Gated(
        ModifierIndex modifiers, DocumentProjection.References references, string tag, Change change, string? row = null)
    {
        var holding = modifiers.Holding(tag);

        foreach (var (type, id) in modifiers.Granting(tag))
        {
            if (type == change.Target.Type && id == change.Target.Id) continue;
            if (references.Of(type, id) is not { } carrier) continue;

            Record(carrier, row is not null && ModifierOps.Adds(change.Op) && modifiers.Percent(row, type, id)
                ? change with { Percent = true }
                : change);
        }

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
            if (references.Of(type, id) is not { } source) continue;

            Record(source, row is not null && ModifierOps.Adds(change.Op) && modifiers.Percent(row, type, id)
                ? change with { Percent = true }
                : change);
        }
    }

    private void Record(EntityReference source, Change change)
    {
        if (ModifierOps.Unchanged(change.Op, change.Value))
        {
            Count($"{change.Op} leaving {change.Field} unchanged");
            return;
        }

        change = change with { Result = Resulting(change) };

        var key = (source.Type, source.Id);

        if (!_changes.TryGetValue(key, out var changes)) _changes[key] = changes = [];
        if (changes.Any(held => Same(held, change))) return;

        changes.Add(change);

        var target = (change.Target.Type, change.Target.Id);

        if (!_changedBy.TryGetValue(target, out var sources)) _changedBy[target] = sources = [];
        if (!sources.Any(held => held.Type == source.Type && held.Id == source.Id)) sources.Add(source);
    }

    /// <summary>
    /// The target's value of the changed field once a factor or term applies, where every entity read for the target
    /// states the same value for the field.
    /// </summary>
    private double? Resulting(Change change)
    {
        if (!ModifierOps.Multiplies(change.Op) && !ModifierOps.Adds(change.Op)) return null;

        var stated = (_targets.GetValueOrDefault((change.Target.Type, change.Target.Id)) ?? [])
            .Select(entity => Stated(entity, change.Field))
            .OfType<double>()
            .Distinct()
            .ToArray();

        if (stated is not [var value]) return null;

        var result = ModifierOps.Multiplies(change.Op)
            ? value * ModifierOps.Factor(change.Op, change.Value)
            : value + ModifierOps.Term(change.Op, change.Value);

        return Math.Round(result, 3);
    }

    private static double? Stated(Entity entity, string field) => field switch
    {
        _ when field == CooldownField => entity.Cooldown,
        _ when field == DurationField => entity.Duration,
        _ when field == ChannelTimeField => entity.ChannelTime,
        _ when field == Field(nameof(AbilityDocument.CastTime)) => entity.CastTime,
        _ when field == Field(nameof(AbilityDocument.ChannelTick)) => entity.ChannelTick,
        _ when field == Field(nameof(AbilityDocument.Period)) => entity.Period,
        _ when field == Field(nameof(AbilityDocument.Range)) => entity.Range,
        _ when field == Field(nameof(AbilityDocument.MinRange)) => entity.MinRange,
        _ when field == Field(nameof(AbilityDocument.Radius)) => entity.Radius,
        _ when field == Field(nameof(AbilityDocument.Cost)) => entity.Cost,
        _ when field == Field(nameof(AbilityDocument.Charges)) => entity.Charges,
        _ when field == Field(nameof(EffectDocument.MaxStacks)) => entity.MaxStacks,
        _ => null,
    };

    private bool Projectable(SourceChange record)
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
            _ => null,
        };

        if (reason is null) return true;

        Count(reason);
        return false;
    }

    private IReadOnlyList<(string Field, string Op)> Projected(SourceChange record, EntityReference target)
    {
        var read = _targets.GetValueOrDefault((target.Type, target.Id)) ?? [];
        var uses = record.Row is { } row
            ? [.. read.SelectMany(entity => entity.RowUses?.GetValueOrDefault(row) ?? [])]
            : Array.Empty<string>();

        if (record.Type is ModifierType.DamageMultiplier or ModifierType.HealingMultiplier)
        {
            return [.. uses.Select(Attribute).OfType<(string, string)>().Distinct()];
        }

        if (!Fields.TryGetValue(record.Type!.Value, out var projected)) return [];

        if (projected.Field == DurationField && read.Any(entity => SetsChannel(entity, uses)))
        {
            return [(ChannelTimeField, projected.Op)];
        }

        return [projected];
    }

    private static (string Field, string Op)? Attribute(string use)
    {
        if (!use.StartsWith(RowSinks.AttributeUse, StringComparison.Ordinal)) return null;

        var stated = use[RowSinks.AttributeUse.Length..];
        var gate = stated.IndexOf(RowSinks.GateMark, StringComparison.Ordinal);

        return (gate < 0 ? stated : stated[..gate]).Split('|') is [var attribute, var op, ..] ? (attribute, op) : null;
    }

    private static bool SetsChannel(Entity target, IReadOnlyList<string> uses)
    {
        if (target is not { ChannelTime: { } channel, Rows: { } rows, RowUses: { } rowUses }) return false;

        var consumers = uses
            .Where(use => use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal))
            .Select(use => use[RowSinks.SinkUse.Length..].Split('|', '~')[0])
            .ToHashSet(StringComparer.Ordinal);

        return rows.Any(entry => entry.Value == channel &&
            rowUses.GetValueOrDefault(entry.Key)?.Any(use =>
                use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal) &&
                consumers.Contains(use[RowSinks.SinkUse.Length..])) == true);
    }

    private static bool OnlyEffects(ModifierCondition condition) =>
        condition with { Effects = null, TargetEffects = null } == new ModifierCondition();

    private void Count(string reason) => _unprojected[reason] = _unprojected.GetValueOrDefault(reason) + 1;

    private static bool Same(Change first, Change second) =>
        first.Target.Type == second.Target.Type && first.Target.Id == second.Target.Id &&
        first.Field == second.Field && first.Op == second.Op && first.Value == second.Value &&
        (first.While ?? []).Select(held => (held.Type, held.Id))
            .SequenceEqual((second.While ?? []).Select(held => (held.Type, held.Id))) &&
        (first.WhileTarget ?? []).Select(held => (held.Type, held.Id))
            .SequenceEqual((second.WhileTarget ?? []).Select(held => (held.Type, held.Id)));
}
