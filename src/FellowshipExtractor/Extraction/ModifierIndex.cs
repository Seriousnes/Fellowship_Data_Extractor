using System.Globalization;
using System.Text.RegularExpressions;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed partial class ModifierIndex
{
    public static readonly ModifierIndex None = new([]);

    private static readonly string[] TalentSegments = [".Talent.", ".Talents."];
    private const string TalentTagPrefix = "Talents.";
    private const string DurationField = "duration";
    private const string IncomingDamage = "IncomingDamageMultiplier";
    private const double Endless = 10000;

    private readonly record struct Key(EntityType Type, int Id);

    private sealed record Requirement(
        IReadOnlyList<Key> Effects,
        double? Stacks,
        IReadOnlyList<Key> TargetEffects,
        double? Chance,
        IReadOnlyList<Key> Talents,
        IReadOnlyList<Key> CastBy,
        IReadOnlyList<string> States)
    {
        public string? ChanceRow { get; init; }

        public IReadOnlyList<Key> Cooldowns { get; init; } = [];

        public IReadOnlyList<Key> OwnEffects { get; init; } = [];

        public IReadOnlyList<Key> StateSources { get; init; } = [];

        public IReadOnlyList<string> TargetStates { get; init; } = [];

        public IReadOnlyDictionary<string, double> TargetHealth { get; init; } = new Dictionary<string, double>();

        public IReadOnlyDictionary<string, double> Health { get; init; } = new Dictionary<string, double>();

        public bool IsEmpty =>
            Effects.Count == 0 && Stacks is null && TargetEffects.Count == 0 && Chance is null &&
            Talents.Count == 0 && CastBy.Count == 0 && States.Count == 0 && Cooldowns.Count == 0 &&
            TargetStates.Count == 0;

        public Requirement Merged(Requirement other) =>
            new([.. Effects.Union(other.Effects)], Stacks ?? other.Stacks, [.. TargetEffects.Union(other.TargetEffects)],
                Chance ?? other.Chance, [.. Talents.Union(other.Talents)], [.. CastBy.Union(other.CastBy)],
                [.. States.Union(other.States, StringComparer.Ordinal)])
            {
                ChanceRow = ChanceRow ?? other.ChanceRow,
                Cooldowns = [.. Cooldowns.Union(other.Cooldowns)],
                OwnEffects = [.. OwnEffects.Union(other.OwnEffects)],
                StateSources = [.. StateSources.Union(other.StateSources)],
                TargetStates = [.. TargetStates.Union(other.TargetStates, StringComparer.Ordinal)],
                Health = Health.Concat(other.Health)
                    .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal),
                TargetHealth = TargetHealth.Concat(other.TargetHealth)
                    .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal),
            };
    }

    private sealed record Typed(
        ModifierCandidate Candidate, ModifierClass Class, IReadOnlyList<Key> Targets, Requirement? Condition)
    {
        public double? PerCount { get; init; }

        public double? Max { get; init; }

        public IReadOnlyList<Key> Counts { get; init; } = [];

        public int? Uncounted { get; init; }

        public bool CountsStacks { get; init; }

        public string? CountsCall { get; init; }
    }

    private static readonly HashSet<ModifierType> Coefficients =
    [
        ModifierType.DamageMultiplier, ModifierType.HealingMultiplier, ModifierType.Coefficient,
        ModifierType.CritChanceIncrease, ModifierType.CritChance, ModifierType.CritDamageMultiplier,
    ];

    private readonly Func<Entity, int?> _partOf;
    private readonly IReadOnlyDictionary<string, double> _healthFractions;
    private readonly Dictionary<string, Key> _owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Key> _assets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Key, Entity> _entities = [];
    private readonly Dictionary<string, HashSet<Key>> _granters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<Key>> _adders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<Key>> _assetTagged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<Key>> _abilityTagged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<Key>> _cooldownTagged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<Key>> _stateHolders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<Key>> _applied = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(Key Effect, string Attribute)> _filled = [];
    private readonly Dictionary<string, SortedSet<string>> _procGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<string>> _procAssetTags = new(StringComparer.Ordinal);
    private readonly HashSet<(Key Source, double Stacks)> _stackedRows = [];
    private Dictionary<int, SortedSet<Key>>? _parts;
    private HashSet<string>? _names;

    private HashSet<string> _sourceNames => _names ??= [.. _sources.Values.Select(source => source.DisplayName).OfType<string>()];
    private readonly Dictionary<string, SortedSet<Key>> _spawners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<Key>> _abilityGranters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Key, SortedDictionary<string, double>> _sourceRows = [];
    private readonly HashSet<(Key Source, string Row)> _bound = [];
    private readonly Dictionary<string, SortedSet<string>> _rowTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<string>> _modifierTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<Key>> _modifierTesters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Key, List<(Key Tester, EffectModifier Modifier, string Tag)>> _sourceModifiers = [];
    private readonly Dictionary<string, EffectModifier> _byRow = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModifierCandidate, EffectModifier> _byCandidate = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, SortedSet<string>> _uses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<Key>> _binders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<Key>> _holders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(Key Source, Key Owner), SortedSet<Key>> _coTargets = [];
    private readonly Dictionary<Key, IReadOnlyList<Typed>> _records = [];
    private readonly Dictionary<Key, SortedSet<Key>> _modifiedBy = [];
    private readonly Dictionary<Key, Entity> _sources = [];
    private readonly Dictionary<Key, HashSet<string>> _held = [];
    private readonly Dictionary<string, SortedSet<Key>> _tagsBySource = new(StringComparer.Ordinal);
    private readonly Dictionary<Key, SortedSet<Key>> _replacements = [];
    private readonly Dictionary<Key, Key> _replaces = [];
    private readonly Dictionary<Key, SortedSet<Key>> _grantedBy = [];
    private readonly Dictionary<Key, SortedSet<Key>> _grants = [];
    private readonly HashSet<(Key Effect, string Row)> _scopedAway = [];
    private readonly Dictionary<Key, Key> _parents = [];
    private readonly Dictionary<Key, List<(string Attribute, double? Value, double? Coefficient, string Row, bool Scaled)>> _gains = [];
    private readonly Dictionary<(string Table, int NetId), Key> _byTable = [];

    public ModifierIndex(
        IEnumerable<Entity> entities, Func<Entity, int?>? partOf = null, IReadOnlyDictionary<string, double>? healthFractions = null)
    {
        _partOf = partOf ?? (_ => null);
        _healthFractions = healthFractions ?? new Dictionary<string, double>();

        var all = entities.Where(entity => entity is { Type: not null, NetId: not null }).ToArray();

        foreach (var entity in all)
        {
            _entities.TryAdd(KeyOf(entity), entity);
            if (entity.Table is { } table) _byTable.TryAdd((table, entity.NetId!.Value), KeyOf(entity));
        }

        var senders = new Dictionary<string, SortedSet<Key>>(StringComparer.Ordinal);

        foreach (var entity in all.Where(entity => entity is { Type: EntityType.Ability, Uncooked: not true }))
        {
            foreach (var tag in entity.SentEvents ?? []) Index(senders, tag, KeyOf(entity));
        }

        foreach (var entity in all.Where(entity => entity is { Type: EntityType.Ability, TriggerTags: not null }))
        {
            var triggering = entity.TriggerTags!
                .SelectMany(tag => senders.GetValueOrDefault(tag) ?? [])
                .Where(sender => sender != KeyOf(entity))
                .Distinct()
                .ToArray();

            if (triggering is [var only]) _parents[KeyOf(entity)] = only;
        }

        foreach (var entity in all
                     .Where(entity => entity.Type is EntityType.Ability or EntityType.Effect && entity.Tuning is not null &&
                                      entity.Uncooked is not true)
                     .OrderBy(entity => entity.Type == EntityType.Ability ? 0 : 1)
                     .ThenBy(entity => entity.DisplayName is null ? 1 : 0)
                     .ThenBy(entity => entity.NetId))
        {
            _owners.TryAdd(entity.Tuning!, KeyOf(entity));
        }

        foreach (var entity in all.Where(IsSpell))
        {
            if (entity is { Name: { } asset, Uncooked: not true }) _assets.TryAdd(asset, KeyOf(entity));

            foreach (var row in entity.OwnRows ?? [])
            {
                if (!_binders.TryGetValue(row, out var binding)) _binders[row] = binding = Keys();
                binding.Add(KeyOf(entity));
            }

            if (entity is { Type: EntityType.Ability, DisplayName: not null, NameInheritedFrom: null })
            {
                foreach (var tag in entity.AbilityTags ?? []) Index(_abilityTagged, tag, KeyOf(entity));
            }

            if (entity.Type == EntityType.Ability)
            {
                foreach (var tag in entity.CooldownTags ?? []) Index(_cooldownTagged, tag, KeyOf(entity));
                foreach (var tag in (entity.AddedTags ?? []).Concat(entity.HeldTags ?? []).Concat(entity.LooseTags ?? [])) Index(_stateHolders, tag, KeyOf(entity));
                foreach (var actor in entity.SpawnedActors ?? []) Index(_spawners, actor, KeyOf(entity));

                foreach (var (assetTag, tags) in entity.ProcAssetTags ?? new Dictionary<string, IReadOnlyList<string>>())
                {
                    if (!_procAssetTags.TryGetValue(assetTag, out var marked)) _procAssetTags[assetTag] = marked = new SortedSet<string>(StringComparer.Ordinal);
                    marked.UnionWith(tags);
                }

                foreach (var (effectAsset, tags) in entity.ProcEffects ?? new Dictionary<string, IReadOnlyList<string>>())
                {
                    if (!_procGates.TryGetValue(effectAsset, out var held)) _procGates[effectAsset] = held = new SortedSet<string>(StringComparer.Ordinal);
                    held.UnionWith(tags);
                }
            }

            if (entity.Type == EntityType.Effect)
            {
                foreach (var granted in entity.GrantedAbilityNames ?? []) Index(_abilityGranters, granted, KeyOf(entity));
            }

            if (entity.Type != EntityType.Effect) continue;

            foreach (var tag in (entity.AddedTags ?? []).Concat(entity.HeldTags ?? [])) Index(_adders, tag, KeyOf(entity));
            foreach (var tag in entity.EffectAssetTags ?? []) Index(_assetTagged, tag, KeyOf(entity));

            foreach (var row in entity.OwnRows ?? [])
            {
                if (entity.RowUses?.GetValueOrDefault(row) is not { } uses || !uses.Any(HoldsOn)) continue;
                if (!_holders.TryGetValue(row, out var holding)) _holders[row] = holding = Keys();
                holding.Add(KeyOf(entity));
            }
        }

        foreach (var entity in all.Where(entity => !IsSpell(entity)))
        {
            foreach (var tag in entity.GrantedTags ?? [])
            {
                if (!_granters.TryGetValue(tag, out var granting)) _granters[tag] = granting = [];
                granting.Add(KeyOf(entity));
            }
        }

        foreach (var entity in all)
        {
            foreach (var (row, uses) in entity.RowUses ?? new Dictionary<string, IReadOnlyList<string>>())
            {
                if (!_uses.TryGetValue(row, out var held)) _uses[row] = held = new(StringComparer.Ordinal);
                held.UnionWith(uses);
            }
        }

        var sources = all.Where(IsSource).ToArray();

        foreach (var source in sources)
        {
            var key = KeyOf(source);
            _sources.TryAdd(key, source);

            if (!_sourceRows.TryGetValue(key, out var rows)) _sourceRows[key] = rows = new(StringComparer.Ordinal);

            foreach (var (row, value) in source.TagRows ?? new Dictionary<string, double>()) rows.TryAdd(row, value);

            var held = new HashSet<string>(source.GrantedTags ?? [], StringComparer.Ordinal);

            foreach (var effect in GrantedEffects(source))
            {
                if (_entities.GetValueOrDefault(effect) is not { OngoingTags: null } carrier) continue;

                held.UnionWith(carrier.HeldTags ?? []);
                held.UnionWith(carrier.AddedTags ?? []);
            }

            _held[key] = held;

            foreach (var tag in held.Concat(source.AddedTags ?? [])) Index(_tagsBySource, tag, key);
        }

        var adds = all
            .SelectMany(entity => entity.GatedAdds ?? new Dictionary<string, IReadOnlyList<string>>())
            .ToArray();

        for (var changed = true; changed;)
        {
            changed = false;

            foreach (var (tag, added) in adds)
            {
                if (!_tagsBySource.TryGetValue(tag, out var granters)) continue;

                foreach (var next in added)
                {
                    if (!_tagsBySource.TryGetValue(next, out var inherited)) _tagsBySource[next] = inherited = Keys();

                    foreach (var source in granters.ToArray()) changed |= inherited.Add(source);
                }
            }
        }

        foreach (var entity in all.Where(IsSpell))
        {
            var tester = KeyOf(entity);

            foreach (var (tag, rows) in entity.GatedConstants ?? new Dictionary<string, IReadOnlyDictionary<string, double>>())
            {
                foreach (var row in rows.Keys) Gated(_rowTags, row, tag);

                foreach (var source in _tagsBySource.GetValueOrDefault(tag) ?? [])
                {
                    foreach (var (row, value) in rows)
                    {
                        _sourceRows[source].TryAdd(row, value);
                    }
                }
            }

            foreach (var (tag, modifiers) in entity.GatedModifiers ?? new Dictionary<string, IReadOnlyList<EffectModifier>>())
            {
                foreach (var modifier in modifiers)
                {
                    if (modifier.Row is { } row)
                    {
                        _byRow.TryAdd(row, modifier);
                        Gated(_rowTags, row, tag);
                        Gated(_modifierTags, row, tag);
                        Index(_modifierTesters, row, tester);
                    }

                    foreach (var source in _tagsBySource.GetValueOrDefault(tag) ?? [])
                    {
                        if (!_sourceModifiers.TryGetValue(source, out var held)) _sourceModifiers[source] = held = [];
                        held.Add((tester, modifier, tag));
                    }
                }
            }

            foreach (var modifier in entity.Modifiers ?? [])
            {
                if (modifier.Row is { } row) _byRow.TryAdd(row, modifier);
            }

            foreach (var read in entity.ForeignReads ?? [])
            {
                if (!_assets.TryGetValue(read.Owner, out var owner) || owner == tester) continue;

                foreach (var source in _tagsBySource.GetValueOrDefault(read.Tag) ?? [])
                {
                    if (!_coTargets.TryGetValue((source, owner), out var joined)) _coTargets[(source, owner)] = joined = Keys();
                    joined.Add(tester);
                }
            }
        }

        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in all)
        {
            foreach (var (row, value) in (entity.Rows ?? new Dictionary<string, double>())
                         .Concat(entity.NamespaceRows ?? new Dictionary<string, double>()))
            {
                values.TryAdd(row, value);
            }
        }

        foreach (var (row, uses) in _uses)
        {
            if (!values.TryGetValue(row, out var value)) continue;

            foreach (var gate in uses.Where(Reads).SelectMany(Gates))
            {
                foreach (var source in _tagsBySource.GetValueOrDefault(gate) ?? [])
                {
                    _sourceRows[source].TryAdd(row, value);
                }
            }
        }

        foreach (var source in sources)
        {
            var key = KeyOf(source);
            var rows = _sourceRows[key];

            foreach (var (row, value) in source.Rows ?? new Dictionary<string, double>())
            {
                if (!TalentSegments.Any(segment => row.Contains(segment, StringComparison.OrdinalIgnoreCase))) continue;

                rows.TryAdd(row, value);
                _bound.Add((key, row));
            }
        }

        foreach (var entity in all)
        {
            foreach (var pair in entity.Replacements ?? [])
            {
                var parts = pair.Split(AbilityReplacements.Separator);
                if (!_assets.TryGetValue(parts[0], out var original) || !_assets.TryGetValue(parts[1], out var replacement)) continue;

                if (!_replacements.TryGetValue(original, out var replacing)) _replacements[original] = replacing = Keys();

                replacing.Add(replacement);
                _replaces.TryAdd(replacement, original);

                if (!IsSource(entity)) continue;

                Grant(KeyOf(entity), replacement);
            }
        }

        var applied = _applied;

        foreach (var effect in all.Where(entity => entity is { Type: EntityType.Effect, AppliedBy.Name: not null }))
        {
            Index(applied, effect.AppliedBy!.Name, KeyOf(effect));
        }

        foreach (var source in sources)
        {
            var key = KeyOf(source);

            foreach (var effect in GrantedEffects(source)) Grant(key, effect);

            foreach (var ability in source.Granted ?? [])
            {
                foreach (var effect in applied.GetValueOrDefault(ability.Name) ?? []) Grant(key, effect);
            }
        }

        foreach (var entity in all)
        {
            if (entity is { Type: EntityType.Item, GrantsId: { } augmentation } &&
                _byTable.TryGetValue((TalentTrees.AugmentationTable, augmentation), out var power))
            {
                Grant(KeyOf(entity), power);
            }

            if (entity.Type != EntityType.GemAbility) continue;

            foreach (var granted in entity.Granted ?? [])
            {
                if (_byTable.TryGetValue((granted.Table, granted.NetId), out var target)) Grant(KeyOf(entity), target);
            }
        }

        var computed = Computed(all);

        foreach (var source in _sourceRows.Keys.Union(_sourceModifiers.Keys).Union(computed.Keys))
        {
            var typed = Source(source, computed.GetValueOrDefault(source) ?? []);
            _records[source] = typed;

        }

        foreach (var (source, typed) in Bypasses(all)) _records[source] = [.. _records.GetValueOrDefault(source) ?? [], .. typed];
        foreach (var (source, typed) in Stacked(all)) _records[source] = [.. _records.GetValueOrDefault(source) ?? [], .. typed];

        foreach (var (source, stacks) in _stackedRows)
        {
            _records[source] =
            [
                .. (_records.GetValueOrDefault(source) ?? []).Where(record =>
                    record.Class.Type == ModifierType.Stacks || record.Class.Value != stacks || record.Candidate.Uses.Count == 0 ||
                    !record.Candidate.Uses.All(use => use.StartsWith(RowSinks.VariableUse, StringComparison.Ordinal))),
            ];
        }
        foreach (var (source, typed) in _records)
        {
            foreach (var record in typed)
            {
                foreach (var target in record.Targets)
                {
                    if (!_modifiedBy.TryGetValue(target, out var modifying)) _modifiedBy[target] = modifying = Keys();
                    modifying.Add(source);
                }
            }
        }
    }

    public EntityReference? PartOf(Entity ability, DocumentProjection.References references) =>
        ability is { Type: EntityType.Ability, NetId: not null } && _parents.TryGetValue(KeyOf(ability), out var parent)
            ? references.Of(parent.Type, parent.Id)
            : null;

    public IReadOnlyList<ResourceGain>? Gains(Entity source) =>
        source is { Type: not null, NetId: not null } && _gains.TryGetValue(KeyOf(source), out var gains)
            ? [.. gains.Select(gain => new ResourceGain(gain.Attribute)
            {
                Amount = gain.Scaled ? null : gain.Value,
                ShareOfMaximum = gain.Coefficient,
                Source = gain.Row,
                SourceKind = gain.Scaled ? ResourceGains.RuntimeFormula : ResourceGains.ConstantsRow,
            })]
            : null;

    private Dictionary<Key, List<Typed>> Stacked(IEnumerable<Entity> all)
    {
        var extras = new Dictionary<Key, List<Typed>>();

        foreach (var ability in all.Where(entity => entity is { Type: EntityType.Ability, StackGrants: not null }))
        {
            foreach (var grant in ability.StackGrants!)
            {
                if (!_assets.TryGetValue(grant.Effect, out var effect)) continue;

                foreach (var gate in grant.Gates)
                {
                    foreach (var source in _tagsBySource.GetValueOrDefault(gate) ?? [])
                    {
                        if (Heroic(_sources.GetValueOrDefault(source), [effect]) is not [var target]) continue;

                        var candidate = new ModifierCandidate { Row = string.Empty, Value = grant.Stacks };
                        var classified = new ModifierClass(ModifierType.Stacks, ModifierBasis.Property, gate) { Value = grant.Stacks };

                        if (!extras.TryGetValue(source, out var held)) extras[source] = held = [];
                        if (held.Any(record => record.Targets.Contains(target) && record.Class.Value == grant.Stacks)) continue;

                        held.Add(new Typed(candidate, classified, [target], null));
                        _stackedRows.Add((source, grant.Stacks));
                    }
                }
            }
        }

        return extras;
    }

    private Dictionary<Key, List<Typed>> Bypasses(IEnumerable<Entity> all)
    {
        var extras = new Dictionary<Key, List<Typed>>();

        foreach (var ability in all.Where(entity => entity.Type == EntityType.Ability))
        {
            foreach (var (type, tags) in new[]
                     {
                         (ModifierType.Cost, ability.BypassCostTags ?? []),
                         (ModifierType.CastTime, ability.InstantTags ?? []),
                     })
            {
                foreach (var tag in tags)
                {
                    foreach (var source in _tagsBySource.GetValueOrDefault(tag) ?? [])
                    {
                        if (Heroic(_sources.GetValueOrDefault(source), [KeyOf(ability)]) is not [var target]) continue;

                        var condition = Satisfied(source, tag) ? null : Resolve(source, [tag]);
                        var candidate = new ModifierCandidate { Row = string.Empty, Value = 0 };
                        var classified = new ModifierClass(type, ModifierBasis.Property, tag) { Value = 0 };

                        if (!extras.TryGetValue(source, out var held)) extras[source] = held = [];

                        if (held.FirstOrDefault(record => record.Class.Type == type &&
                                                          Describe(record.Condition) == Describe(condition)) is { } same)
                        {
                            held[held.IndexOf(same)] = same with { Targets = [.. same.Targets.Append(target).Distinct()] };
                        }
                        else
                        {
                            held.Add(new Typed(candidate, classified, [target], condition is { IsEmpty: false } ? condition : null));
                        }
                    }
                }
            }
        }

        return extras;
    }

    public IReadOnlyList<SourceChange>? Of(Entity entity, DocumentProjection.References references)
    {
        var typed = entity switch
        {
            _ when IsSource(entity) => _records.GetValueOrDefault(KeyOf(entity)) ?? [],
            { Type: EntityType.Effect } => Own(entity),
            _ => [],
        };

        return typed.Count == 0 ? null : [.. typed.Select(record => Record(record, references))];
    }

    public IReadOnlyList<EntityReference>? ModifiedBy(Entity entity, DocumentProjection.References references) =>
        entity is { Type: not null, NetId: not null } && _modifiedBy.TryGetValue(KeyOf(entity), out var sources)
            ? [.. sources.Select(source => references.Of(source.Type, source.Id)!)]
            : null;

    public IReadOnlyCollection<(EntityType Type, int Id)> Modifying(Entity entity) =>
        entity is { Type: not null, NetId: not null } && _modifiedBy.TryGetValue(KeyOf(entity), out var sources)
            ? [.. sources.Where(source => source.Type is EntityType.Talent or EntityType.LegendaryPower).Select(source => (source.Type, source.Id))]
            : [];

    public EntityReference? Replaces(Entity entity, DocumentProjection.References references) =>
        entity is { Type: not null, NetId: not null } && _replaces.TryGetValue(KeyOf(entity), out var original)
            ? references.Of(original.Type, original.Id)
            : null;

    public IReadOnlyList<EntityReference>? GrantedBy(Entity entity, DocumentProjection.References references) =>
        entity is { Type: not null, NetId: not null } && _grantedBy.TryGetValue(KeyOf(entity), out var sources)
            ? [.. sources.Select(source => references.Of(source.Type, source.Id)!)]
            : null;

    public IReadOnlyList<EntityReference>? Grants(Entity entity, DocumentProjection.References references) =>
        entity is { Type: not null, NetId: not null } && _grants.TryGetValue(KeyOf(entity), out var granted)
            ? [.. granted.Select(target => references.Of(target.Type, target.Id)!)]
            : null;

    public TuningReference Reference(
        string key, Entity? context, DocumentProjection.References references, object? value = null)
    {
        var row = Absolute(key, context?.Tuning);
        var self = context is { Type: { } type, NetId: { } id } ? new Key(type, id) : (Key?)null;

        var (scope, owner) = Deepest(row);
        var remainder = scope is null ? TuningNamespace.Relative(row, context?.Tuning) : row[(scope.Length + 1)..];

        EntityReference? talent = null;

        if (Scoped(row) is var (found, name, segment))
        {
            talent = references.Of(found.Type, found.Id);

            var marker = $"{segment[1..]}{name}.";
            var within = remainder.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (remainder.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder[marker.Length..];
            }
            else if (within > 0 && remainder[within - 1] == '.')
            {
                remainder = remainder[..(within - 1)] + "." + remainder[(within + marker.Length)..];
            }
        }
        else if (TalentClaiming(row) is var (claimer, claimed) && remainder.Length > 0)
        {
            talent = references.Of(claimer.Type, claimer.Id);

            if (row.StartsWith(claimed + ".", StringComparison.OrdinalIgnoreCase))
            {
                remainder = row[(claimed.Length + 1)..];
            }
        }

        return new TuningReference(Fold(remainder))
        {
            Owner = owner is { } held && held != self ? references.Of(held.Type, held.Id) : null,
            Talent = talent,
            Value = value,
        };
    }

    public TuningReference? Located(string asset, string name, DocumentProjection.References references) =>
        _assets.TryGetValue(asset, out var owner)
            ? new TuningReference(name) { Owner = references.Of(owner.Type, owner.Id) }
            : null;

    public static string Fold(string path) => string.Concat(path.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static string Dashed(string tag) => tag.Replace('.', '-');

    private static bool IsSource(Entity entity) => entity.Type is EntityType.Talent or EntityType.LegendaryPower;

    public IReadOnlyList<(EntityType Type, int Id)> Holding(string tag) =>
        _tagsBySource.TryGetValue(tag, out var sources) ? [.. sources.Select(source => (source.Type, source.Id))] : [];

    private static SortedSet<Key> Keys() => new(Comparer<Key>.Create(Compare));

    private static void Index(Dictionary<string, SortedSet<Key>> index, string tag, Key key)
    {
        if (!index.TryGetValue(tag, out var keys)) index[tag] = keys = Keys();
        keys.Add(key);
    }

    private void Grant(Key source, Key granted)
    {
        if (!_grants.TryGetValue(source, out var grants)) _grants[source] = grants = Keys();
        grants.Add(granted);

        if (!_grantedBy.TryGetValue(granted, out var granters)) _grantedBy[granted] = granters = Keys();
        granters.Add(source);
    }

    private IEnumerable<Key> GrantedEffects(Entity source) =>
        (source.GrantedEffects ?? [])
            .Select(name => _assets.TryGetValue(name, out var effect) ? effect : (Key?)null)
            .OfType<Key>()
            .Where(effect => effect.Type == EntityType.Effect);

    private bool Elsewhere(Key source, string row)
    {
        if (row.Length == 0 || !_rowTags.TryGetValue(row, out var tags)) return false;

        var talented = tags.Where(tag => tag.StartsWith(TalentTagPrefix, StringComparison.Ordinal)).ToArray();

        return talented.Length > 0 &&
               !talented.Any(tag => _tagsBySource.GetValueOrDefault(tag)?.Contains(source) == true) &&
               talented.All(tag => _tagsBySource.GetValueOrDefault(tag) is { Count: > 0 } ||
                                   !_granters.ContainsKey(tag));
    }

    private static void Gated(Dictionary<string, SortedSet<string>> index, string row, string tag)
    {
        if (!index.TryGetValue(row, out var tags)) index[row] = tags = new(StringComparer.Ordinal);
        tags.Add(tag);
    }

    private static bool IsSpell(Entity entity) => entity.Type is EntityType.Ability or EntityType.Effect;

    private (Key Talent, string Name, string Segment)? Scoped(string row)
    {
        foreach (var segment in TalentSegments)
        {
            var at = row.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
            if (at <= 0) continue;

            var after = row[(at + segment.Length)..].Split('.');
            if (after.Length < 2) return null;

            for (var length = 1; length < after.Length; length++)
            {
                var name = string.Join('.', after[..length]);

                if (TalentNamed(row[..at], name) is { } whole) return (whole, name, segment);
                if (TalentNamed(row[..at], after[length - 1]) is { } part) return (part, name, segment);
            }

            return null;
        }

        return null;
    }

    private (Key Source, string Scope)? TalentClaiming(string row)
    {
        var claimers = _sourceRows
            .Where(entry => entry.Value.ContainsKey(row))
            .Select(entry => entry.Key)
            .ToArray();

        if (claimers is not [var only]) return null;

        var scoped = _sourceRows[only].Keys
            .Where(key => TalentSegments.Any(segment => key.Contains(segment, StringComparison.OrdinalIgnoreCase)))
            .Select(key => key.Split('.'))
            .ToArray();

        if (scoped.Length == 0) return null;

        var common = scoped[0][..^1];

        foreach (var segments in scoped.Skip(1))
        {
            var length = 0;
            while (length < common.Length && length < segments.Length - 1 &&
                   common[length].Equals(segments[length], StringComparison.OrdinalIgnoreCase))
            {
                length++;
            }

            common = common[..length];
        }

        var scope = string.Join('.', common);

        return TalentSegments.Any(segment => $"{scope}.".Contains(segment, StringComparison.OrdinalIgnoreCase))
            ? (only, scope)
            : null;
    }

    private Key? TalentNamed(string scope, string name)
    {
        var granting = _granters.GetValueOrDefault($"{TalentTagPrefix}{scope}.{name}");
        if (granting is { Count: 1 }) return granting.First();

        var ability = HeroNames.LastSegment(scope);

        var named = _granters
            .Where(entry => entry.Key.StartsWith(TalentTagPrefix, StringComparison.Ordinal) &&
                            entry.Key.EndsWith($".{ability}.{name}", StringComparison.Ordinal))
            .SelectMany(entry => entry.Value)
            .Distinct()
            .ToArray();

        if (named is [var only]) return only;

        var hero = scope.Split('.')[0];

        var heroes = _granters
            .Where(entry => entry.Key.StartsWith($"{TalentTagPrefix}{hero}.", StringComparison.Ordinal) &&
                            entry.Key.EndsWith($".{name}", StringComparison.Ordinal))
            .SelectMany(entry => entry.Value)
            .Distinct()
            .ToArray();

        return heroes is [var sole] ? sole : null;
    }

    private string Absolute(string key, string? tuning)
    {
        if (Deepest(key).Scope is not null || tuning is null) return key;

        var prefixed = $"{tuning}.{key}";

        return Deepest(prefixed).Scope is not null ? prefixed : key;
    }

    private (string? Scope, Key? Owner) Deepest(string row)
    {
        var scope = row;

        while (true)
        {
            var dot = scope.LastIndexOf('.');
            if (dot <= 0) return (null, null);

            scope = scope[..dot];

            if (_owners.TryGetValue(scope, out var owner)) return (scope, owner);
        }
    }

    private static SourceChange Record(Typed record, DocumentProjection.References references)
    {
        IReadOnlyList<EntityReference>? Refs(IReadOnlyList<Key> keys) =>
            keys.Count == 0 ? null : [.. keys.Select(key => references.Of(key.Type, key.Id)!)];

        return new SourceChange
        {
            Type = record.Class.Type,
            Basis = record.Class.Basis,
            Targets = Refs(record.Targets),
            Attribute = record.Class.Attribute,
            Value = record.Class.Value,
            ValueLow = record.Class.ValueLow,
            ValueHigh = record.Class.ValueHigh,
            PerCount = record.PerCount,
            Max = record.Max,
            Counts = record.Counts.Count == 0 ? null : Refs(record.Counts),
            Uncounted = record.Uncounted,
            CountsStacks = record.CountsStacks ? true : null,
            CountsCall = record.CountsCall,
            Per = Per(record.Candidate),
            Coefficient = record.Candidate.Coefficient,
            ScalesOff = record.Candidate.ScalesOff,
            Condition = record.Condition is { IsEmpty: false } condition
                ? new ModifierCondition
                {
                    Effects = Refs(condition.Effects),
                    Stacks = condition.Stacks,
                    TargetEffects = Refs(condition.TargetEffects),
                    Chance = condition.Chance,
                    Talents = Refs(condition.Talents),
                    CastBy = Refs(condition.CastBy),
                    Cooldowns = Refs(condition.Cooldowns),
                    OwnEffects = Refs(condition.OwnEffects),
                    StateSources = Refs(condition.StateSources),
                    TargetStates = condition.TargetStates.Count == 0 ? null : condition.TargetStates,
                    TargetHealthFractions = condition.TargetHealth.Count == 0 ? null : condition.TargetHealth,
                    HealthFractions = condition.Health.Count == 0 ? null : condition.Health,
                    States = condition.States.Count == 0 ? null : condition.States,
                }
                : null,
            Row = record.Candidate.Row,
        };
    }

    private Dictionary<Key, List<(Key Effect, double Duration)>> Computed(IEnumerable<Entity> all)
    {
        var computed = new Dictionary<Key, List<(Key Effect, double Duration)>>();

        foreach (var effect in all.Where(entity => entity.Type == EntityType.Effect && entity.Talents is not null))
        {
            var hero = effect.Tuning?.Split('.')[0];

            foreach (var (name, fields) in effect.Talents!)
            {
                if (fields.GetValueOrDefault(DurationField) is not double duration) continue;

                var tested = (effect.TestedTags ?? [])
                    .Where(tag => tag.StartsWith(TalentTagPrefix, StringComparison.Ordinal) &&
                                  tag.EndsWith($".{name}", StringComparison.Ordinal))
                    .ToArray();

                var named = _tagsBySource
                    .Where(entry => tested.Length > 0
                        ? tested.Contains(entry.Key)
                        : entry.Key.EndsWith($".{name}", StringComparison.Ordinal))
                    .ToArray();

                if (named.Select(entry => entry.Value).Distinct().Count() > 1 && hero is not null)
                {
                    named = [.. named.Where(entry => entry.Key.Contains($".{hero}.", StringComparison.Ordinal))];
                }

                var granting = named.SelectMany(entry => entry.Value).Distinct().ToArray();

                if (granting.Length == 0)
                {
                    granting = [.. _sourceRows
                        .Where(entry => entry.Value.Keys.Any(row => TalentSegments.Any(segment =>
                            row.Contains($"{segment}{name}.", StringComparison.OrdinalIgnoreCase))))
                        .Select(entry => entry.Key)];
                }

                if (granting.Length > 1 && effect.OwningHeroes is { Count: > 0 } owning)
                {
                    granting = [.. granting.Where(candidate =>
                        _sources.GetValueOrDefault(candidate) is { } held &&
                        (held.OwningHeroes ?? held.Heroes ?? []).Intersect(owning, StringComparer.Ordinal).Any())];
                }

                if (granting is not [var source]) continue;

                if (!computed.TryGetValue(source, out var held)) computed[source] = held = [];
                held.Add((KeyOf(effect), duration));
            }
        }

        return computed;
    }

    private IReadOnlyList<Typed> Source(Key self, IReadOnlyList<(Key Effect, double Duration)> computed)
    {
        var entity = _sources.GetValueOrDefault(self);
        var text = entity?.Description;

        var candidates = new List<(ModifierCandidate Candidate, Key? Owner, Key? Tester)>();
        var rows = _sourceRows.GetValueOrDefault(self);

        foreach (var (row, value) in rows ?? [])
        {
            candidates.Add((Candidate(row, value, _byRow.GetValueOrDefault(row), text), Owner(row), null));
        }

        var sourced = _sourceModifiers.GetValueOrDefault(self) ?? [];
        var gatedBy = new Dictionary<ModifierCandidate, string[]>(ReferenceEqualityComparer.Instance);

        static bool Same(EffectModifier first, EffectModifier second) =>
            first.Row is null && second.Row is null && first.Attribute == second.Attribute &&
            first.Value == second.Value && first.Op == second.Op;

        foreach (var (tester, modifier, tag) in sourced)
        {
            if (modifier.Row is { } row && rows?.ContainsKey(row) == true) continue;
            if (candidates.Any(held => held.Candidate.Row == modifier.Row && modifier.Row is not null)) continue;
            if (tester.Type == EntityType.Ability &&
                sourced.Any(other => other.Tester.Type == EntityType.Effect && Same(other.Modifier, modifier))) continue;
            if (candidates.Any(held => held.Tester == tester && held.Candidate.Row.Length == 0 &&
                                       _byCandidate.GetValueOrDefault(held.Candidate) is { } seen && Same(seen, modifier))) continue;

            var owner = modifier.Row is { } named && Owner(named) is { } found ? found : tester;
            var candidate = Candidate(modifier.Row ?? string.Empty, modifier.Value, modifier, text);

            _byCandidate[candidate] = modifier;

            if (modifier.Row is null && _entities.GetValueOrDefault(tester)?.GatedModifiers is { } gated)
            {
                gatedBy[candidate] =
                [
                    .. gated.Where(entry => entry.Key != tag && entry.Value.Any(other => Same(other, modifier)))
                        .Select(entry => entry.Key),
                ];
            }

            candidates.Add((candidate, owner, tester));
        }

        var typed = new List<Typed>();

        foreach (var (candidate, owner, tester) in candidates)
        {
            var condition = Condition(self, candidate);

            if (gatedBy.GetValueOrDefault(candidate) is { Length: > 0 } extra &&
                extra.Where(gate => !Satisfied(self, gate)).ToArray() is { Length: > 0 } open)
            {
                condition = condition is null ? Resolve(self, open) : condition.Merged(Resolve(self, open));
            }

            if (candidate.Row.Length > 0 &&
                Scoped(candidate.Row) is { } scope && scope.Talent != self &&
                !(_bound.Contains((self, candidate.Row)) &&
                  (condition is null || condition.Effects.Count == 0 || condition.OwnEffects.Count > 0)))
            {
                continue;
            }

            if (Elsewhere(self, candidate.Row)) continue;

            if (ModifierTypes.Classify(candidate) is not { } classified) continue;
            if (!Applies(classified)) continue;

            var targets = Heroic(entity, Targets(self, Aimed(classified, candidate), owner, classified.Type));

            IReadOnlyList<Key> carriers = tester is { } carrier
                ? [carrier]
                : candidate.Row.Length > 0 && _modifierTesters.GetValueOrDefault(candidate.Row) is { } testers
                    ? [.. testers]
                    : [];

            if (Scope(candidate.Row) is { Count: > 0 } scoped && carriers.Count > 0 &&
                classified is { Type: ModifierType.AttributeMultiplier, Attribute: IncomingDamage })
            {
                classified = classified with { Type = ModifierType.DamageMultiplier, Attribute = null };
                targets = [.. scoped];
                condition = (condition ?? Empty()) with { TargetEffects = carriers };

                foreach (var holder in carriers)
                {
                    if (_entities.GetValueOrDefault(holder)?.Modifiers?.Any(modifier =>
                            string.Equals(modifier.Row, candidate.Row, StringComparison.OrdinalIgnoreCase)) != true)
                    {
                        continue;
                    }

                    _scopedAway.Add((holder, candidate.Row));
                    Grant(self, holder);
                }
            }

            targets = Filled(self, classified, Lifted(classified.Type, targets));
            targets = [.. Effectual(classified, targets).Where(target => _entities.GetValueOrDefault(target)?.Uncooked != true)];

            if (classified.Type is ModifierType.AttributeIncrease && targets.Count > 0 && targets.All(Utility))
            {
                if (!_gains.TryGetValue(self, out var gained)) _gains[self] = gained = [];
                gained.Add((classified.Attribute ?? string.Empty, classified.Value, candidate.Coefficient, candidate.Row, Stepped(candidate, "Multiply", -1)));
                continue;
            }

            if (classified.Conversion != ModifierTypes.CountedScale &&
                !Printed(classified, candidate, entity) && !Changed(classified, candidate, entity, targets))
            {
                continue;
            }

            var fitting = Fitting(classified.Type, targets);

            if (fitting.Count == 0) continue;

            IEnumerable<string> Gating(Key target) =>
                target.Type == EntityType.Effect && _entities.GetValueOrDefault(target)?.Name is { } asset &&
                _procGates.GetValueOrDefault(asset) is { } procs &&
                procs.Where(gate => !Satisfied(self, gate)).ToArray() is { Length: > 0 } open &&
                condition is not { Effects.Count: > 0 }
                    ? open
                    : [];

            foreach (var group in fitting.GroupBy(target =>
                         string.Join(";", (_entities.GetValueOrDefault(target)?.OngoingTags ?? []).Concat(Gating(target))),
                         StringComparer.Ordinal))
            {
                var grouped = condition;

                if (group.Key.Length > 0)
                {
                    var required = Resolve(self, group.Key.Split(';'));
                    grouped = grouped is null ? required : grouped.Merged(required);
                }

                if (grouped is { IsEmpty: true }) grouped = null;

                typed.Add(new Typed(candidate, classified, [.. group], grouped));
            }
        }

        var asserted = typed
            .Where(record => record.Class.Basis != ModifierBasis.Keyword)
            .SelectMany(record => Shown(record.Class, record.Candidate, entity))
            .ToHashSet();

        typed.RemoveAll(record => record.Class.Basis == ModifierBasis.Keyword &&
                                  Shown(record.Class, record.Candidate, entity) is { Count: > 0 } matched &&
                                  matched.All(asserted.Contains));

        var rolled = typed
            .Select(record => record.Condition?.ChanceRow)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var chances = typed
            .Select(record => record.Condition?.Chance)
            .OfType<double>()
            .ToHashSet();

        typed.RemoveAll(record => record.Class.Type == ModifierType.ProcChance &&
                                  (rolled.Contains(record.Candidate.Row) ||
                                   (record.Class.Value is { } chance && chances.Contains(chance))));

        var seen = new HashSet<string>(StringComparer.Ordinal);

        typed.RemoveAll(record => !seen.Add(
            $"{record.Candidate.Row}|{record.Class.Type}|{record.Class.Attribute}|{record.Class.Value}|{record.Candidate.Coefficient}|" +
            $"{string.Join(",", record.Targets)}|{Describe(record.Condition)}"));

        typed = Counted(typed);
        typed = [.. typed.Select(record => Scaled(record, null))];

        var counted = new Dictionary<string, Typed>(StringComparer.Ordinal);

        foreach (var record in typed.Where(record => record.Counts.Count > 0))
        {
            foreach (var use in record.Candidate.Uses.Where(use => use.StartsWith(RowSinks.VariableUse, StringComparison.Ordinal)))
            {
                counted.TryAdd(use[RowSinks.VariableUse.Length..], record);
            }
        }

        typed = [.. typed.Select(record => Scaled(record, counted))];

        typed.RemoveAll(record => record.Class.Conversion == ModifierTypes.CountedScale && record.PerCount is null && record.CountsCall is null);

        foreach (var record in typed)
        {
            if (!Attributes.Contains(record.Class.Type) || record.Class.Attribute is not { } attribute) continue;

            foreach (var target in record.Targets.Where(target => target.Type == EntityType.Effect))
            {
                if (_entities.GetValueOrDefault(target) is not { } effect) continue;

                if (Own(effect).Any(own => own.Class.Type == record.Class.Type && own.Class.Attribute == attribute &&
                                           own.Class.Value is null && own.Candidate.Coefficient is null))
                {
                    _filled.Add((target, attribute));
                }
            }
        }

        typed.RemoveAll(record => Restates(self, record));

        foreach (var (effect, duration) in computed)
        {
            var candidate = new ModifierCandidate { Row = string.Empty, Value = duration, Text = text };
            var classified = new ModifierClass(ModifierType.Duration, ModifierBasis.Attribute, "custom magnitude calculation")
            {
                Value = duration,
            };

            var bare = _entities.GetValueOrDefault(effect)?.Duration;
            var change = bare is { } before ? classified with { Value = duration - before } : null;

            var ratio = bare is { } start && start > 0
                ? classified with { Type = ModifierType.DurationMultiplier, Value = duration / start }
                : null;

            if (!Printed(classified, candidate, entity) &&
                (change is null || !Printed(change, candidate with { Value = change.Value }, entity)) &&
                (ratio is null || !Printed(ratio, candidate with { Value = ratio.Value }, entity)))
            {
                continue;
            }

            if (typed.Any(held => held.Class.Type == ModifierType.Duration && held.Targets.Contains(effect))) continue;

            typed.Add(new Typed(candidate, classified, [effect], null));
        }

        return typed;
    }

    private List<Typed> Counted(List<Typed> typed)
    {
        var merged = new List<Typed>();

        foreach (var group in typed.GroupBy(record =>
                     $"{record.Class.Type}|{record.Class.Attribute}|{string.Join(",", record.Targets)}|{Describe(record.Condition)}"))
        {
            var records = group.ToList();

            var cap = records.FirstOrDefault(record => Stepped(record, "FMin", 1));
            var counted = records.FirstOrDefault(record => record != cap && Stepped(record, "Multiply", -1));
            var bases = records.Where(record => record != cap && record != counted).ToList();

            if (records.Count >= 2 && cap is not null && counted is not null && bases is [var start])
            {
                var read = Read(counted.Candidate);

                merged.Add(start with
                {
                    PerCount = counted.Class.Value,
                    Max = cap.Class.Value,
                    Counts = read is null ? [] : [.. read.Tags.SelectMany(tag => _assetTagged.GetValueOrDefault(tag) ?? []).Distinct()],
                    Uncounted = read?.Skipped,
                    CountsStacks = read?.Stacks == true,
                });
                continue;
            }

            merged.AddRange(records);
        }

        return merged;
    }

    private Typed Scaled(Typed record, IReadOnlyDictionary<string, Typed>? counted)
    {
        if (record.PerCount is not null || Read(record.Candidate) is not { } read) return record;

        if (!Attributes.Contains(record.Class.Type) && read.Call is null && read.Inverts is null) return record;

        if (read.Inverts is { } divisor && Stepped(record.Candidate, "Multiply", -1) && record.Candidate.Value is { } per &&
            counted?.GetValueOrDefault(divisor) is { } inverted)
        {
            return record with
            {
                Class = record.Class with { Value = 1 },
                PerCount = per,
                Counts = inverted.Counts,
                Uncounted = inverted.Uncounted,
                CountsStacks = inverted.CountsStacks,
            };
        }

        if (read.Attribute is { } scale && record.Candidate.Coefficient is null && record.Class.Value is { } share)
        {
            return record with
            {
                Candidate = record.Candidate with { Coefficient = share, ScalesOff = [scale] },
                Class = record.Class with { Value = null },
            };
        }

        if (read.Call is { } counter && Stepped(record.Candidate, "Multiply", -1) && record.Candidate.Value is { } added)
        {
            return record with
            {
                Class = record.Class with { Value = 1 },
                PerCount = added,
                CountsCall = counter,
            };
        }

        if (read.Tags.Count > 0 && Stepped(record.Candidate, Subtract, 1) && record.Class.Value is { } each)
        {
            return record with
            {
                Class = record.Class with { Value = 1 },
                PerCount = -each,
                Counts = [.. read.Tags.SelectMany(tag => _assetTagged.GetValueOrDefault(tag) ?? []).Distinct()],
                Uncounted = read.Skipped,
                CountsStacks = read.Stacks,
            };
        }

        return record;
    }

    private const string Subtract = "Subtract_";

    private CountRead? Read(ModifierCandidate candidate)
    {
        var variables = candidate.Uses
            .Where(use => use.StartsWith(RowSinks.VariableUse, StringComparison.Ordinal))
            .Select(use => use[RowSinks.VariableUse.Length..])
            .ToHashSet(StringComparer.Ordinal);

        return _entities.Values
            .SelectMany(entity => entity.Counts ?? new Dictionary<string, CountRead>())
            .Where(entry => variables.Contains(entry.Key))
            .Select(entry => entry.Value)
            .FirstOrDefault();
    }

    private static bool Stepped(Typed record, string op, int at) => Stepped(record.Candidate, op, at);

    private static bool Stepped(ModifierCandidate candidate, string op, int at) =>
        candidate.Uses.Any(use => use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal) &&
                                         use.Contains('|') &&
                                         use[(use.IndexOf('|') + 1)..].Split(',').Any(step =>
                                             step.StartsWith(op, StringComparison.Ordinal) &&
                                             (at < 0 ? step.Contains("(_)", StringComparison.Ordinal)
                                                 : step.Contains($"@{at}(", StringComparison.Ordinal))));

    private IReadOnlyList<Key> Filled(Key self, ModifierClass classified, IReadOnlyList<Key> targets)
    {
        if (classified.Type is not (ModifierType.AttributeIncrease or ModifierType.AttributeMultiplier
            or ModifierType.AttributeOverride) || classified.Attribute is null || targets.Count == 0)
        {
            return targets;
        }

        var filled = new List<Key>();

        foreach (var target in targets)
        {
            if (target.Type != EntityType.Ability || _entities.GetValueOrDefault(target)?.Name is not { } asset) return targets;

            var holes = (_applied.GetValueOrDefault(asset) ?? [])
                .Where(effect => _filled.Contains((effect, classified.Attribute)) ||
                                 (_entities.GetValueOrDefault(effect) is { } held ? Own(held) : [])
                    .Any(own => own.Class.Type == classified.Type && own.Class.Attribute == classified.Attribute &&
                                own.Class.Value is null && own.Candidate.Coefficient is null))
                .ToArray();

            if (holes is not [var effect]) return targets;

            _filled.Add((effect, classified.Attribute));

            Grant(self, effect);
            filled.Add(effect);
        }

        return filled;
    }

    private static ModifierCandidate Aimed(ModifierClass classified, ModifierCandidate candidate)
    {
        string[] direct = [.. candidate.Uses.Where(use => !Divides(use))];
        if (direct.Length > 0 && direct.Length < candidate.Uses.Count) candidate = candidate with { Uses = direct };

        if (classified.Type != ModifierType.TickRateMultiplier) return candidate;

        string[] ticking =
        [
            .. candidate.Uses.Where(use => use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal) &&
                                           (use.Contains(TickPeriod, StringComparison.Ordinal) ||
                                            use.StartsWith(RowSinks.SinkUse + SetSink, StringComparison.Ordinal) &&
                                            use.Contains(PeriodScaler, StringComparison.Ordinal))),
        ];

        return ticking.Length == 0 ? candidate : candidate with { Uses = ticking };
    }

    private static bool Divides(string use) =>
        use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal) &&
        (use.Contains("Divide_DoubleDouble@1(_)", StringComparison.Ordinal) || use.Contains("SafeDivide@1(_)", StringComparison.Ordinal));

    private const string TickPeriod = "TickPeriodScaler";
    private const string PeriodScaler = "PeriodScaler";
    private const string SetSink = "set:";

    private static readonly HashSet<ModifierType> Attributes =
        [ModifierType.AttributeIncrease, ModifierType.AttributeMultiplier, ModifierType.AttributeOverride];


    private IReadOnlyList<Key> Lifted(ModifierType type, IReadOnlyList<Key> targets)
    {
        var lifted = Keys();
        var named = targets.ToHashSet();

        foreach (var target in targets)
        {
            var key = target;

            if (Coefficients.Contains(type) && key.Type == EntityType.Effect &&
                _entities.GetValueOrDefault(key) is { } effect && _partOf(effect) is { } whole &&
                _entities.ContainsKey(new Key(EntityType.Ability, whole)) &&
                Carriers(whole, null, Output(type)) is { Length: > 0 } carriers && carriers.Contains(key) && carriers.All(named.Contains))
            {
                key = new Key(EntityType.Ability, whole);
            }

            lifted.Add(key.Type == EntityType.Ability && _parents.TryGetValue(key, out var parent) ? parent : key);
        }

        return [.. lifted];
    }

    private Key[] Carriers(int ability, Placeholder? only, string? output = null)
    {
        var whole = new Key(EntityType.Ability, ability);
        Parts(whole);

        var name = _entities.GetValueOrDefault(whole)?.DisplayName;
        var parts = (_parts!.GetValueOrDefault(ability) ?? [])
            .Where(part => !_grantedBy.ContainsKey(part) &&
                           !(_entities.GetValueOrDefault(part)?.DisplayName is { } own && own != name && _sourceNames.Contains(own)))
            .ToHashSet();
        var tuning = _entities.GetValueOrDefault(whole)?.Tuning;

        Placeholder[] printed =
        [
            .. (_entities.GetValueOrDefault(whole)?.Placeholders?.Values ?? [])
                .Where(placeholder => placeholder.Coefficient is not null && placeholder.Attribute is not null),
        ];

        var chosen = only is not null
            ? [only]
            : output is null
                ? printed
                : printed.Where(placeholder =>
                    (placeholder.ScalesOff?.Contains(HealOutput, StringComparer.Ordinal) == true ? HealOutput : DamageOutput) == output).ToArray();

        Key[] Owners(Placeholder placeholder) =>
        [
            .. (placeholder.Constants?.Keys ?? [])
                .Select(key => Deepest(Absolute(key, tuning)).Owner)
                .OfType<Key>()
                .Where(parts.Contains)
                .Distinct(),
        ];

        var carriers = new HashSet<Key>();

        foreach (var placeholder in chosen)
        {
            if (placeholder.Attribute is not { } attribute) continue;

            var owners = Owners(placeholder);

            if (owners.Length > 0)
            {
                carriers.UnionWith(owners);
                continue;
            }

            var claimed = printed
                .Where(other => other.Key != placeholder.Key)
                .SelectMany(Owners)
                .ToHashSet();

            var matched = parts
                .Where(part => _entities.GetValueOrDefault(part)?.ScalesOff?.Contains(attribute, StringComparer.Ordinal) == true)
                .ToArray();

            var yields = placeholder.ScalesOff?.Contains(HealOutput, StringComparer.Ordinal) == true ? HealOutput : DamageOutput;

            foreach (var narrowing in new Func<Key, bool>[]
                     {
                         part => _entities.GetValueOrDefault(part)?.ScalesOff?.Contains(yields, StringComparer.Ordinal) == true,
                         part => _entities.GetValueOrDefault(part)?.Rows?.Values.Any(value =>
                             placeholder.Coefficient is { } coefficient && Math.Abs(value - coefficient) < Tolerance) == true,
                         part => !claimed.Contains(part),
                     })
            {
                var narrowed = matched.Where(narrowing).ToArray();
                if (narrowed.Length > 0) matched = narrowed;
            }

            carriers.UnionWith(matched);
        }

        return [.. carriers.OrderBy(carrier => carrier.Id)];
    }
    public IReadOnlyList<EntityReference>? CarriedBy(
        Entity ability, Placeholder placeholder, DocumentProjection.References references) =>
        ability is { Type: EntityType.Ability, NetId: { } id } && placeholder is { Coefficient: not null, Attribute: not null } &&
        Carriers(id, placeholder) is { Length: > 0 } carriers
            ? [.. carriers.Select(carrier => references.Of(carrier.Type, carrier.Id)).OfType<EntityReference>()]
            : null;

    private static string? Output(ModifierType type) => type switch
    {
        ModifierType.DamageMultiplier => DamageOutput,
        ModifierType.HealingMultiplier => HealOutput,
        _ => null,
    };

    private const string HealOutput = "HealScale";
    private const double Tolerance = 1e-9;
    private const string DamageOutput = "DamageScale";

    private IReadOnlyList<Key> Effectual(ModifierClass classified, IReadOnlyList<Key> targets)
    {
        if (classified.Type is not (ModifierType.AttributeIncrease or ModifierType.AttributeMultiplier
            or ModifierType.AttributeOverride) || classified.Attribute is not { } attribute)
        {
            return targets;
        }

        var mapped = Keys();

        foreach (var target in targets)
        {
            Key[] parts = target.Type == EntityType.Ability
                ? [.. Parts(target).Where(effect => _entities.GetValueOrDefault(effect)?.ScalesOff?.Contains(attribute, StringComparer.Ordinal) == true)]
                : [];

            if (parts.Length == 0) mapped.Add(target);
            else mapped.UnionWith(parts);
        }

        return [.. mapped];
    }

    private IEnumerable<Key> Parts(Key ability)
    {
        if (_parts is null)
        {
            _parts = [];

            foreach (var (key, entity) in _entities)
            {
                if (key.Type != EntityType.Effect || _partOf(entity) is not { } whole) continue;

                if (!_parts.TryGetValue(whole, out var held)) _parts[whole] = held = Keys();
                held.Add(key);
            }
        }

        var applied = _entities.GetValueOrDefault(ability)?.Name is { } asset ? _applied.GetValueOrDefault(asset) ?? [] : [];

        return applied.Union(_parts.GetValueOrDefault(ability.Id) ?? []);
    }

    private bool Utility(Key target) =>
        target.Type == EntityType.Effect &&
        _entities.GetValueOrDefault(target) is { DisplayName: null, Modifiers: { Count: > 0 } modifiers } &&
        modifiers.All(modifier => modifier is { Value: null, Coefficient: null });

    private bool Restates(Key source, Typed record)
    {
        if (record.Class.Type is not (ModifierType.AttributeIncrease or ModifierType.AttributeMultiplier
            or ModifierType.AttributeOverride)) return false;

        bool Stated(Key target) =>
            target.Type == EntityType.Effect &&
            _entities.GetValueOrDefault(target) is { } effect &&
            Own(effect).Any(own =>
                own.Class.Type == record.Class.Type && own.Class.Attribute == record.Class.Attribute &&
                own.Class.Value == record.Class.Value && own.Candidate.Coefficient == record.Candidate.Coefficient);

        var restated = record.Targets.Count > 0 && record.Targets.All(Stated);

        if (restated)
        {
            foreach (var target in record.Targets) Grant(source, target);
        }

        return restated || (_grants.GetValueOrDefault(source)?.Any(Stated) ?? false);
    }

    private IReadOnlySet<Key>? Scope(string row)
    {
        if (row.Length == 0 || !_modifierTags.TryGetValue(row, out var tags)) return null;

        var scoped = Keys();

        foreach (var tag in tags) scoped.UnionWith(_abilityTagged.GetValueOrDefault(tag) ?? []);

        return scoped;
    }

    private IReadOnlyList<Key> Targets(Key source, ModifierCandidate candidate, Key? owner, ModifierType type)
    {
        var targets = Keys();

        if (Held(candidate) is { } holders)
        {
            targets.UnionWith(holders);
            return [.. targets];
        }

        foreach (var use in candidate.Uses.Where(use => use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal)))
        {
            var named = Keys();

            foreach (Match match in ClassPattern().Matches(use))
            {
                if (_assets.TryGetValue(match.Groups["asset"].Value, out var applied)) named.Add(applied);
            }

            foreach (Match match in ActorPattern().Matches(use))
            {
                named.UnionWith(_spawners.GetValueOrDefault(match.Groups["actor"].Value) ?? []);
            }

            if (use.Contains($"<{RowSinks.GranterMark}>", StringComparison.Ordinal) &&
                candidate.Row.Length > 0 && _binders.TryGetValue(candidate.Row, out var readers))
            {
                foreach (var reader in readers)
                {
                    if (_entities.GetValueOrDefault(reader)?.Name is { } asset)
                    {
                        named.UnionWith(_abilityGranters.GetValueOrDefault(asset) ?? []);
                    }
                }
            }

            if (named.Count > 0)
            {
                targets.UnionWith(named);
                continue;
            }

            foreach (var gate in Gates(use)) targets.UnionWith(_abilityTagged.GetValueOrDefault(gate) ?? []);

            foreach (Match tagged in TagPattern().Matches(use))
            {
                var tag = tagged.Groups["tag"].Value;

                switch (tagged.Groups["kind"].Value)
                {
                    case "assettag":
                        targets.UnionWith(_assetTagged.GetValueOrDefault(tag) ?? []);
                        break;
                    case "grantedtag":
                        targets.UnionWith(_adders.GetValueOrDefault(tag) ?? []);
                        break;
                    default:
                        targets.UnionWith(_assetTagged.GetValueOrDefault(tag) is { Count: > 0 } effects
                            ? effects
                            : _abilityTagged.GetValueOrDefault(tag) ?? []);
                        break;
                }
            }
        }

        if (targets.Count > 0)
        {
            if (candidate.Row.Length > 0 && _binders.TryGetValue(candidate.Row, out var reading))
            {
                targets.UnionWith(reading.Where(binder => binder.Type == EntityType.Effect));
            }

            foreach (var target in targets.ToArray()) targets.UnionWith(_replacements.GetValueOrDefault(target) ?? []);
            return [.. targets];
        }

        if (candidate.Row.Length > 0 && _binders.TryGetValue(candidate.Row, out var binders))
        {
            foreach (var binder in binders)
            {
                targets.Add(!Coefficients.Contains(type) && !Attributes.Contains(type) &&
                            binder.Type == EntityType.Effect && _entities.GetValueOrDefault(binder) is { } bound &&
                            _partOf(bound) is { } ability
                    ? new Key(EntityType.Ability, ability)
                    : binder);
            }

        }

        if (owner is { } primary)
        {
            if (targets.Count == 0) targets.Add(primary);
            if (targets.Contains(primary)) targets.UnionWith(_coTargets.GetValueOrDefault((source, primary)) ?? []);
        }


        foreach (var target in targets.ToArray()) targets.UnionWith(_replacements.GetValueOrDefault(target) ?? []);

        return [.. targets];
    }

    private IReadOnlyList<Key> Heroic(Entity? source, IReadOnlyList<Key> targets)
    {
        var heroes = source?.OwningHeroes ?? source?.Heroes;
        if (heroes is not { Count: > 0 }) return targets;

        return
        [
            .. targets.Where(target =>
                _entities.GetValueOrDefault(target)?.OwningHeroes is not { Count: > 0 } owning ||
                owning.Intersect(heroes, StringComparer.Ordinal).Any()),
        ];
    }

    private IReadOnlyList<Key> Fitting(ModifierType type, IReadOnlyList<Key> targets) =>
    [
        .. targets.Where(target => _entities.GetValueOrDefault(target) is not { } entity || type switch
        {
            ModifierType.MaxStacks or ModifierType.Stacks => entity.Type == EntityType.Effect && entity.MaxStacks is not null,
            ModifierType.CooldownReduction => entity.Cooldown is not null,
            ModifierType.DamageMultiplier or ModifierType.HealingMultiplier =>
                entity.Type == EntityType.Effect || entity.Coefficients is { Count: > 0 } ||
                entity.Placeholders?.Values.Any(placeholder => placeholder.Coefficient is not null) == true,
            _ => true,
        }),
    ];

    private static Requirement Empty() => new([], null, [], null, [], [], []);

    private static string Describe(Requirement? condition) =>
        condition is null
            ? string.Empty
            : $"{string.Join(",", condition.Effects)}|{condition.Stacks}|{string.Join(",", condition.TargetEffects)}|" +
              $"{condition.Chance}|{string.Join(",", condition.Talents)}|{string.Join(",", condition.CastBy)}|" +
              $"{string.Join(",", condition.Cooldowns)}|{string.Join(",", condition.States)}|{string.Join(",", condition.TargetStates)}";

    private const string Returned = RowSinks.SinkUse + "return:";
    private const string Supplied = "AssignTagSetByCallerMagnitude";

    private static bool Reads(string use) =>
        use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal) ||
        use.StartsWith(RowSinks.AttributeUse, StringComparison.Ordinal) ||
        (use.StartsWith(RowSinks.PropertyUse, StringComparison.Ordinal) &&
         !use.StartsWith(RowSinks.PropertyUse + "Modifiers[", StringComparison.Ordinal) &&
         !use.StartsWith(RowSinks.PropertyUse + "Executions[", StringComparison.Ordinal));

    private static string[] Readers(IReadOnlyList<string> uses)
    {
        var supplied = uses.Any(use => use.StartsWith(RowSinks.SinkUse + Supplied, StringComparison.Ordinal));

        var direct = uses
            .Where(Reads)
            .Where(use => !use.StartsWith(Returned, StringComparison.Ordinal))
            .Where(use => !(supplied && use.StartsWith(RowSinks.AttributeUse, StringComparison.Ordinal)))
            .ToArray();

        if (direct.Length > 0) return direct;

        var returned = uses.Where(use => use.StartsWith(Returned, StringComparison.Ordinal)).ToArray();

        return returned.Any(use => Gates(use).Any()) ? [.. returned.Where(use => Gates(use).Any())] : returned;
    }

    private static IEnumerable<string> Gates(string use)
    {
        var mark = use.IndexOf(RowSinks.GateMark, StringComparison.Ordinal);

        return mark < 0
            ? []
            : use[(mark + RowSinks.GateMark.Length)..]
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Where(gate => !gate.StartsWith(RowSinks.PerGate, StringComparison.Ordinal));
    }

    private static string? Per(ModifierCandidate candidate)
    {
        var heard = candidate.Uses
            .Where(use => use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal) && use.Contains(RowSinks.GateMark, StringComparison.Ordinal))
            .SelectMany(use => use[(use.IndexOf(RowSinks.GateMark, StringComparison.Ordinal) + RowSinks.GateMark.Length)..]
                .Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Where(gate => gate.StartsWith(RowSinks.PerGate, StringComparison.Ordinal))
            .Select(gate => gate[RowSinks.PerGate.Length..])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return heard is [var only] ? Dashed(only) : null;
    }

    private Requirement? Condition(Key source, ModifierCandidate candidate)
    {
        var readers = Readers(candidate.Uses);
        if (readers.Length == 0) return null;

        var states = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var reader in readers)
        {
            var open = Gates(reader).Where(gate => !Satisfied(source, gate)).ToArray();
            if (open.Length == 0) return null;

            states.UnionWith(open);
        }

        var resolved = Resolve(source, states);

        return resolved.IsEmpty ? null : resolved;
    }

    private bool Satisfied(Key source, string gate)
    {
        var held = _held.GetValueOrDefault(source) ?? [];

        if (gate.StartsWith(RowSinks.ChanceGate, StringComparison.Ordinal) ||
            gate.StartsWith(RowSinks.StacksGate, StringComparison.Ordinal))
        {
            return false;
        }

        if (gate.StartsWith(RowSinks.EventGate, StringComparison.Ordinal))
        {
            var tag = gate[RowSinks.EventGate.Length..];
            return !held.Contains(tag) && !_tagsBySource.ContainsKey(tag);
        }

        if (gate.StartsWith(EffectModifiers.TargetMark, StringComparison.Ordinal))
        {
            return _assetTagged.ContainsKey(gate[EffectModifiers.TargetMark.Length..]);
        }

        return held.Contains(gate) || _assetTagged.ContainsKey(gate) || _abilityTagged.ContainsKey(gate);
    }

    private Requirement Resolve(Key? source, IEnumerable<string> gates)
    {
        var effects = Keys();
        var targetEffects = Keys();
        var talents = Keys();
        var cast = Keys();
        var cooldowns = Keys();
        var sources = Keys();
        var states = new SortedSet<string>(StringComparer.Ordinal);
        var targetStates = new SortedSet<string>(StringComparer.Ordinal);
        var targetHealth = new SortedDictionary<string, double>(StringComparer.Ordinal);
        var health = new SortedDictionary<string, double>(StringComparer.Ordinal);
        double? chance = null;
        double? stacks = null;
        string? chanceRow = null;

        foreach (var gate in gates)
        {
            if (gate.StartsWith(RowSinks.ChanceGate, StringComparison.Ordinal))
            {
                var parts = gate[RowSinks.ChanceGate.Length..].Split('@');
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rolled)) chance = rolled;
                if (parts.Length > 1) chanceRow = parts[1];
                continue;
            }

            if (gate.StartsWith(RowSinks.StacksGate, StringComparison.Ordinal))
            {
                var parts = gate[RowSinks.StacksGate.Length..].Split('@', 2);
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var count)) stacks = count;
                if (parts.Length > 1) effects.UnionWith(_adders.GetValueOrDefault(parts[1]) ?? []);
                continue;
            }

            if (gate.StartsWith(RowSinks.EventGate, StringComparison.Ordinal))
            {
                var tag = gate[RowSinks.EventGate.Length..];

                if (source is { } own && _held.GetValueOrDefault(own)?.Contains(tag) == true) cast.Add(own);
                else cast.UnionWith(_tagsBySource.GetValueOrDefault(tag) ?? []);

                continue;
            }

            if (gate.StartsWith(EffectModifiers.TargetMark, StringComparison.Ordinal))
            {
                var tag = gate[EffectModifiers.TargetMark.Length..];
                if (_assetTagged.ContainsKey(tag)) continue;

                if (_adders.GetValueOrDefault(tag) is { Count: > 0 } marked) targetEffects.UnionWith(marked);
                else
                {
                    targetStates.Add(Dashed(tag));
                    if (_healthFractions.TryGetValue(tag[(tag.LastIndexOf('.') + 1)..], out var fraction)) targetHealth[Dashed(tag)] = fraction;
                }

                continue;
            }

            if (_assetTagged.ContainsKey(gate) || _abilityTagged.ContainsKey(gate)) continue;

            if (_adders.GetValueOrDefault(gate) is { Count: > 0 } adders) effects.UnionWith(adders);
            else if (_tagsBySource.GetValueOrDefault(gate) is { Count: > 0 } granting) talents.UnionWith(granting);
            else if (_cooldownTagged.GetValueOrDefault(gate) is { Count: > 0 } cooling) cooldowns.UnionWith(cooling);
            else if (_procAssetTags.GetValueOrDefault(gate) is { Count: > 0 } procs &&
                     procs.SelectMany(proc => _adders.GetValueOrDefault(proc) ?? []).ToArray() is { Length: > 0 } holders)
            {
                effects.UnionWith(holders);
            }
            else
            {
                states.Add(Dashed(gate));
                sources.UnionWith(_stateHolders.GetValueOrDefault(gate) ?? []);
                if (_healthFractions.TryGetValue(gate[(gate.LastIndexOf('.') + 1)..], out var fraction)) health[Dashed(gate)] = fraction;
            }
        }

        var added = source is { } owner && _sources.GetValueOrDefault(owner)?.AddedTags is { } tags
            ? tags.ToHashSet(StringComparer.Ordinal)
            : [];

        var produced = effects
            .Where(effect => _entities.GetValueOrDefault(effect) is { } held &&
                             (held.HeldTags ?? []).Concat(held.AddedTags ?? []).Any(added.Contains))
            .ToArray();

        return new Requirement([.. effects], stacks, [.. targetEffects], chance, [.. talents], [.. cast], [.. states])
        {
            ChanceRow = chanceRow,
            Cooldowns = [.. cooldowns],
            OwnEffects = produced,
            StateSources = [.. sources],
            TargetStates = [.. targetStates],
            TargetHealth = targetHealth,
            Health = health,
        };
    }

    private static bool Applies(ModifierClass classified)
    {
        if (classified.Value is not { } value) return true;

        if (ModifierTypes.IsMultiplier(classified.Type)) return value is not 1 and > 0;

        return classified.Type switch
        {
            ModifierType.CastTimeIncrease or ModifierType.CastTimeReduction or ModifierType.CooldownReduction
                or ModifierType.DurationIncrease or ModifierType.CostIncrease or ModifierType.CostReduction
                or ModifierType.AttributeIncrease or ModifierType.CritChanceIncrease => value != 0,
            ModifierType.ProcChance or ModifierType.CritChance => value is > 0 and <= 1,
            ModifierType.Duration => value is > 0 and < Endless,
            _ => true,
        };
    }

    private bool Changed(ModifierClass classified, ModifierCandidate candidate, Entity? source, IReadOnlyList<Key> targets)
    {
        if (classified.Value is not { } value) return false;

        foreach (var target in targets)
        {
            if (_entities.GetValueOrDefault(target) is not { } entity) continue;

            double? bare = classified.Type switch
            {
                ModifierType.Duration => entity.Duration,
                ModifierType.TargetCount => entity.MaxTargets,
                ModifierType.Radius => entity.Radius,
                ModifierType.Range => entity.Range,
                ModifierType.Charges => entity.Charges,
                ModifierType.Period => entity.Period,
                _ => null,
            };

            var own = (entity.Rows ?? new Dictionary<string, double>())
                .Where(row => !TalentSegments.Any(segment => row.Key.Contains(segment, StringComparison.OrdinalIgnoreCase)) &&
                              ModifierTypes.Classify(Candidate(row.Key, row.Value, null, null))?.Type == classified.Type)
                .Select(row => (double?)row.Value);

            foreach (var start in new[] { bare }.Concat(own).OfType<double>().Distinct())
            {
                if (start <= 0 || start == value) continue;

                var change = classified with { Value = Math.Abs(value - start) };
                var ratio = classified with { Type = ModifierType.DurationMultiplier, Value = value / start };

                if (Printed(change, candidate with { Value = change.Value }, source) ||
                    Printed(ratio, candidate with { Value = ratio.Value }, source))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Printed(ModifierClass classified, ModifierCandidate candidate, Entity? source) =>
        new[] { classified.Value, candidate.Value, candidate.Coefficient }.All(value => value is null) ||
        classified is { Type: ModifierType.ProcChance, Value: 1, Basis: ModifierBasis.Bytecode } ||
        Shown(classified, candidate, source).Count > 0;

    private static IReadOnlyList<int> Shown(ModifierClass classified, ModifierCandidate candidate, Entity? source)
    {
        var shown = new List<(double Number, bool? Percent)>();

        if (source?.Description is { Length: > 0 } text)
        {
            foreach (Match match in NumberPattern().Matches(Markup().Replace(text, string.Empty)))
            {
                shown.Add((double.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture),
                    match.Groups["percent"].Success));
            }
        }

        foreach (var placeholder in source?.Placeholders?.Values ?? [])
        {
            if (placeholder.Value is { } value)
            {
                shown.AddRange([(value, null), (value * 100, null), (Math.Abs(value - 1) * 100, null)]);
            }

            if (placeholder.Coefficient is { } coefficient) shown.Add((coefficient * 100, null));
        }

        var values = new[] { classified.Value, candidate.Value }.OfType<double>().ToArray();
        var readings = new List<(double Number, bool? Percent)>();

        if (candidate.Coefficient is { } scale) readings.AddRange([(scale, null), (scale * 100, null)]);

        foreach (var value in values)
        {
            if (ModifierTypes.IsMultiplier(classified.Type))
            {
                readings.AddRange([(Math.Abs(value - 1) * 100, true), (value * 100, true)]);
            }
            else if (classified.Type is ModifierType.ProcChance or ModifierType.CritChance
                     or ModifierType.CritChanceIncrease or ModifierType.Coefficient)
            {
                readings.Add((value * 100, true));
            }
            else if (classified.Type is ModifierType.AttributeIncrease or ModifierType.AttributeOverride)
            {
                readings.AddRange([(Math.Abs(value), false), (Math.Abs(value) * 100, true)]);
            }
            else
            {
                readings.Add((value, false));
            }
        }

        return
        [
            .. shown
                .Select((number, index) => (number, index))
                .Where(entry => readings.Any(reading =>
                    Math.Abs(entry.number.Number - reading.Number) < 0.0051 &&
                    (entry.number.Percent is not { } percent || reading.Percent is not { } wanted || percent == wanted)))
                .Select(entry => entry.index),
        ];
    }

    private IReadOnlyList<Typed> Own(Entity entity)
    {
        var gated = (entity.GatedModifiers ?? new Dictionary<string, IReadOnlyList<EffectModifier>>())
            .SelectMany(entry => entry.Value.Select(modifier => (entry.Key, modifier.Row)))
            .Where(entry => entry.Row is not null)
            .GroupBy(entry => entry.Row!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Key).ToArray(), StringComparer.OrdinalIgnoreCase);

        var granters = _grantedBy.GetValueOrDefault(KeyOf(entity));
        var typed = new List<Typed>();

        foreach (var modifier in entity.Modifiers ?? [])
        {
            if (modifier.Row is { } scoped && _scopedAway.Contains((KeyOf(entity), scoped))) continue;

            var tags = modifier.Row is { } row ? gated.GetValueOrDefault(row) ?? [] : [];

            var candidate = Candidate(modifier.Row ?? string.Empty, modifier.Value, modifier, entity.Description) with
            {
                Uses = [],
            };

            if (ModifierTypes.Classify(candidate with { Value = candidate.Value ?? 0 }) is not { } classified) continue;
            var banded = classified.Type == ModifierType.Duration && candidate.ValueLow is not null;

            if (!banded && classified.Type is not (ModifierType.AttributeIncrease or ModifierType.AttributeMultiplier
                or ModifierType.AttributeOverride)) continue;

            if (candidate.Value is null || !Applies(classified)) classified = classified with { Value = null };
            if (classified.Value is null && modifier.Attribute is { } filled && _filled.Contains((KeyOf(entity), filled))) continue;

            var condition = tags.Length == 0 ? null : Resolve(null, tags);

            if (entity.OngoingTags is { Count: > 0 } ongoing)
            {
                var required = Resolve(null, ongoing);
                condition = condition is null ? required : condition.Merged(required);
            }

            if (condition is { Talents.Count: > 0 } && granters is { Count: > 0 } &&
                condition.Talents.All(granters.Contains))
            {
                condition = condition with { Talents = [] };
            }

            var own = new Typed(candidate, classified, [], condition is { IsEmpty: false } ? condition : null);

            if (classified is { Type: ModifierType.AttributeMultiplier, Value: { } each } && entity.MaxStacks is > 1 &&
                PerUnitRow().IsMatch(HeroNames.LastSegment(candidate.Row)) &&
                Contradicted(entity, classified.Attribute, each) is { } sign)
            {
                own = own with
                {
                    Class = classified with { Value = 1 },
                    PerCount = sign * each,
                    Counts = [KeyOf(entity)],
                    Uncounted = 0,
                    CountsStacks = true,
                };
            }

            typed.Add(own);
        }

        return typed;
    }

    private static int? Contradicted(Entity entity, string? attribute, double factor)
    {
        if (entity.Description is not { Length: > 0 } description) return null;
        if (attribute is not null && description.Contains($"{{{attribute}}}", StringComparison.Ordinal)) return null;

        var text = MarkupTag().Replace(description, string.Empty);
        var increases = IncreaseWord().IsMatch(text);
        var decreases = DecreaseWord().IsMatch(text);

        if (increases == decreases) return null;
        if (increases && factor < 1) return 1;
        if (decreases && factor > 1) return -1;

        return null;
    }

    [GeneratedRegex(@"Per[A-Z][a-z]+$")]
    private static partial Regex PerUnitRow();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex MarkupTag();

    [GeneratedRegex(@"\b(increase[ds]?|more|additional|faster)\b", RegexOptions.IgnoreCase)]
    private static partial Regex IncreaseWord();

    [GeneratedRegex(@"\b(reduc\w*|decreas\w*|less|lower\w*|slow\w*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DecreaseWord();

    private ModifierCandidate Candidate(string row, double? value, EffectModifier? stated, string? text) => new()
    {
        Row = row,
        Value = stated is { Coefficient: not null } ? stated.Value : stated?.Value ?? value,
        ValueLow = stated?.ValueLow,
        ValueHigh = stated?.ValueHigh,
        Attribute = stated?.Attribute,
        Op = stated?.Op,
        Coefficient = stated?.Coefficient,
        ScalesOff = stated?.ScalesOff,
        Uses = row.Length > 0 && _uses.TryGetValue(row, out var uses) ? [.. uses] : [],
        Text = text,
    };

    private static readonly string[] HolderPaths =
    [
        RowSinks.PropertyUse + "Modifiers[",
        RowSinks.PropertyUse + "DurationMagnitude",
        RowSinks.PropertyUse + "Period",
        RowSinks.PropertyUse + "StackCountMax",
    ];

    private static bool HoldsOn(string use) => HolderPaths.Any(path => use.StartsWith(path, StringComparison.Ordinal));

    private IReadOnlySet<Key>? Held(ModifierCandidate candidate) =>
        candidate.Uses.Any(HoldsOn) &&
        !candidate.Uses.Any(use => use.StartsWith(RowSinks.PropertyUse + "Executions[", StringComparison.Ordinal)) &&
        _holders.TryGetValue(candidate.Row, out var holders)
            ? holders
            : null;

    private static int Compare(Key left, Key right) =>
        left.Type != right.Type ? left.Type.CompareTo(right.Type) : left.Id.CompareTo(right.Id);

    private static Key KeyOf(Entity entity) => new(entity.Type!.Value, entity.NetId!.Value);

    private Key? Owner(string row)
    {
        var scope = row;

        foreach (var segment in TalentSegments)
        {
            var at = scope.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
            if (at > 0) scope = scope[..at];
        }

        return Walk(scope) ?? (scope.Length < row.Length ? Walk(row) : null);
    }

    private Key? Walk(string scope)
    {
        while (true)
        {
            if (_owners.TryGetValue(scope, out var owner)) return owner;

            var dot = scope.LastIndexOf('.');
            if (dot <= 0) return null;

            scope = scope[..dot];
        }
    }

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Markup();

    [GeneratedRegex(@"<actor=(?<actor>[^>]+)>")]
    private static partial Regex ActorPattern();

    [GeneratedRegex(@"<class=(?<asset>[^>]+)>")]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"<(?<kind>assettag|grantedtag|tag)=(?<tag>[^>]+)>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"(?<number>\d+(?:\.\d+)?)(?<percent>\s?%)?")]
    private static partial Regex NumberPattern();
}
