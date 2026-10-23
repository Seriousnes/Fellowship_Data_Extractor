using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class ResourceGains(
    AbstractFileProvider provider,
    TypeMappings mappings,
    ConstantsTable constants,
    ResourcePools pools,
    AbilityOwners owners,
    HeroNames heroes,
    ResourceNames names,
    IReadOnlyList<TagRecord> tags,
    Func<string, UObject?> byName)
{
    public const string ConstantsRow = "ConstantsRow";
    public const string BytecodeLiteral = "BytecodeLiteral";
    public const string PoolFraction = "PoolFraction";
    public const string PoolCeiling = "PoolCeiling";
    public const string RowProduct = "RowProduct";
    public const string RuntimeFormula = "RuntimeFormula";
    public const string ModifierMagnitude = "ModifierMagnitude";

    private const string ModifiersField = "Modifiers";

    private const string AdditionalEffectsComponent = "AdditionalEffectsGameplayEffectComponent";

    private const string CopiesSpecField = "bOnApplicationCopyDataFromOriginalSpec";

    private const string OnApplicationField = "OnApplicationGameplayEffects";

    private const string EffectClassField = "EffectClass";
    private const string ExecutionsField = "Executions";
    private const string ScopedField = "CalculationModifiers";
    private const string AggregatorField = "TransientAggregatorIdentifier";
    private const string AttributeField = "Attribute";
    private const string AttributeNameField = "AttributeName";
    private const string ModifierOpField = "ModifierOp";
    private const string MagnitudeField = "ModifierMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string SetByCallerField = "SetByCallerMagnitude";
    private const string CustomField = "CustomMagnitude";
    private const string CalculationField = "CalculationClassMagnitude";
    private const string CalculationTagField = "BaseValueSetByCallerTag";
    private const string AttributeBasedField = "AttributeBasedMagnitude";
    private const string CoefficientField = "Coefficient";
    private const string BackingField = "BackingAttribute";
    private const string CapturedField = "AttributeToCapture";
    private const string DataTagField = "DataTag";
    private const string TagNameField = "TagName";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";

    private const string KindEnum = "EGameplayEffectMagnitudeCalculation";
    private const string ScalableFloat = "ScalableFloat";
    private const string AttributeBased = "AttributeBased";
    private const string SetByCaller = "SetByCaller";
    private const string CustomCalculationClass = "CustomCalculationClass";

    private const string AddsUp = "Additive";

    private const string Magnitudes = "CalculateBaseMagnitude";
    private const string Assignment = "AssignTagSetByCallerMagnitude";
    private const string Activation = "TryActivateAbilityByClassWithTargetData";
    private const string Spawning = "BeginSpawningActor";
    private const string Setting = "SetStructurePropertyByName";
    private const string EventMagnitude = "EventMagnitude";
    private const string ScalableRead = "GetScalableFloat";
    private const string SuppliedRead = "GetSetByCallerMagnitude";
    private const int SuppliedFallback = 2;
    private const string AttributeRead = "GetFloatAttribute";
    private const string Select = "SelectFloat";
    private const string TagTest = "HasMatchingGameplayTag";
    private const string Multiply = "Multiply_";
    private const string Sum = "Add_";
    private const string Subtract = "Subtract_";
    private const string Divide = "Divide_";
    private const string Convert = "Conv_";
    private const string Literal = "MakeLiteral";
    private const string Counted = "LessEqual_IntInt";
    private const string Stepped = "Add_IntInt";
    private const string DefaultPrefix = "Default__";
    private const string ClassSuffix = "_C";
    private const string EffectTable = "GameplayEffect";
    private const string AbilityTable = "GameplayAbility";
    private const string Equality = "EqualEqual_";
    private const string Either = "BooleanOR";
    private const string GrantingComponent = "AbilitiesGameplayEffectComponent";
    private const string GrantedField = "GrantAbilityConfigs";
    private const string GrantedAbilityField = "Ability";
    private const string CritToken = "crit";
    private const string Uncritical = "Crit";
    private const string TalentSegment = "Talent";
    private const string AtCritToken = "atcrit";
    private const string ChanceToken = "chance";
    private const string PerToken = "Per";
    private const string PoolRead = "Get";
    private const string Joiner = " and ";
    private const char OffsetMark = '@';

    private const int NamespaceSegments = 2;
    private const int EventWords = 2;
    private const int MaxDepth = 12;
    private const int MaxChain = 5;
    private const int MaxIterations = 64;

    private const double WholeUnit = 1;

    private const string ShareComment = "percentage of max";

    private const string Spent = "the magnitude is a spend, not a gain";
    private const string Never = "the roll that gates it can never succeed";
    private const string Unbound = "only an event no statement binds reaches it";
    private const string Withdrawn = "a row the designers commented retired";
    private const string Nothing = "the number it works out at is nothing";
    private const string Ceilinged = "every row it named is a term of the pool ceiling";
    private const string Framed = "a bytecode frame and no number";

    private static readonly string[] RetiredComments = ["not used", "unused", "deprecated"];

    private readonly Dictionary<string, IReadOnlyList<Vehicle>> _vehicles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UObject?> _defaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _runtimeCeilings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Scaler>> _scalers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _effectNames = Names(tags, EffectTable);
    private readonly HashSet<string> _abilityNames = Names(tags, AbilityTable);
    private readonly HashSet<string> _barren = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlySet<int>?> _live = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _frames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _granting = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _adds = Adding(mappings);
    private readonly IReadOnlySet<string> _kinds = Calculating(mappings);

    private readonly SortedDictionary<string, int> _dropped = new(StringComparer.Ordinal);

    private Dictionary<string, List<string>>? _byHero;

    public int Generations { get; private set; }

    public Tally Totals { get; private set; }

    public IReadOnlyDictionary<string, int> Dropped => _dropped;

    private void Drop(string reason) => _dropped[reason] = _dropped.GetValueOrDefault(reason) + 1;

    public readonly record struct Tally(
        int Documents, int Entries, int Amounts, int Shares, int Filled, int Amountless);

    private static string Adding(TypeMappings mappings)
    {
        if (EnumOrdinals.Members(mappings, GameplayEffects.OpEnum).Values.Contains(AddsUp, StringComparer.Ordinal))
        {
            return AddsUp;
        }

        throw new InvalidOperationException(
            $"{GameplayEffects.OpEnum} in the mappings declares no {AddsUp}, so a modifier that adds to a " +
            "resource pool cannot be told from one that scales it, and every generation would read as none. " +
            $"Read the enum out of mappings/{ExtractorOptions.MappingsPattern} with " +
            "'dotnet run --project src/FellowshipExtractor -- usmap'.");
    }

    private static IReadOnlySet<string> Calculating(TypeMappings mappings)
    {
        var declared = EnumOrdinals.Members(mappings, KindEnum).Values.ToHashSet(StringComparer.Ordinal);
        string[] wanted = [SetByCaller, AttributeBased, CustomCalculationClass];
        var missing = wanted.Where(member => !declared.Contains(member)).ToArray();

        if (missing.Length == 0) return declared;

        throw new InvalidOperationException(
            $"{KindEnum} in the mappings declares no {string.Join(" or ", missing)}, so a vehicle whose " +
            "magnitude the caller sets would read as a plain scalable float of nothing and every generation " +
            "walked through one would vanish with no error. " +
            $"Read the enum out of mappings/{ExtractorOptions.MappingsPattern} with " +
            "'dotnet run --project src/FellowshipExtractor -- usmap'.");
    }

    public IReadOnlyList<ResourceGain> Of(UObject? cdo, string? assetName) => Of(cdo, assetName, 0);

    private IReadOnlyList<ResourceGain> Of(UObject? cdo, string? assetName, int depth)
    {
        if (cdo is null) return [];

        var entries = new List<Entry>();

        Declared(cdo, entries);
        Walked(cdo, assetName, entries);
        Siblings(cdo, entries);

        var stated = Merged(entries);
        var granted = Granting(cdo, depth);

        return granted.Count == 0 ? stated : Ordered(Collapsed(stated.Concat(granted)));
    }

    private IReadOnlyList<ResourceGain> Granting(UObject cdo, int depth)
    {
        if (depth >= MaxChain) return [];

        var gains = new List<ResourceGain>();

        foreach (var ability in Grants(cdo))
        {
            if (!_granting.Add(ability)) continue;

            try
            {
                if (Default(ability) is not { } given) continue;

                var name = Leaf(ability, ClassSuffix);

                gains.AddRange(Of(given, name, depth + 1));
            }
            finally
            {
                _granting.Remove(ability);
            }
        }

        return gains;
    }

    private IEnumerable<string> Grants(UObject cdo)
    {
        var granted = new List<string>();

        Granted(cdo, granted);

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                foreach (var path in Referencing(statement, cdo, 0))
                {
                    if (Default(path) is { } effect) Granted(effect, granted);
                }
            }
        }

        return granted.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    internal static void Granted(UObject cdo, ICollection<string> granted)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (!export.ExportType.StartsWith(GrantingComponent, StringComparison.Ordinal)) continue;
            if (PropertyWalk.Get(export, GrantedField)?.GenericValue is not UScriptArray configs) continue;

            foreach (var entry in configs.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder config }) continue;
                if (PropertyWalk.GetString(config, GrantedAbilityField) is not { Length: > 0 } ability) continue;

                granted.Add(ability);
            }
        }
    }

    private IEnumerable<string> Referencing(KismetExpression? expression, UObject cdo, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        foreach (var parameter in Parameters(expression))
        {
            if (Referenced(parameter, cdo) is not { } path) continue;
            if (!_effectNames.Contains(Leaf(path, ClassSuffix))) continue;

            yield return path;
        }

        foreach (var child in Children(expression))
        {
            foreach (var path in Referencing(child, cdo, depth + 1)) yield return path;
        }
    }

    public IReadOnlyList<ResourceGain> Joined(
        IReadOnlyList<ResourceGain>? gains, IReadOnlyList<string>? owningHeroes)
    {
        if (gains is not { Count: > 0 }) return [];

        return
        [
            .. gains
                .Select(gain => Relative(gain, owningHeroes))
                .OfType<ResourceGain>()
                .Select(gain => gain.ShareOfMaximum is { } share ? Filled(gain, share, owningHeroes) : gain),
        ];
    }

    private ResourceGain? Relative(ResourceGain gain, IReadOnlyList<string>? owningHeroes)
    {
        if (Rows(gain.Source) is not { Count: > 0 } rows) return gain;

        var scalers = (owningHeroes ?? [])
            .Select(hero => Scaling(hero, gain.Resource))
            .Distinct()
            .ToArray();

        if (scalers is not [{ } scaler] || scaler.Value <= 0) return gain;
        if (Parent(scaler.Row) is not { } scope) return gain;

        if (!States(gain) && !gain.Unread)
        {
            var apart = rows
                .Where(row => !scaler.Reads.Contains(row, StringComparer.Ordinal))
                .Where(row => constants.Lookup(row).Record?.Value is not 0)
                .Where(row => Under(row, scope) && Depth(row) > Depth(scaler.Row))
                .ToArray();

            if (apart is [{ } named] && constants.Lookup(named).Record?.Value is { } value and > 0)
            {
                return Naming(
                    gain with
                    {
                        ShareOfMaximum = Math.Round(value / scaler.Value, 10),
                        SourceKind = PoolFraction,
                        Per = gain.Per ?? Trailing(Leaf(named)),
                    },
                    [scaler.Row, named]);
            }
        }

        if (States(gain)) return gain;

        var kept = rows
            .Where(row => !Under(row, scope) || Depth(row) > Depth(scaler.Row))
            .ToArray();

        if (kept.Length == rows.Count) return gain;

        if (kept.Length > 0) return Naming(gain, kept);

        Drop(Ceilinged);

        return null;
    }

    private static ResourceGain Naming(ResourceGain gain, IReadOnlyList<string> rows)
    {
        var named = rows.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        return gain with { Source = string.Join(Joiner, named), RowGroup = Qualified(named) };
    }

    public IEnumerable<string> RuntimeCeilings()
    {
        foreach (var channel in pools.Channels)
        {
            foreach (var hero in Owned().Keys)
            {
                if (Scaling(hero, channel) is not { } scaler) continue;

                yield return $"{hero} {ResourcePools.CeilingPrefix}{channel} is {scaler.Row} {scaler.Value} " +
                             "times what a calculation works out at runtime";
            }
        }
    }

    private Scaler? Scaling(string hero, string channel)
    {
        if (!_runtimeCeilings.ContainsKey(channel)) RuntimeCeiling(hero, channel);

        return _scalers.TryGetValue(channel, out var byHero) && byHero.TryGetValue(hero, out var scaler)
            ? scaler
            : null;
    }

    private static string? Parent(string row) =>
        row.LastIndexOf('.') is var at && at > 0 ? row[..at] : null;

    private static int Depth(string row) => row.Count(character => character == '.');

    private ResourceGain Filled(ResourceGain gain, double share, IReadOnlyList<string>? owningHeroes)
    {
        if (gain.Amount is not null) return gain;

        var ceilings = (owningHeroes ?? [])
            .Where(hero => !RuntimeCeiling(hero, gain.Resource))
            .Select(hero => pools.Ceiling(hero, gain.Resource))
            .Where(ceiling => ceiling is not null)
            .Distinct()
            .ToArray();

        if (ceilings is not [{ } ceiling]) return gain;

        return gain with { Amount = Math.Round(share * ceiling, 10) };
    }

    private bool RuntimeCeiling(string hero, string channel)
    {
        if (!_runtimeCeilings.TryGetValue(channel, out var overridden))
        {
            _runtimeCeilings[channel] = overridden = Overriding(channel);
        }

        return overridden.Contains(hero);
    }

    private HashSet<string> Overriding(string channel)
    {
        var overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scalers = new Dictionary<string, Scaler?>(StringComparer.OrdinalIgnoreCase);
        var ceiling = ResourcePools.CeilingPrefix + channel;

        foreach (var (hero, effects) in Owned())
        {
            foreach (var effect in effects)
            {
                if (!Overrides(effect, ceiling)) continue;

                overridden.Add(hero);
                Records(scalers, hero, Overridden(effect, ceiling));
            }
        }

        foreach (var profile in heroes.Profiles)
        {
            if (pools.Ceiling(profile.ShippedName, channel) is not null) continue;
            if (scalers.ContainsKey(profile.ShippedName)) continue;

            foreach (var effect in Under(profile.Codename))
            {
                if (!Overrides(effect, ceiling)) continue;

                overridden.Add(profile.ShippedName);
                Records(scalers, profile.ShippedName, Overridden(effect, ceiling));
            }
        }

        _scalers[channel] = scalers
            .Where(entry => entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Value, StringComparer.OrdinalIgnoreCase);

        return overridden;
    }

    private static void Records(IDictionary<string, Scaler?> scalers, string hero, Scaler? found)
    {
        if (found is not { } scaler) return;
        if (!scalers.TryGetValue(hero, out var held)) { scalers[hero] = scaler; return; }
        if (held is { } known && known.Row == scaler.Row && known.Value == scaler.Value) return;

        scalers[hero] = null;
    }

    private IEnumerable<string> Under(string codename)
    {
        var scope = $"GE_{codename}_";

        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) continue;
            if (!tag.Name.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) continue;

            yield return tag.Name;
        }
    }

    private Scaler? Overridden(string effect, string ceiling)
    {
        if (byName(effect) is not { } cdo) return null;
        if (PropertyWalk.Get(cdo, ModifiersField)?.GenericValue is not UScriptArray modifiers) return null;

        foreach (var entry in modifiers.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
            if (Name(modifier) != ceiling) continue;
            if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

            var kind = Kind(magnitude);

            var held = kind switch
            {
                CustomCalculationClass => PropertyWalk.GetStruct(magnitude, CustomField),
                AttributeBased => PropertyWalk.GetStruct(magnitude, AttributeBasedField),
                _ => null,
            };

            if (held is null) continue;
            if (PropertyWalk.GetStruct(held, CoefficientField) is not { } coefficient) continue;
            if (Row(coefficient) is not { } row) continue;
            if (constants.Lookup(row).Record?.Value is not { } declared) continue;

            var reads = kind == CustomCalculationClass && PropertyWalk.GetString(held, CalculationField) is { } named
                ? Calculated(named)
                : [];

            return new Scaler(row, Math.Round(Scale(coefficient) * declared, 10), reads);
        }

        return null;
    }

    private IReadOnlyList<string> Calculated(string calculationClass)
    {
        if (Default(calculationClass) is not { } cdo) return [];
        if (BareStateWalk.Compiled(cdo, Magnitudes) is not { ScriptBytecode: { Length: > 0 } bytecode }) return [];

        var locals = Locals(bytecode);
        var rows = new List<string>();

        foreach (var statement in bytecode) Rows(statement, cdo, locals, rows, 0);

        return [.. rows.Distinct(StringComparer.Ordinal)];
    }

    private Dictionary<string, List<string>> Owned()
    {
        if (_byHero is not null) return _byHero;

        _byHero = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var codename in owners.HeroesOf(tag.Name))
            {
                var hero = heroes.Shipped(codename) ?? codename;

                if (!_byHero.TryGetValue(hero, out var effects)) _byHero[hero] = effects = [];

                effects.Add(tag.Name);
            }
        }

        return _byHero;
    }

    private bool Overrides(string effect, string ceiling)
    {
        if (byName(effect) is not { } cdo) return false;
        if (PropertyWalk.Get(cdo, ModifiersField)?.GenericValue is not UScriptArray modifiers) return false;

        foreach (var entry in modifiers.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
            if (Name(modifier) != ceiling) continue;
            if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;
            if (Kind(magnitude) != ScalableFloat) return true;
            if (Operation(modifier) == _adds) continue;
            if (Stated(magnitude).Amount is null) return true;
        }

        return false;
    }

    private void Declared(UObject cdo, ICollection<Entry> entries)
    {
        var owner = Leaf(cdo.Owner?.Name ?? cdo.Name, ClassSuffix);

        Modified(cdo, owner, entries);
        Executed(cdo, owner, entries);
    }

    private void Executed(UObject cdo, string owner, ICollection<Entry> entries)
    {
        if (PropertyWalk.Get(cdo, ExecutionsField)?.GenericValue is not UScriptArray executions) return;

        foreach (var held in executions.Properties)
        {
            if (held?.GenericValue is not FScriptStruct { StructType: IPropertyHolder execution }) continue;
            if (PropertyWalk.Get(execution, ScopedField)?.GenericValue is not UScriptArray scoped) continue;

            foreach (var entry in scoped.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder scope }) continue;
                if (PropertyWalk.GetStruct(scope, AggregatorField) is not { } identifier) continue;
                if (PropertyWalk.GetString(identifier, TagNameField) is not { } tag) continue;

                var channel = Leaf(tag);

                if (!Channel(channel)) continue;
                if (PropertyWalk.GetStruct(scope, MagnitudeField) is not { } magnitude) continue;
                if (Kind(magnitude) != AttributeBased) continue;

                var (coefficient, captured) = Based(magnitude);

                if (coefficient is not { } share || share <= 0) continue;
                if (Pooled(captured) != channel) continue;

                var source = Row(PropertyWalk.GetStruct(magnitude, AttributeBasedField), CoefficientField)
                    ?? $"{owner}.{ExecutionsField}";

                entries.Add(Own(channel, null, share, source, PoolFraction));
            }
        }
    }

    private void Modified(UObject cdo, string owner, ICollection<Entry> entries)
    {
        if (PropertyWalk.Get(cdo, ModifiersField)?.GenericValue is not UScriptArray modifiers) return;

        foreach (var entry in modifiers.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
            if (Name(modifier) is not { } attribute) continue;
            if (!Channel(attribute)) continue;
            if (Operation(modifier) != _adds) continue;
            if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

            switch (Kind(magnitude))
            {
                case ScalableFloat:
                {
                    var (stated, row) = Stated(magnitude);
                    var amount = stated ?? Resolved(magnitude);

                    if (amount is not { } gained || gained <= 0) continue;

                    entries.Add(row is null
                        ? Own(attribute, gained, null, $"{owner}.{ModifiersField}", ModifierMagnitude)
                        : Own(attribute, gained, null, row, ConstantsRow));

                    break;
                }

                case AttributeBased:
                {
                    var (coefficient, captured) = Based(magnitude);

                    if (coefficient is not { } share || share <= 0) continue;
                    if (Pooled(captured) != attribute) continue;

                    var source = Row(PropertyWalk.GetStruct(magnitude, AttributeBasedField), CoefficientField)
                        ?? $"{owner}.{ModifiersField}";

                    entries.Add(Own(attribute, null, share, source, PoolFraction));
                    break;
                }
            }
        }
    }

    private void Siblings(UObject cdo, ICollection<Entry> entries)
    {
        if (entries.Count == 0) return;

        var named = entries
            .SelectMany(entry => Rows(entry.Source))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (named.Length == 0) return;

        var unread = new List<Entry>();

        foreach (var binding in CurveBindings.Collect(cdo, constants))
        {
            var row = binding.RowName;

            if (named.Contains(row, StringComparer.Ordinal)) continue;
            if (constants.Lookup(row).Record?.Value is not { } declared || declared <= 0) continue;
            if (Retired(row)) continue;
            if (Parent(row) is not { } scope || Parent(scope) is not { } family) continue;

            var twin = entries.FirstOrDefault(entry =>
                Rows(entry.Source).Any(one =>
                    Leaf(one).Equals(Leaf(row), StringComparison.Ordinal) &&
                    Parent(one) is { } held &&
                    Parent(held) == family));

            if (twin is null) continue;

            unread.Add(Own(twin.Channel, null, null, row, RuntimeFormula));
        }

        foreach (var entry in unread.DistinctBy(entry => entry.Source, StringComparer.Ordinal)) entries.Add(entry);
    }

    private static Entry Own(
        string channel, double? amount, double? share, string source, string kind) =>
        new(channel, amount, share, null, Trailing(Leaf(source)), Qualified([source]), source, kind, null,
            Keyed(Leaf(source)), Stem(Leaf(source)), Marked(Leaf(source)));

    private void Walked(UObject cdo, string? assetName, ICollection<Entry> entries)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var locals = Locals(bytecode);
            var walk = Walking(body, cdo, locals);
            var chances = new Dictionary<int, double?>();
            var gates = Rolled(body, cdo, bytecode);

            var repeats = Repeats(bytecode, walk);

            foreach (var statement in bytecode)
            {
                foreach (var call in Calls(statement, statement.StatementIndex, 0))
                {
                    if (Function(call.Library, call.Function, cdo) is not { } inner) continue;
                    if (Chance(inner.Body, call.Parameters, walk) is not { } chance) continue;

                    chances[call.Statement] = chance;
                }
            }

            foreach (var reached in Gains(body, cdo, null, 1))
            {
                Generations++;

                var read = Read(reached, body, cdo, walk, locals, chances, gates, repeats, assetName);

                foreach (var entry in read) entries.Add(entry);
            }
        }
    }

    private IEnumerable<Entry> Read(
        Generation reached,
        UStruct body,
        UObject cdo,
        BareStateWalk walk,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyDictionary<int, double?> chances,
        Gated gated,
        IReadOnlyList<Repeat> repeats,
        string? assetName)
    {
        if (reached.Sign < 0) { Drop(Spent); yield break; }

        var bare = Bare(reached.Magnitude, locals, 0);

        var chance = chances.TryGetValue(reached.Statement, out var supplied) ? supplied
            : gated.Rolls.TryGetValue(reached.Statement, out var rolled) ? rolled
            : null;

        if (chance is 0) { Drop(Never); yield break; }
        if (chance is 1) chance = null;

        if (reached.Value is null && Added(bare, walk) is { Count: > 1 } addends)
        {
            foreach (var addend in addends)
            {
                var apart = Read(
                    reached with { Magnitude = addend }, body, cdo, walk, locals,
                    chances, gated, repeats, assetName);

                foreach (var entry in apart) yield return entry;
            }

            yield break;
        }

        var rows = new List<string>(reached.Sources);
        var spelled = new List<string>(reached.Spelled);

        if (Handle(bare, cdo, locals, 0) is { } own) rows.Add(own);

        Rows(bare, cdo, locals, rows, 0);
        Spellings(bare, locals, spelled, 0);

        if (DynamicTextFloats.Name(bare) is { } holder) spelled.Add(holder);

        foreach (var input in reached.Inputs)
        {
            if (Handle(input, cdo, locals, 0) is { } held) rows.Add(held);

            Rows(input, cdo, locals, rows, 0);
            Spellings(input, locals, spelled, 0);

            if (DynamicTextFloats.Name(input) is { } property) spelled.Add(property);
        }

        if (rows.Any(Retired)) { Drop(Withdrawn); yield break; }

        var named = Contributing(rows);
        var traced = Traced(bare, locals, 0);
        var product = Multiplied(bare, cdo, locals);
        var claim = gated.Claims.GetValueOrDefault(reached.Statement);

        var unread = reached.Unread ||
                     Unread(bare, cdo, locals, [], 0) ||
                     reached.Inputs.Any(input => Unread(input, cdo, locals, [], 0));

        var selected = Selected(reached.Magnitude, cdo, locals);

        var offered = Offered(reached, reached.Magnitude, cdo, walk, locals)
            .Concat(reached.Inputs.SelectMany(input => Offered(reached, input, cdo, walk, locals)))
            .DistinctBy(one => one.Row, StringComparer.Ordinal)
            .ToArray();

        var basis = Excluding(Excluding(named, selected), offered);

        foreach (var (row, granted) in offered)
        {
            yield return Build(
                reached.Channel, null, granted, chance, [row], traced, spelled, product, claim,
                PoolFraction, unread, reached, body, cdo, assetName);
        }

        if (Sharing(reached, bare, walk, locals) is { } share)
        {
            if (share <= 0) { Drop(Nothing); yield break; }

            yield return Build(
                reached.Channel, null, share, chance, basis, traced, spelled, product, claim,
                share == 1 ? PoolCeiling : PoolFraction, unread, reached, body, cdo, assetName);

            foreach (var (row, scaler) in selected)
            {
                yield return Build(
                    reached.Channel, null, Math.Round(share * scaler, 10), chance, [.. basis, row], traced,
                    spelled, product, claim, PoolFraction, unread, reached, body, cdo, assetName);
            }

            yield break;
        }

        var read = (reached.Value ?? walk.Number(bare) ?? walk.Reading(bare) ?? Single(named, unread, bare, locals))
                   * (reached.Factor ?? 1);

        if (read is null && Portion(named, unread) is { } portion)
        {
            yield return Build(
                reached.Channel, null, portion, chance, named, traced, spelled, product, claim,
                PoolFraction, unread, reached, body, cdo, assetName);

            yield break;
        }

        if (read is { } value && value * reached.Sign <= 0) { Drop(Nothing); yield break; }

        var amount = read is { } gained ? Math.Round(gained * reached.Sign, 10) : (double?)null;
        var counted = false;

        if (amount is not null && named.Count == 0 && Repeated(repeats, reached.Statement) is { } repeat)
        {
            amount = Math.Round(amount.Value * repeat.Iterations, 10);
            Rows(repeat.Bound, cdo, locals, named, 0);
            named = Contributing(named);
            counted = named.Count > 0;
        }

        if (named.Count > 1 && !product && reached.Factor is null) amount = null;

        var kind = amount is null
            ? RuntimeFormula
            : counted || reached.Factor is not null
                ? RowProduct
                : named.Count switch { 0 => BytecodeLiteral, 1 => ConstantsRow, _ => RowProduct };

        if (amount is null)
        {
            var rated = new List<string>(reached.Rates);

            Rated(bare, cdo, locals, rated, 0);

            named = [.. named.Where(row => !rated.Contains(row, StringComparer.Ordinal))];
        }

        if (amount is not { } stated || selected.Count == 0)
        {
            yield return Build(
                reached.Channel, amount, null, chance, named, traced, spelled, product, claim, kind, unread,
                reached, body, cdo, assetName);

            yield break;
        }

        yield return Build(
            reached.Channel, stated, null, chance, basis, traced, spelled, product, claim,
            basis.Count switch { 0 => BytecodeLiteral, 1 => ConstantsRow, _ => RowProduct }, unread,
            reached, body, cdo, assetName);

        foreach (var (row, scaler) in selected)
        {
            yield return Build(
                reached.Channel, Math.Round(stated * scaler, 10), null, chance, [.. basis, row], traced,
                spelled, product, claim, RowProduct, unread, reached, body, cdo, assetName);
        }
    }

    private double? Single(
        IReadOnlyList<string> named,
        bool unread,
        KismetExpression? bare,
        IReadOnlyDictionary<string, KismetExpression> locals)
    {
        if (unread || named is not [{ } only]) return null;
        if (Captures(bare, locals, 0)) return null;
        if (constants.Lookup(only).Record?.Value is not { } declared) return null;

        return declared != 0 && Math.Abs(declared) < WholeUnit ? null : declared;
    }

    private bool Retired(string row) =>
        constants.Lookup(row).Record?.Comment is { } comment &&
        RetiredComments.Any(marker => comment.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private List<string> Contributing(IEnumerable<string> rows) =>
    [
        .. rows
            .Distinct(StringComparer.Ordinal)
            .Where(row => constants.Lookup(row).Record?.Value is not 0),
    ];

    private double? Portion(IReadOnlyList<string> named, bool unread)
    {
        if (unread || named is not [{ } only]) return null;
        if (constants.Lookup(only).Record is not { Comment: { } comment, Value: { } declared }) return null;
        if (!comment.Contains(ShareComment, StringComparison.OrdinalIgnoreCase)) return null;

        return declared is > 0 and <= WholeUnit ? declared : null;
    }

    private static IReadOnlyList<KismetExpression> Added(KismetExpression? bare, BareStateWalk walk)
    {
        if (bare is not EX_CallMath call) return [];
        if (!call.StackNode.Name.StartsWith(Sum, StringComparison.Ordinal)) return [];
        if (walk.Number(bare) is not null) return [];
        if (call.Parameters.ElementAtOrDefault(0) is not { } left) return [];
        if (call.Parameters.ElementAtOrDefault(1) is not { } right) return [];

        return [left, right];
    }

    private string? Declaring(string? named) =>
        named is null || constants.Lookup(named).Record is not null ||
        named.EndsWith($".{ModifiersField}", StringComparison.Ordinal)
            ? named
            : null;

    private Entry Build(
        string channel,
        double? amount,
        double? share,
        double? chance,
        IReadOnlyList<string> named,
        string? traced,
        IReadOnlyList<string> spelled,
        bool product,
        string? claim,
        string kind,
        bool unread,
        Generation reached,
        UStruct body,
        UObject cdo,
        string? assetName)
    {
        var leaf = named.Count > 0 ? Leaf(named[0]) : reached.Name ?? traced ?? string.Empty;

        var source = named.Count > 0
            ? string.Join(Joiner, named)
            : Declaring(reached.Name)
              ?? $"{Leaf(assetName ?? cdo.Owner?.Name ?? cdo.Name, ClassSuffix)}.{body.Name}" +
                 $"{OffsetMark}{reached.Statement}";

        return new Entry(
            channel,
            amount,
            share,
            chance,
            product || (amount is null && share is null && named.Count > 1)
                ? null
                : Per(Except(named, reached), traced, Except(spelled, reached)),
            Qualified(Rows(source)),
            source,
            kind,
            claim,
            Keyed(leaf),
            Stem(leaf),
            Marked(leaf),
            unread);
    }

    private IReadOnlyList<ResourceGain> Merged(IReadOnlyList<Entry> entries)
    {
        var distinct = entries.Distinct().ToArray();
        var basics = distinct.Where(entry => !entry.Critical).ToArray();
        var criticals = distinct.Where(entry => entry.Critical).ToList();
        var gains = new List<ResourceGain>();

        foreach (var basis in basics)
        {
            var matching = criticals.Where(entry => Pairs(entry, basis)).ToArray();

            if (matching is [{ } only])
            {
                criticals.Remove(only);
                gains.Add(Stated(basis, only.Amount, only.Source));
                continue;
            }

            gains.Add(Stated(basis, null, null));
        }

        gains.AddRange(criticals.Select(Paired));

        return Ordered(Collapsed(Pruned(gains)));
    }

    private ResourceGain Paired(Entry critical)
    {
        if (critical.Amount is not null && Shipped(critical.Source) is var (amount, row))
        {
            return Stated(
                critical with { Amount = amount, Share = null, Source = row, Kind = ConstantsRow },
                critical.Amount,
                critical.Source);
        }

        return Stated(critical with { Amount = null, Share = null }, critical.Amount, null);
    }

    private (double Amount, string Row)? Shipped(string source)
    {
        if (Rows(source) is not [{ } row]) return null;

        var at = row.LastIndexOf('.');
        var scope = at < 0 ? string.Empty : row[..(at + 1)];
        var leaf = at < 0 ? row : row[(at + 1)..];

        for (var found = leaf.IndexOf(Uncritical, StringComparison.Ordinal); found >= 0;
             found = leaf.IndexOf(Uncritical, found + 1, StringComparison.Ordinal))
        {
            var candidate = scope + leaf.Remove(found, Uncritical.Length);

            if (constants.Lookup(candidate).Record?.Value is { } value and > 0) return (value, candidate);
        }

        return null;
    }

    public static IEnumerable<ResourceGain> Collapsed(IEnumerable<ResourceGain> gains) =>
        gains.GroupBy(gain => (gain.Resource, gain.Source)).Select(Fullest);

    private static ResourceGain Fullest(IEnumerable<ResourceGain> group)
    {
        var ordered = group.OrderByDescending(gain => gain.Amount ?? gain.AmountOnCritical ?? 0).ToArray();
        var fullest = ordered[0];

        foreach (var gain in ordered.Skip(1))
        {
            fullest = fullest with
            {
                Per = fullest.Per ?? gain.Per,
                Chance = fullest.Chance ?? gain.Chance,
                RowGroup = fullest.RowGroup ?? gain.RowGroup,
            };
        }

        return fullest;
    }

    public IReadOnlyList<Entity> Shared(IReadOnlyList<Entity> entities)
    {
        var carried = new List<(IReadOnlyList<string> Rows, ResourceGain Gain)>();
        var stated = new List<(IReadOnlyList<string> Rows, ResourceGain Gain)>();
        var claimed = new Dictionary<string, List<ResourceGain>>(StringComparer.OrdinalIgnoreCase);

        var owners = entities
            .Where(entity => entity is { Type: EntityType.Ability, Name.Length: > 0 })
            .Select(entity => Leaf(entity.Name!, ClassSuffix))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        names.Learn(entities);

        foreach (var entity in entities)
        {
            var scopes = Scopes(entity);

            foreach (var gain in entity.Generates ?? [])
            {
                if (gain.ClaimedBy is { } asset && owners.Contains(asset))
                {
                    if (!claimed.TryGetValue(asset, out var held)) claimed[asset] = held = [];

                    held.Add(gain);
                    continue;
                }

                if (!States(gain)) continue;
                if (Rows(gain.Source) is not { Count: > 0 } rows) continue;

                if (rows.All(row => scopes.Any(scope => Under(row, scope)))) carried.Add((rows, gain));
                else stated.Add((rows, gain));
            }
        }

        var augmentations = entities
            .Where(entity => entity is { NetId: not null } &&
                             entity.Table is { } table &&
                             table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entity => entity.NetId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Generates);

        var widened = entities
            .Select(entity => Widened(entity, carried, stated, claimed, owners, augmentations))
            .ToArray();

        var agreed = Agreed(widened);

        names.Learn(agreed);

        var printed = agreed
            .Select(entity => entity.Generates is { Count: > 0 } gains
                ? entity with { Generates = Printing(gains, entity.OwningHeroes) }
                : entity)
            .ToArray();

        Totals = Tallied(printed);

        return printed;
    }

    private static IReadOnlyList<Entity> Agreed(IReadOnlyList<Entity> entities)
    {
        var triggers = Settled(entities, gain => gain.Per);
        var groups = Settled(entities, gain => gain.RowGroup);

        return
        [
            .. entities.Select(entity => entity.Generates is { Count: > 0 } gains
                ? entity with
                {
                    Generates =
                    [
                        .. gains.Select(gain => gain with
                        {
                            Per = gain.Per ?? triggers.GetValueOrDefault((gain.Resource, gain.Source)),
                            RowGroup = gain.RowGroup ?? groups.GetValueOrDefault((gain.Resource, gain.Source)),
                        }),
                    ],
                }
                : entity),
        ];
    }

    private static Dictionary<(string Resource, string? Source), string> Settled(
        IReadOnlyList<Entity> entities, Func<ResourceGain, string?> read)
    {
        var stated = new Dictionary<(string Resource, string? Source), HashSet<string>>();

        foreach (var gain in entities.SelectMany(entity => entity.Generates ?? []))
        {
            if (read(gain) is not { } value) continue;
            if (!stated.TryGetValue((gain.Resource, gain.Source), out var held))
            {
                stated[(gain.Resource, gain.Source)] = held = new(StringComparer.Ordinal);
            }

            held.Add(value);
        }

        return stated
            .Where(entry => entry.Value.Count == 1)
            .ToDictionary(entry => entry.Key, entry => entry.Value.First());
    }

    private static Tally Tallied(IReadOnlyList<Entity> entities)
    {
        var written = entities.Where(entity => entity.Generates is { Count: > 0 }).ToArray();
        var gains = written.SelectMany(entity => entity.Generates!).ToArray();

        return new Tally(
            written.Length,
            gains.Length,
            gains.Count(gain => gain.Amount is not null),
            gains.Count(gain => gain.ShareOfMaximum is not null),
            gains.Count(gain => gain is { Amount: not null, ShareOfMaximum: not null }),
            gains.Count(gain => !States(gain)));
    }

    private static bool States(ResourceGain gain) =>
        gain.Amount is not null || gain.AmountOnCritical is not null || gain.ShareOfMaximum is not null;

    private Entity Widened(
        Entity entity,
        IReadOnlyList<(IReadOnlyList<string> Rows, ResourceGain Gain)> carried,
        IReadOnlyList<(IReadOnlyList<string> Rows, ResourceGain Gain)> stated,
        IReadOnlyDictionary<string, List<ResourceGain>> claimed,
        IReadOnlySet<string> owners,
        IReadOnlyDictionary<int, IReadOnlyList<ResourceGain>?> augmentations)
    {
        var own = entity.Name is { Length: > 0 } named ? Leaf(named, ClassSuffix) : null;

        var kept = (entity.Generates ?? [])
            .Where(gain =>
                gain.ClaimedBy is not { } asset ||
                !owners.Contains(asset) ||
                asset.Equals(own, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var scopes = Scopes(entity);
        var granting = Talented(entity);
        var declares = entity.Declares ?? [];

        var borrowed = entity is { Type: EntityType.Sigil or EntityType.Aspect or EntityType.ProtectiveEnchant or EntityType.Enchant, Augmentation: { } power } &&
                       augmentations.TryGetValue(power, out var granted)
            ? granted ?? []
            : [];

        IReadOnlyList<ResourceGain> shared =
        [
            .. own is not null && claimed.TryGetValue(own, out var mine) ? mine : [],
            .. carried
                .Where(one => one.Rows.All(row => scopes.Any(scope => Under(row, scope))))
                .Where(one =>
                    one.Rows.All(declares.Contains) ||
                    one.Rows.All(row => granting.Any(scope => Under(row, scope))) ||
                    names.Prints(entity, one.Gain))
                .Select(one => one.Gain),
            .. stated
                .Where(one => one.Rows.All(row => scopes.Any(scope => Under(row, scope))))
                .Where(one => names.Names(entity, one.Gain, entity.OwningHeroes))
                .Select(one => one.Gain),
            .. borrowed,
        ];

        var written = Added(kept, shared) ?? kept;

        return entity with { Generates = Empty(written) };
    }

    private IReadOnlyList<ResourceGain> Printing(
        IReadOnlyList<ResourceGain> gains, IReadOnlyList<string>? owningHeroes) =>
    [
        .. gains.Select(gain => gain with
        {
            ResourceName = names.Of(gain, owningHeroes),
            Per = gain.Per ?? names.Triggering(gain),
        }),
    ];

    private static IReadOnlyList<ResourceGain>? Empty(IReadOnlyList<ResourceGain> gains) =>
        gains.Count == 0 ? null : gains;

    private IReadOnlyList<string> Talented(Entity entity)
    {
        var granted = new List<string>();

        foreach (var tag in entity.GrantedTags ?? [])
        {
            if (TuningNamespace.ForTalentTag(tag, constants) is { } talent) granted.Add(talent);
        }

        return [.. granted.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private IReadOnlyList<string> Scopes(Entity entity)
    {
        var scopes = new List<string>();

        if (entity.Tuning is { } tuning && tuning.Contains('.')) scopes.Add(tuning);

        foreach (var tag in entity.GrantedTags ?? [])
        {
            if (TuningNamespace.ForTalentTag(tag, constants) is { } talent) scopes.Add(talent);
        }

        if (entity.Name is { Length: > 0 } named &&
            HeroNames.LastSegment(named) is { Length: > 0 } leaf &&
            !heroes.Codenames.Contains(leaf, StringComparer.OrdinalIgnoreCase) &&
            constants.IsNamespace(leaf))
        {
            scopes.Add(leaf);
        }

        return [.. scopes.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static IReadOnlyList<ResourceGain>? Added(
        IReadOnlyList<ResourceGain>? stated, IReadOnlyList<ResourceGain> shared)
    {
        var known = (stated ?? [])
            .Select(gain => (gain.Resource, gain.Source))
            .ToHashSet();

        var missing = shared.Where(gain => known.Add((gain.Resource, gain.Source))).ToArray();

        return missing.Length == 0 ? null : Ordered((stated ?? []).Concat(missing));
    }

    private static IReadOnlyList<string> Rows(string? source) =>
        source is null || source.Contains(OffsetMark)
            ? []
            : [.. source.Split(Joiner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static bool Under(string row, string tuning) =>
        row.Length > tuning.Length &&
        row.StartsWith(tuning, StringComparison.OrdinalIgnoreCase) &&
        row[tuning.Length] == '.';

    public static IReadOnlyList<ResourceGain> Ordered(IEnumerable<ResourceGain> gains) =>
    [
        .. gains
            .OrderBy(gain => gain.Resource, StringComparer.Ordinal)
            .ThenBy(gain => gain.Source, StringComparer.Ordinal)
            .ThenBy(gain => gain.Amount ?? 0),
    ];

    private IEnumerable<ResourceGain> Pruned(IReadOnlyList<ResourceGain> gains)
    {
        foreach (var gain in gains)
        {
            if (States(gain) || gain.Source is not { } source || !source.Contains(OffsetMark))
            {
                yield return gain;
                continue;
            }

            _frames.Add(source);
            Drop(Framed);
        }
    }

    public IReadOnlyCollection<string> Frames => _frames;

    private static bool Pairs(Entry critical, Entry basis)
    {
        if (!critical.Channel.Equals(basis.Channel, StringComparison.Ordinal)) return false;
        if (critical.Group != basis.Group) return false;
        if (critical.Key.Equals(basis.Key, StringComparison.Ordinal)) return true;

        return critical.Stem.Equals(basis.Stem, StringComparison.Ordinal) &&
               (critical.Per is null || basis.Per is null ||
                critical.Per.Equals(basis.Per, StringComparison.Ordinal));
    }

    private static ResourceGain Stated(Entry entry, double? critical, string? criticalSource) =>
        new(entry.Channel)
        {
            Amount = entry.Amount,
            AmountOnCritical = critical,
            Per = entry.Per,
            Chance = entry.Chance,
            RowGroup = entry.Group,
            Source = entry.Source,
            SourceOnCritical = criticalSource,
            SourceKind = entry.Kind,
            ShareOfMaximum = entry.Share,
            ClaimedBy = entry.Claim,
            Unread = entry.Unread,
        };

    private IReadOnlyList<Generation> Gains(
        UStruct body, UObject cdo, IReadOnlyList<KismetExpression>? arguments, int depth)
    {
        if (depth > MaxChain || body.ScriptBytecode is not { Length: > 0 } bytecode) return [];

        var barren = $"{cdo.Owner?.Name}|{body.Name}|{MaxChain - depth}";
        if (_barren.Contains(barren)) return [];

        var parameters = Parameters(body);
        var locals = Locals(bytecode);
        var collected = new Collected();

        foreach (var statement in bytecode)
        {
            Collect(statement, cdo, statement.StatementIndex, 0, collected);
        }

        var vehicles = collected.Vehicles;
        var assigned = collected.Assigned;
        var calls = collected.Calls;
        var activated = collected.Activated;

        var reachable = Reaching(body, cdo, parameters, arguments, locals);
        var live = Live(body, cdo);

        var found = new List<Generation>();

        foreach (var applied in vehicles)
        {
            if (live is not null && !live.Contains(applied.Statement))
            {
                Drop(Unbound);
                continue;
            }

            if (applied.Vehicle.Share is { } share)
            {
                found.Add(new Generation(applied.Vehicle.Channel, null, share, 1, applied.Statement, false)
                {
                    Ceiling = applied.Vehicle.Ceiling,
                    Name = applied.Vehicle.Source,
                });

                continue;
            }

            if (applied.Vehicle.Tag is { } tag)
            {
                foreach (var supplied in assigned)
                {
                    if (!supplied.Tag.Equals(tag, StringComparison.Ordinal)) continue;

                    Carry(found, applied.Vehicle, supplied.Magnitude, supplied.Statement, body, cdo,
                        locals, parameters, arguments);
                }

                continue;
            }

            Carry(found, applied.Vehicle, Amount(applied.Parameters, parameters, locals), applied.Statement,
                body, cdo, locals, parameters, arguments);
        }

        foreach (var call in calls)
        {
            if (live is not null && !live.Contains(call.Statement)) continue;
            if (Function(call.Library, call.Function, cdo) is not { } inner) continue;
            if (inner.Body == body) continue;

            foreach (var reached in Gains(inner.Body, inner.Cdo, call.Parameters, depth + 1))
            {
                Carry(found, reached with { Statement = call.Statement }, body, cdo, locals, parameters, arguments);
            }
        }

        if (activated.Count > 0 && Assigned(bytecode, locals, parameters) is { } forwarded)
        {
            foreach (var ability in activated)
            {
                foreach (var reached in Forwarded(ability, depth + 1))
                {
                    found.Add(reached with
                    {
                        Magnitude = arguments?.ElementAtOrDefault(forwarded),
                        Value = null,
                        Supplied = arguments is not null,
                    });
                }
            }
        }

        foreach (var actor in collected.Spawned)
        {
            foreach (var (reached, property) in Spawned(actor, depth + 1))
            {
                if (!collected.Settings.TryGetValue(property, out var handed)) continue;

                Carry(found, reached with { Magnitude = handed, Value = null }, body, cdo,
                    locals, parameters, arguments);
            }
        }

        if (found.Count == 0) _barren.Add(barren);

        var stated = Supplying(found);

        return stated.Count > 1 ? Narrowed(stated, reachable) : stated;
    }

    private static IReadOnlyList<Generation> Supplying(IReadOnlyList<Generation> found)
    {
        var supplied = found
            .Where(one => one.Supplied)
            .Select(one => one.Channel)
            .ToHashSet(StringComparer.Ordinal);

        return supplied.Count == 0
            ? found
            : [.. found.Where(one => one.Supplied || !supplied.Contains(one.Channel))];
    }

    private void Carry(
        ICollection<Generation> found,
        Vehicle vehicle,
        KismetExpression? magnitude,
        int statement,
        UStruct body,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> parameters,
        IReadOnlyList<KismetExpression>? arguments)
    {
        var (sign, inner) = Signed(magnitude, locals);
        var folded = Negates(inner, locals, 0) ? -1 : 1;

        Carry(
            found,
            new Generation(vehicle.Channel, inner, null, vehicle.Sign * sign * folded, statement, false),
            body,
            cdo,
            locals,
            parameters,
            arguments);
    }

    private void Carry(
        ICollection<Generation> found,
        Generation reached,
        UStruct body,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> parameters,
        IReadOnlyList<KismetExpression>? arguments)
    {
        var (sign, magnitude) = Signed(reached.Magnitude, locals);
        var carried = reached with { Magnitude = magnitude, Sign = reached.Sign * sign };

        if (arguments is null)
        {
            found.Add(carried);
            return;
        }

        if (Traced(magnitude, locals, 0) is { } name && Index(parameters, name) is { } at)
        {
            found.Add(carried with
            {
                Magnitude = arguments.ElementAtOrDefault(at),
                Value = null,
                Supplied = true,
            });

            return;
        }

        var (factor, ceiling) = Fractional(magnitude, locals);

        if (ceiling is not null &&
            factor is not null &&
            Traced(factor, locals, 0) is { } shared &&
            Index(parameters, shared) is { } share)
        {
            found.Add(carried with
            {
                Magnitude = arguments.ElementAtOrDefault(share),
                Value = null,
                Supplied = true,
                Ceiling = ceiling,
            });

            return;
        }

        if (ceiling is null && Scaled(magnitude, body, cdo, locals, parameters) is
            ({ } scale, { } scaling, var scaled))
        {
            var scalers = new List<string>();

            Rows(scaling, cdo, locals, scalers, 0);

            found.Add(carried with
            {
                Magnitude = arguments.ElementAtOrDefault(scaled),
                Value = null,
                Supplied = true,
                Factor = scale,
                Sources = [.. carried.Sources, .. scalers],
                Spelled = [.. carried.Spelled, .. Spelling(scaling, locals)],
                Factored = [.. carried.Factored, .. scalers, .. Spelling(scaling, locals)],
                Unread = carried.Unread || Unread(scaling, cdo, locals, Handed(parameters, arguments), 0),
            });

            return;
        }

        var read = ceiling is null ? magnitude : factor;
        var resolved = new List<string>();
        var rated = new List<string>();

        Rows(read, cdo, locals, resolved, 0);
        Rated(read, cdo, locals, rated, 0);

        found.Add(carried with
        {
            Magnitude = null,
            Ceiling = ceiling,
            Rates = [.. carried.Rates, .. rated],
            Value = carried.Value
                ?? Walking(body, cdo, locals).Number(Bare(read, locals, 0))
                ?? (ceiling is not null && factor is null ? 1 : null),
            Sources = [.. carried.Sources, .. resolved],
            Spelled = [.. carried.Spelled, .. Spelling(read, locals)],
            Inputs = Supplies(magnitude, locals, parameters, arguments),
            Unread = carried.Unread || Unread(read, cdo, locals, Handed(parameters, arguments), 0),
        });
    }

    private (double? Scale, KismetExpression? Scaling, int Scaled) Scaled(
        KismetExpression? magnitude,
        UStruct body,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> parameters)
    {
        if (Bare(magnitude, locals, 0) is not EX_CallMath call) return (null, null, -1);
        if (!call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal)) return (null, null, -1);

        var walk = Walking(body, cdo, locals);

        foreach (var (scaling, supplied) in new[]
                 {
                     (call.Parameters.ElementAtOrDefault(0), call.Parameters.ElementAtOrDefault(1)),
                     (call.Parameters.ElementAtOrDefault(1), call.Parameters.ElementAtOrDefault(0)),
                 })
        {
            if (Traced(supplied, locals, 0) is not { } name) continue;
            if (Index(parameters, name) is not { } at) continue;
            if (walk.Number(Bare(scaling, locals, 0)) is not { } scale || scale == 0) continue;

            return (scale, scaling, at);
        }

        return (null, null, -1);
    }

    private static IReadOnlyList<string> Handed(
        IReadOnlyList<string> parameters, IReadOnlyList<KismetExpression>? arguments) =>
        arguments is null ? [] : parameters;

    private static IReadOnlyList<KismetExpression> Supplies(
        KismetExpression? magnitude,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> parameters,
        IReadOnlyList<KismetExpression> arguments)
    {
        var supplied = new List<KismetExpression>();

        Supplies(magnitude, locals, parameters, arguments, supplied, 0);

        return supplied;
    }

    private static void Supplies(
        KismetExpression? expression,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> parameters,
        IReadOnlyList<KismetExpression> arguments,
        ICollection<KismetExpression> supplied,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return;

        if (expression is EX_StructConst)
        {
            supplied.Add(expression);
            return;
        }

        if (Traced(expression, locals, 0) is { } name &&
            Index(parameters, name) is { } at &&
            arguments.ElementAtOrDefault(at) is { } argument)
        {
            supplied.Add(argument);
            return;
        }

        foreach (var child in Children(expression))
        {
            Supplies(child, locals, parameters, arguments, supplied, depth + 1);
        }

        if (Bound(expression, locals) is { } assigned)
        {
            Supplies(assigned, locals, parameters, arguments, supplied, depth + 1);
        }
    }

    private (KismetExpression? Factor, string? Channel) Fractional(
        KismetExpression? magnitude, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var bare = Bare(magnitude, locals, 0);

        if (Ceiling(bare) is { } whole) return (null, whole);
        if (bare is not EX_CallMath call) return (null, null);
        if (!call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal)) return (null, null);

        var left = call.Parameters.ElementAtOrDefault(0);
        var right = call.Parameters.ElementAtOrDefault(1);

        if (Ceiling(Bare(right, locals, 0)) is { } byRight) return (left, byRight);
        if (Ceiling(Bare(left, locals, 0)) is { } byLeft) return (right, byLeft);

        return (null, null);
    }

    private IReadOnlySet<int>? Live(UStruct body, UObject cdo)
    {
        var key = $"{cdo.Owner?.Name}|{body.Name}";

        if (!_live.TryGetValue(key, out var live)) _live[key] = live = BytecodeGates.Of(body, cdo)?.Reached;

        return live;
    }

    private IReadOnlySet<int>? Reaching(
        UStruct body,
        UObject cdo,
        IReadOnlyList<string> parameters,
        IReadOnlyList<KismetExpression>? arguments,
        IDictionary<string, KismetExpression> locals)
    {
        if (arguments is null) return null;

        var walk = new BareStateWalk(body, cdo, constants, null);
        var at = 0;

        foreach (var parameter in parameters)
        {
            if (arguments.ElementAtOrDefault(at) is { } supplied) walk.Holds(parameter, supplied);

            at++;
        }

        var reached = new HashSet<int>();

        foreach (var statement in walk.Statements())
        {
            reached.Add(statement.StatementIndex);

            if (Writes(statement) is { } assignment) locals[assignment.Name] = assignment.Value;
        }

        return reached;
    }

    private static IReadOnlyList<Generation> Narrowed(IReadOnlyList<Generation> found, IReadOnlySet<int>? reached)
    {
        if (reached is null) return found;

        var narrowed = found.Where(one => reached.Contains(one.Statement)).ToArray();

        if (narrowed.Length > 0) return narrowed;

        return found.Select(one => one.Channel).Distinct(StringComparer.Ordinal).Count() == 1 ? found : [];
    }

    private static int? Assigned(
        IReadOnlyList<KismetExpression> bytecode,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> parameters)
    {
        foreach (var statement in bytecode)
        {
            if (statement is not EX_Let let) continue;
            if (DynamicTextFloats.Name(let.Variable) != EventMagnitude) continue;
            if (Traced(let.Assignment, locals, 0) is not { } name) continue;

            var at = Index(parameters, name) ?? -1;
            if (at >= 0) return at;
        }

        return null;
    }

    private IReadOnlyList<(Generation Reached, string Property)> Spawned(string actorPath, int depth)
    {
        if (depth > MaxChain) return [];
        if (Default(actorPath) is not { } cdo) return [];

        var carried = new List<(Generation, string)>();

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var locals = Locals(bytecode);

            foreach (var reached in Gains(body, cdo, null, depth))
            {
                var spelled = new List<string>();

                if (Traced(reached.Magnitude, locals, 0) is { } traced) spelled.Add(traced);

                Spellings(reached.Magnitude, locals, spelled, 0);

                foreach (var property in spelled.Distinct(StringComparer.Ordinal))
                {
                    carried.Add((reached, property));
                }
            }
        }

        return carried;
    }

    private IReadOnlyList<Generation> Forwarded(string abilityPath, int depth)
    {
        if (depth > MaxChain) return [];
        if (Default(abilityPath) is not { } cdo) return [];

        var carried = new List<Generation>();

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

            foreach (var statement in bytecode) Remember(statement, locals);

            foreach (var reached in Gains(body, cdo, null, depth))
            {
                if (Traced(reached.Magnitude, locals, 0) != EventMagnitude) continue;

                carried.Add(reached);
            }
        }

        return carried;
    }

    private void Collect(
        KismetExpression? expression,
        UObject cdo,
        int statement,
        int depth,
        Collected into)
    {
        if (expression is null || depth > MaxDepth) return;

        switch (expression)
        {
            case EX_CallMath { StackNode.Name: Assignment } call
                when Tagged(call.Parameters.ElementAtOrDefault(1)) is { } tag:
                into.Assigned.Add(new Supplied(tag, call.Parameters.ElementAtOrDefault(2), statement));
                break;

            case EX_FinalFunction { StackNode.Name: Activation } activation
                when Path(activation.Parameters.FirstOrDefault()) is { } ability:
                into.Activated.Add(ability);
                break;

            case EX_FinalFunction { StackNode.Name: Spawning } spawn:
                foreach (var parameter in spawn.Parameters)
                {
                    if (Path(parameter) is { } actor) into.Spawned.Add(actor);
                }

                break;

            case EX_FinalFunction { StackNode.Name: Setting } set
                when set.Parameters.ElementAtOrDefault(1) is EX_NameConst named &&
                     set.Parameters.ElementAtOrDefault(2) is { } handed:
                into.Settings[named.Value.Text] = handed;
                break;

            case EX_Context context
                when Path(context.ObjectExpression) is { } library &&
                     library.Contains(DefaultPrefix, StringComparison.Ordinal) &&
                     Invoked(context.ContextExpression) is { } invoked:
                into.Calls.Add(new Called(library, invoked.Name, invoked.Parameters, statement));

                foreach (var parameter in invoked.Parameters)
                {
                    Collect(parameter, cdo, statement, depth + 1, into);
                }

                return;

            case EX_LocalVirtualFunction own:
                into.Calls.Add(new Called(null, own.VirtualFunctionName.Text, own.Parameters, statement));
                break;

            case EX_VirtualFunction own:
                into.Calls.Add(new Called(null, own.VirtualFunctionName.Text, own.Parameters, statement));
                break;
        }

        var supplied = Parameters(expression);

        foreach (var parameter in supplied)
        {
            if (Referenced(parameter, cdo) is not { } path) continue;
            if (!_effectNames.Contains(Leaf(path, ClassSuffix))) continue;

            foreach (var vehicle in Vehicles(path))
            {
                into.Vehicles.Add(new Applied(vehicle, supplied, statement));
            }
        }

        foreach (var child in Children(expression))
        {
            Collect(child, cdo, statement, depth + 1, into);
        }
    }

    private sealed class Collected
    {
        public List<Applied> Vehicles { get; } = [];

        public List<Supplied> Assigned { get; } = [];

        public List<Called> Calls { get; } = [];

        public List<string> Activated { get; } = [];

        public List<string> Spawned { get; } = [];

        public Dictionary<string, KismetExpression> Settings { get; } = new(StringComparer.Ordinal);
    }

    private static string? Referenced(KismetExpression? expression, UObject cdo)
    {
        if (Path(expression) is { } named) return named;
        if (DynamicTextFloats.Name(expression) is not { } property) return null;

        return PropertyWalk.GetString(cdo, property);
    }

    private IReadOnlyList<Vehicle> Vehicles(string classPath)
    {
        if (_vehicles.TryGetValue(classPath, out var cached)) return cached;
        if (Default(classPath) is not { } effect) return _vehicles[classPath] = [];

        _vehicles[classPath] = [];

        var carried = new List<Vehicle>();
        var owner = Leaf(effect.Owner?.Name ?? effect.Name, ClassSuffix);

        foreach (var forwarded in Forwarded(effect))
        {
            carried.AddRange(Vehicles(forwarded).Where(vehicle => vehicle.Tag is not null));
        }

        if (PropertyWalk.Get(effect, ModifiersField)?.GenericValue is UScriptArray modifiers)
        {
            foreach (var entry in modifiers.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
                if (Name(modifier) is not { } attribute) continue;
                if (!Channel(attribute)) continue;
                if (Operation(modifier) != _adds) continue;
                if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) continue;

                switch (Kind(magnitude))
                {
                    case SetByCaller or CustomCalculationClass:
                    {
                        if (SetByCallerTag(magnitude) is not { } tag) continue;

                        carried.Add(new Vehicle(attribute, tag, 1, null, null, null));
                        break;
                    }

                    case AttributeBased:
                    {
                        var (coefficient, captured) = Based(magnitude);

                        if (coefficient is not { } share || share <= 0) continue;
                        if (Pooled(captured) != attribute) continue;

                        var source = Row(PropertyWalk.GetStruct(magnitude, AttributeBasedField), CoefficientField)
                            ?? $"{owner}.{ModifiersField}";

                        carried.Add(new Vehicle(attribute, null, 1, share, attribute, source));
                        break;
                    }

                    default:
                    {
                        if (Resolved(magnitude) is not { } scale || scale == 0) continue;

                        carried.Add(new Vehicle(attribute, null, Math.Sign(scale), null, null, null));
                        break;
                    }
                }
            }
        }

        return _vehicles[classPath] = carried;
    }

    private static IEnumerable<string> Forwarded(UObject effect)
    {
        foreach (var export in effect.Owner?.GetExports() ?? [])
        {
            if (export.ExportType != AdditionalEffectsComponent) continue;
            if (PropertyWalk.Get(export, CopiesSpecField)?.GenericValue is not true) continue;
            if (PropertyWalk.Get(export, OnApplicationField)?.GenericValue is not UScriptArray applied) continue;

            foreach (var entry in applied.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder conditional }) continue;
                if (PropertyWalk.GetString(conditional, EffectClassField) is { Length: > 0 } path) yield return path;
            }
        }
    }

    private string Kind(IPropertyHolder magnitude)
    {
        var declared = PropertyWalk.GetString(magnitude, CalculationTypeField);

        if (declared is null || declared.Length == 0) return ScalableFloat;
        if (EnumOrdinals.Member(mappings, KindEnum, declared) is { } member) return member;

        throw new InvalidOperationException(
            $"A modifier writes {KindEnum} {declared}, and the mappings name none of " +
            $"({string.Join(", ", _kinds.Order(StringComparer.Ordinal))}), so the magnitude would read as a " +
            "plain scalable float and the export would state no generation for it. " +
            $"Read the enum out of mappings/{ExtractorOptions.MappingsPattern} with " +
            "'dotnet run --project src/FellowshipExtractor -- usmap'.");
    }

    private double? Resolved(IPropertyHolder magnitude)
    {
        if (PropertyWalk.GetStruct(magnitude, ScalableField) is not { } scalable) return null;

        var declared = Row(scalable) is { } row ? constants.Lookup(row).Record?.Value ?? 1 : 1;

        return Math.Round(Scale(scalable) * declared, 10);
    }

    private (double? Amount, string? Row) Stated(IPropertyHolder magnitude)
    {
        if (PropertyWalk.GetStruct(magnitude, ScalableField) is not { } scalable) return (null, null);
        if (Row(scalable) is not { } row) return (null, null);
        if (constants.Lookup(row).Record?.Value is not { } declared) return (null, null);

        return (Math.Round(Scale(scalable) * declared, 10), row);
    }

    private (double? Coefficient, string? Captured) Based(IPropertyHolder magnitude)
    {
        if (PropertyWalk.GetStruct(magnitude, AttributeBasedField) is not { } based) return (null, null);

        var coefficient = PropertyWalk.GetStruct(based, CoefficientField) is { } scalable
            ? Scale(scalable) * (Row(scalable) is { } row ? constants.Lookup(row).Record?.Value ?? 1 : 1)
            : (double?)null;

        var captured = PropertyWalk.GetStruct(based, BackingField) is { } backing &&
                       PropertyWalk.GetStruct(backing, CapturedField) is { } attribute
            ? PropertyWalk.GetString(attribute, AttributeNameField)
            : null;

        return (coefficient is { } scaled ? Math.Round(scaled, 10) : null, captured);
    }

    private string? Pooled(string? attribute)
    {
        if (attribute is null) return null;
        if (!attribute.StartsWith(ResourcePools.CeilingPrefix, StringComparison.Ordinal)) return null;

        var channel = attribute[ResourcePools.CeilingPrefix.Length..];

        return Channel(channel) ? channel : null;
    }

    private string? SetByCallerTag(IPropertyHolder magnitude)
    {
        if (PropertyWalk.GetStruct(magnitude, SetByCallerField) is { } supplied &&
            PropertyWalk.GetStruct(supplied, DataTagField) is { } tag &&
            Row(PropertyWalk.GetString(tag, TagNameField)) is { } named)
        {
            return named;
        }

        if (PropertyWalk.GetStruct(magnitude, CustomField) is not { } custom) return null;
        if (PropertyWalk.GetString(custom, CalculationField) is not { } calculation) return null;
        if (Default(calculation) is not { } calculating) return null;

        if (PropertyWalk.GetStruct(calculating, CalculationTagField) is { } held &&
            Row(PropertyWalk.GetString(held, TagNameField)) is { } own)
        {
            return own;
        }

        foreach (var property in PropertyWalk.Walk(calculating))
        {
            if (!property.Name.Equals(TagNameField, StringComparison.Ordinal)) continue;
            if (!property.Path.StartsWith(CalculationTagField, StringComparison.Ordinal)) continue;
            if (Row(PropertyWalk.AsString(property.Value)) is not { } written) continue;

            return written;
        }

        return null;
    }

    private static KismetExpression? Amount(
        IReadOnlyList<KismetExpression> applied,
        IReadOnlyList<string> parameters,
        IReadOnlyDictionary<string, KismetExpression> locals)
    {
        KismetExpression? only = null;

        foreach (var parameter in applied)
        {
            if (Traced(parameter, locals, 0) is not { } name) continue;
            if (!parameters.Contains(name, StringComparer.Ordinal)) continue;
            if (only is not null) return null;

            only = parameter;
        }

        return only;
    }

    private double? Chance(UStruct body, IReadOnlyList<KismetExpression> arguments, BareStateWalk walk)
    {
        var at = 0;

        foreach (var parameter in Parameters(body))
        {
            if (Numbers.Normalize(parameter).Contains(ChanceToken, StringComparison.Ordinal))
            {
                var supplied = arguments.ElementAtOrDefault(at);

                return walk.Number(supplied) ?? walk.Reading(supplied);
            }

            at++;
        }

        return null;
    }

    private Compiled? Function(string? libraryPath, string function, UObject own)
    {
        var cdo = libraryPath is null ? own : Default(libraryPath);

        if (cdo is null) return null;
        if (BareStateWalk.Compiled(cdo, function) is not { ScriptBytecode.Length: > 0 } body) return null;

        return new Compiled(body, cdo);
    }

    private UObject? Default(string objectPath)
    {
        if (_defaults.TryGetValue(objectPath, out var cached)) return cached;

        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return _defaults[objectPath] = null;
        }

        try
        {
            return _defaults[objectPath] = package.GetExports()
                .FirstOrDefault(export => export.Name.StartsWith(DefaultPrefix, StringComparison.Ordinal));
        }
        catch (Exception)
        {
            return _defaults[objectPath] = null;
        }
    }

    private double? Sharing(
        Generation reached,
        KismetExpression? bare,
        BareStateWalk walk,
        IReadOnlyDictionary<string, KismetExpression> locals)
    {
        if (reached.Ceiling is { } inner)
        {
            return inner.Equals(reached.Channel, StringComparison.Ordinal)
                ? reached.Value ?? walk.Number(bare)
                : null;
        }

        var (factor, ceiling) = Fractional(bare, locals);

        if (ceiling is null || !ceiling.Equals(reached.Channel, StringComparison.Ordinal)) return null;

        return factor is null ? 1 : walk.Number(Bare(factor, locals, 0));
    }

    private string? Ceiling(KismetExpression? expression)
    {
        if (Invoking(expression) is not { } called) return null;

        if (Calling(called) is { } invoked &&
            invoked.StartsWith(PoolRead, StringComparison.Ordinal) &&
            Pooled(invoked[PoolRead.Length..]) is { } read)
        {
            return read;
        }

        if (called is not EX_CallMath call) return null;
        if (!call.StackNode.Name.StartsWith(AttributeRead, StringComparison.Ordinal)) return null;
        if (call.Parameters.ElementAtOrDefault(1) is not EX_StructConst held) return null;
        if (held.Properties.OfType<EX_StringConst>().FirstOrDefault()?.Value is not { } named) return null;

        return Pooled(named);
    }

    private static string? Calling(KismetExpression? expression) => expression switch
    {
        EX_CallMath call => call.StackNode.Name,
        EX_LocalFinalFunction call => call.StackNode.Name,
        EX_FinalFunction call => call.StackNode.Name,
        EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
        EX_VirtualFunction call => call.VirtualFunctionName.Text,
        _ => null,
    };

    private bool Channel(string attribute) =>
        pools.Channels.Contains(attribute, StringComparer.Ordinal);

    private static IReadOnlyList<Repeat> Repeats(
        IReadOnlyList<KismetExpression> bytecode, BareStateWalk walk)
    {
        var repeats = new List<Repeat>();

        foreach (var statement in bytecode)
        {
            if (statement is not EX_Jump jump) continue;

            var start = (int)jump.CodeOffset;
            var end = statement.StatementIndex;

            if (start >= end) continue;

            var body = bytecode.Where(one => one.StatementIndex >= start && one.StatementIndex <= end).ToArray();

            if (Compared(body) is not ({ } counter, { } bound)) continue;
            if (!Steps(body, counter)) continue;
            if (walk.Number(bound) is not { } most) continue;
            if (Initial(bytecode, counter, start) is not { } least) continue;

            var iterations = Math.Floor(most) - least + 1;

            if (iterations is < 2 or > MaxIterations) continue;

            repeats.Add(new Repeat(start, end, iterations, bound));
        }

        return repeats;
    }

    private static (string? Counter, KismetExpression? Bound) Compared(IReadOnlyList<KismetExpression> body)
    {
        foreach (var statement in body)
        {
            if (Writes(statement) is not { } assignment) continue;
            if (assignment.Value is not EX_CallMath { StackNode.Name: Counted } call) continue;
            if (DynamicTextFloats.Name(call.Parameters.ElementAtOrDefault(0)) is not { } counter) continue;

            return (counter, call.Parameters.ElementAtOrDefault(1));
        }

        return (null, null);
    }

    private static bool Steps(IReadOnlyList<KismetExpression> body, string counter)
    {
        foreach (var statement in body)
        {
            if (Writes(statement) is not { } assignment) continue;
            if (assignment.Value is not EX_CallMath { StackNode.Name: Stepped } call) continue;
            if (DynamicTextFloats.Name(call.Parameters.ElementAtOrDefault(0)) != counter) continue;
            if (Constant(call.Parameters.ElementAtOrDefault(1)) is not 1) continue;

            return true;
        }

        return false;
    }

    private static double? Initial(IReadOnlyList<KismetExpression> bytecode, string counter, int start)
    {
        double? seeded = null;

        foreach (var statement in bytecode)
        {
            if (statement.StatementIndex >= start) break;
            if (Writes(statement) is not { } assignment) continue;
            if (!assignment.Name.Equals(counter, StringComparison.Ordinal)) continue;

            seeded = Constant(assignment.Value);
        }

        return seeded;
    }

    private static Repeat? Repeated(IReadOnlyList<Repeat> repeats, int statement)
    {
        foreach (var repeat in repeats)
        {
            if (statement >= repeat.Start && statement <= repeat.End) return repeat;
        }

        return null;
    }

    private void Rows(
        KismetExpression? expression,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ICollection<string> rows,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return;

        if (expression is EX_CallMath scalable &&
            scalable.StackNode.Name.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            if (Declared(Handle(scalable.Parameters.FirstOrDefault(), cdo, locals, depth)) is { } row)
            {
                rows.Add(row);
            }

            return;
        }

        foreach (var child in Children(expression)) Rows(child, cdo, locals, rows, depth + 1);

        if (Bound(expression, locals) is { } assigned) Rows(assigned, cdo, locals, rows, depth + 1);
    }

    private string? Declared(string? row) =>
        row is not null && constants.Lookup(row).Record is not null ? row : null;

    private bool Unread(
        KismetExpression? expression,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyList<string> supplied,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return false;

        if (expression is EX_CallMath scalable &&
            scalable.StackNode.Name.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            var handle = scalable.Parameters.FirstOrDefault();

            if (Traced(handle, locals, 0) is { } name && Index(supplied, name) is not null) return false;

            return Declared(Handle(handle, cdo, locals, depth)) is null;
        }

        foreach (var child in Children(expression))
        {
            if (Unread(child, cdo, locals, supplied, depth + 1)) return true;
        }

        return Bound(expression, locals) is { } assigned && Unread(assigned, cdo, locals, supplied, depth + 1);
    }

    private bool Captures(KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (expression is null || depth > MaxDepth) return false;
        if (Ceiling(expression) is not null) return true;

        if (Calling(Invoking(expression)) is { } invoked &&
            invoked.StartsWith(AttributeRead, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var child in Children(expression))
        {
            if (Captures(child, locals, depth + 1)) return true;
        }

        return Bound(expression, locals) is { } assigned && Captures(assigned, locals, depth + 1);
    }

    private IReadOnlyList<(string Row, double Value)> Selected(
        KismetExpression? expression, UObject cdo, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var found = new List<(string Row, double Value)>();

        Selected(expression, cdo, locals, found, 0);

        return [.. found.DistinctBy(one => one.Row, StringComparer.Ordinal)];
    }

    private void Selected(
        KismetExpression? expression,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ICollection<(string Row, double Value)> found,
        int depth)
    {
        if (depth > MaxDepth) return;
        if (Resolve(expression, locals, 0) is not EX_CallMath call) return;

        if (call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal))
        {
            foreach (var parameter in call.Parameters) Selected(parameter, cdo, locals, found, depth + 1);

            return;
        }

        if (!call.StackNode.Name.Equals(Select, StringComparison.Ordinal)) return;
        if (Constant(Resolve(call.Parameters.ElementAtOrDefault(1), locals, 0)) is not 1) return;
        if (Calling(Invoking(Resolve(call.Parameters.ElementAtOrDefault(2), locals, 0))) != TagTest) return;

        var gathered = new List<string>();

        Rows(call.Parameters.ElementAtOrDefault(0), cdo, locals, gathered, 0);

        if (gathered.Distinct(StringComparer.Ordinal).ToArray() is not [{ } only]) return;
        if (constants.Lookup(only).Record?.Value is not { } declared) return;
        if (declared <= 0 || declared == 1) return;

        found.Add((only, declared));
    }

    private IReadOnlyList<(string Row, double Value)> Offered(
        Generation reached,
        KismetExpression? expression,
        UObject cdo,
        BareStateWalk walk,
        IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var found = new List<(string Row, double Share)>();

        Offered(reached, expression, cdo, walk, locals, found, 0);

        return [.. found.DistinctBy(one => one.Row, StringComparer.Ordinal)];
    }

    private void Offered(
        Generation reached,
        KismetExpression? expression,
        UObject cdo,
        BareStateWalk walk,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ICollection<(string Row, double Value)> found,
        int depth)
    {
        if (depth > MaxDepth) return;
        if (Resolve(expression, locals, 0) is not { } held) return;

        if (held is EX_CallMath { StackNode.Name: Select } select &&
            Constant(Resolve(select.Parameters.ElementAtOrDefault(1), locals, 0)) is 0 &&
            Calling(Invoking(Resolve(select.Parameters.ElementAtOrDefault(2), locals, 0))) == TagTest &&
            select.Parameters.ElementAtOrDefault(0) is { } gated)
        {
            var (factor, ceiling) = Fractional(gated, locals);
            var gathered = new List<string>();

            Rows(gated, cdo, locals, gathered, 0);

            if (ceiling is { } pooled &&
                pooled.Equals(reached.Channel, StringComparison.Ordinal) &&
                walk.Number(Bare(factor, locals, 0)) is { } share and > 0 &&
                gathered.Distinct(StringComparer.Ordinal).ToArray() is [{ } only])
            {
                found.Add((only, share));

                return;
            }
        }

        foreach (var child in Children(held)) Offered(reached, child, cdo, walk, locals, found, depth + 1);
    }

    private static IReadOnlyList<string> Excluding(
        IReadOnlyList<string> named, IReadOnlyList<(string Row, double Value)> selected) =>
        selected.Count == 0
            ? named
            : [.. named.Where(row => !selected.Any(one => one.Row.Equals(row, StringComparison.Ordinal)))];

    private void Rated(
        KismetExpression? expression,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ICollection<string> rated,
        int depth)
    {
        if (depth > MaxDepth) return;
        if (Bare(expression, locals, 0) is not EX_CallMath call) return;
        if (!call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal)) return;
        if (call.Parameters.ElementAtOrDefault(0) is not { } left) return;
        if (call.Parameters.ElementAtOrDefault(1) is not { } right) return;

        foreach (var (factor, scaled) in new[] { (left, right), (right, left) })
        {
            if (!Reads(factor, locals) || !Combines(scaled, locals)) continue;

            var named = new List<string>();

            Rows(factor, cdo, locals, named, 0);

            if (named is not [{ } only]) continue;

            rated.Add(only);
            Rated(scaled, cdo, locals, rated, depth + 1);

            return;
        }
    }

    private static bool Reads(KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals) =>
        Calling(Bare(expression, locals, 0)) is { } called &&
        called.StartsWith(ScalableRead, StringComparison.Ordinal);

    private static bool Combines(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals) =>
        Bare(expression, locals, 0) is EX_CallMath call &&
        (call.StackNode.Name.StartsWith(Sum, StringComparison.Ordinal) ||
         call.StackNode.Name.StartsWith(Subtract, StringComparison.Ordinal));

    private bool Multiplied(
        KismetExpression? bare, UObject cdo, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        if (bare is not EX_CallMath call) return false;
        if (!call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal)) return false;

        var left = new List<string>();
        var right = new List<string>();

        Rows(call.Parameters.ElementAtOrDefault(0), cdo, locals, left, 0);
        Rows(call.Parameters.ElementAtOrDefault(1), cdo, locals, right, 0);

        return left.Count > 0 && right.Count > 0;
    }

    private static string? Handle(
        KismetExpression? expression,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        if (expression is EX_StructConst inline)
        {
            foreach (var member in inline.Properties)
            {
                if (member is not EX_StructConst handle) continue;
                if (handle.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text is not { } named) continue;

                return Row(named);
            }

            return null;
        }

        if (Bound(expression, locals) is { } assigned) return Handle(assigned, cdo, locals, depth + 1);
        if (DynamicTextFloats.Name(expression) is not { } property) return null;
        if (PropertyWalk.GetStruct(cdo, property) is not { } scalable) return null;

        return Row(scalable);
    }

    private static KismetExpression? Resolve(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_Cast cast:
                return Resolve(cast.Target, locals, depth + 1);

            case EX_CallMath call
                when call.StackNode.Name.StartsWith(Convert, StringComparison.Ordinal) ||
                     call.StackNode.Name.StartsWith(Literal, StringComparison.Ordinal):
                return Resolve(call.Parameters.FirstOrDefault(), locals, depth + 1);
        }

        return Bound(expression, locals) is { } assigned ? Resolve(assigned, locals, depth + 1) : expression;
    }

    private static KismetExpression? Bare(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_Cast cast:
                return Bare(cast.Target, locals, depth + 1);

            case EX_CallMath { StackNode.Name: Select } select:
            {
                var ungated = Bare(select.Parameters.ElementAtOrDefault(1), locals, depth + 1);

                return Constant(ungated) is 0
                    ? Bare(select.Parameters.ElementAtOrDefault(0), locals, depth + 1)
                    : ungated;
            }

            case EX_CallMath call
                when call.StackNode.Name.StartsWith(Convert, StringComparison.Ordinal) ||
                     call.StackNode.Name.StartsWith(Literal, StringComparison.Ordinal):
                return Bare(call.Parameters.FirstOrDefault(), locals, depth + 1);
        }

        return Bound(expression, locals) is { } assigned ? Bare(assigned, locals, depth + 1) : expression;
    }

    private static (double Sign, KismetExpression? Magnitude) Signed(
        KismetExpression? magnitude, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        if (Bare(magnitude, locals, 0) is not EX_CallMath call) return (1, magnitude);

        var left = Constant(Bare(call.Parameters.ElementAtOrDefault(0), locals, 0));
        var right = Constant(Bare(call.Parameters.ElementAtOrDefault(1), locals, 0));

        if (call.StackNode.Name.StartsWith(Subtract, StringComparison.Ordinal))
        {
            return left is 0 ? (-1, call.Parameters.ElementAtOrDefault(1)) : (1, magnitude);
        }

        if (!call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal)) return (1, magnitude);

        if (left is < 0) return (-1, call.Parameters.ElementAtOrDefault(1));
        if (right is < 0) return (-1, call.Parameters.ElementAtOrDefault(0));

        return (1, magnitude);
    }

    private static bool Negates(
        KismetExpression? magnitude, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (depth > MaxDepth) return false;
        if (Bare(magnitude, locals, depth) is not EX_CallMath call) return false;

        var left = call.Parameters.ElementAtOrDefault(0);
        var right = call.Parameters.ElementAtOrDefault(1);

        if (call.StackNode.Name.StartsWith(Subtract, StringComparison.Ordinal))
        {
            return Constant(Bare(left, locals, depth)) is 0 && !Negates(right, locals, depth + 1);
        }

        if (!call.StackNode.Name.StartsWith(Multiply, StringComparison.Ordinal) &&
            !call.StackNode.Name.StartsWith(Divide, StringComparison.Ordinal))
        {
            return false;
        }

        var negated = Constant(Bare(left, locals, depth)) is < 0 || Negates(left, locals, depth + 1);

        return negated ^ (Constant(Bare(right, locals, depth)) is < 0 || Negates(right, locals, depth + 1));
    }

    private static double? Constant(KismetExpression? expression) => expression switch
    {
        EX_FloatConst value => value.Value,
        EX_DoubleConst value => value.Value,
        EX_IntConst value => value.Value,
        _ => null,
    };

    private static string? Traced(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;
        if (expression is EX_Cast cast) return Traced(cast.Target, locals, depth + 1);
        if (DynamicTextFloats.Name(expression) is not { } name) return null;

        return locals.TryGetValue(name, out var assigned) ? Traced(assigned, locals, depth + 1) ?? name : name;
    }

    private static KismetExpression? Bound(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals) =>
        DynamicTextFloats.Name(expression) is { } name && locals.TryGetValue(name, out var assigned)
            ? assigned
            : null;

    private static (string Name, KismetExpression Value)? Writes(KismetExpression statement)
    {
        var assignment = statement switch
        {
            EX_Let let => (Variable: let.Variable, Value: let.Assignment),
            EX_LetObj let => (Variable: let.Variable, Value: let.Assignment),
            EX_LetBool let => (Variable: let.Variable, Value: let.Assignment),
            _ => default,
        };

        if (assignment.Variable is null || assignment.Value is null) return null;
        if (assignment.Variable is EX_InstanceVariable) return null;
        if (DynamicTextFloats.Name(assignment.Variable) is not { } name) return null;

        return (name, assignment.Value);
    }

    private static void Remember(KismetExpression statement, IDictionary<string, KismetExpression> locals)
    {
        if (Built(statement) is { } built) locals.TryAdd(built.Name, built.Value);
        if (Writes(statement) is not { } assignment) return;

        locals.TryAdd(assignment.Name, Shipped(assignment.Value) ?? assignment.Value);
    }

    private static (string Name, KismetExpression Value)? Built(KismetExpression statement)
    {
        if (statement is not EX_Let { Variable: EX_StructMemberContext member, Assignment: { } value }) return null;
        if (DynamicTextFloats.Name(member) != ValueField) return null;
        if (DynamicTextFloats.Name(member.StructExpression) is not { } held) return null;

        return (held, value);
    }

    private static KismetExpression? Shipped(KismetExpression? assigned) =>
        assigned is EX_CallMath call && call.StackNode.Name.StartsWith(SuppliedRead, StringComparison.Ordinal)
            ? call.Parameters.ElementAtOrDefault(SuppliedFallback)
            : null;

    private static Dictionary<string, KismetExpression> Locals(IReadOnlyList<KismetExpression> bytecode)
    {
        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

        foreach (var statement in bytecode) Remember(statement, locals);

        return locals;
    }

    private BareStateWalk Walking(
        UStruct body, UObject cdo, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var walk = new BareStateWalk(body, cdo, constants, null);

        foreach (var (name, value) in locals) walk.Holds(name, value);

        return walk;
    }

    private Gated Rolled(UStruct body, UObject cdo, IReadOnlyList<KismetExpression> bytecode)
    {
        var rolled = new Dictionary<int, double>();
        var claimed = new Dictionary<int, string>();

        if (BytecodeGates.Of(body, cdo) is not { } gates) return new Gated(rolled, claimed);

        var branches = gates.Branches.ToDictionary(branch => branch.At, branch => branch.Condition);
        if (branches.Count == 0) return new Gated(rolled, claimed);

        var running = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

        for (var at = 0; at < bytecode.Count; at++)
        {
            if (branches.TryGetValue(at, out var condition))
            {
                var chance = Rolls(condition, body, cdo, running);
                var claim = Claims(condition, running);

                if (chance is not null || claim is not null)
                {
                    foreach (var statement in gates.Only(at))
                    {
                        if (chance is { } rolls) rolled[statement] = rolls;
                        if (claim is { } named) claimed[statement] = named;
                    }
                }
            }

            if (Writes(bytecode[at]) is { } assignment) running[assignment.Name] = assignment.Value;
        }

        return new Gated(rolled, claimed);
    }

    private string? Claims(KismetExpression? condition, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var named = new List<string>();

        Claimed(condition, locals, named, 0);

        if (named.Count == 0) return null;

        var shortest = named.OrderBy(name => name.Length).First();

        return named.All(name => name.StartsWith(shortest, StringComparison.OrdinalIgnoreCase))
            ? shortest
            : null;
    }

    private void Claimed(
        KismetExpression? condition,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ICollection<string> named,
        int depth)
    {
        if (depth > MaxDepth) return;
        if (Invoking(Bare(condition, locals, 0)) is not { } called) return;
        if (Calling(called) is not { } invoked) return;

        if (invoked.StartsWith(Either, StringComparison.Ordinal))
        {
            foreach (var operand in Parameters(called)) Claimed(operand, locals, named, depth + 1);

            return;
        }

        if (!invoked.StartsWith(Equality, StringComparison.Ordinal)) return;

        foreach (var argument in Parameters(called))
        {
            if (Path(Bare(argument, locals, 0)) is not { } path) continue;

            var leaf = Leaf(path, ClassSuffix);

            if (_abilityNames.Contains(leaf)) named.Add(leaf);
        }
    }

    private double? Rolls(
        KismetExpression? condition,
        UStruct body,
        UObject cdo,
        IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var called = Invoking(Bare(condition, locals, 0));

        foreach (var argument in called is null ? [] : Parameters(called))
        {
            if (!Chanced(argument, cdo, locals)) continue;

            var walk = Walking(body, cdo, locals);

            if ((walk.Number(argument) ?? walk.Reading(argument)) is not { } read) continue;

            return read;
        }

        return null;
    }

    private bool Chanced(
        KismetExpression? argument, UObject cdo, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var spelled = new List<string>();

        Rows(argument, cdo, locals, spelled, 0);
        Spellings(argument, locals, spelled, 0);

        return spelled.Any(name =>
            Numbers.Normalize(Leaf(name)).Contains(ChanceToken, StringComparison.Ordinal));
    }

    private static KismetExpression? Invoking(KismetExpression? expression) => expression switch
    {
        EX_Context context => Invoking(context.ContextExpression),
        EX_CallMath or EX_LocalFinalFunction or EX_FinalFunction
            or EX_LocalVirtualFunction or EX_VirtualFunction => expression,
        _ => null,
    };

    private static void Spellings(
        KismetExpression? expression,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ICollection<string> spelled,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return;

        if (expression is EX_CallMath scalable &&
            scalable.StackNode.Name.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            if (DynamicTextFloats.Name(scalable.Parameters.FirstOrDefault()) is { } property) spelled.Add(property);

            return;
        }

        foreach (var child in Children(expression)) Spellings(child, locals, spelled, depth + 1);

        if (Bound(expression, locals) is { } assigned) Spellings(assigned, locals, spelled, depth + 1);
    }

    private static IEnumerable<Called> Calls(KismetExpression? expression, int statement, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_Context context &&
            Path(context.ObjectExpression) is { } library &&
            library.Contains(DefaultPrefix, StringComparison.Ordinal) &&
            Invoked(context.ContextExpression) is { } invoked)
        {
            yield return new Called(library, invoked.Name, invoked.Parameters, statement);
        }

        foreach (var child in Children(expression))
        {
            foreach (var call in Calls(child, statement, depth + 1)) yield return call;
        }
    }

    private static (string Name, IReadOnlyList<KismetExpression> Parameters)? Invoked(
        KismetExpression? expression) => expression switch
    {
        EX_LocalVirtualFunction call => (call.VirtualFunctionName.Text, call.Parameters),
        EX_VirtualFunction call => (call.VirtualFunctionName.Text, call.Parameters),
        _ => null,
    };

    private static string? Path(KismetExpression? expression) =>
        expression is EX_ObjectConst named ? PropertyWalk.AsString(named.Value) : null;

    private static string? Tagged(KismetExpression? expression) =>
        expression is EX_StructConst inline
            ? inline.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text
            : null;

    private static HashSet<string> Names(IReadOnlyList<TagRecord> tags, string table)
    {
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (tag.Table.Equals(table, StringComparison.OrdinalIgnoreCase)) named.Add(tag.Name);
        }

        return named;
    }

    private static int? Index(IReadOnlyList<string> parameters, string name)
    {
        for (var at = 0; at < parameters.Count; at++)
        {
            if (parameters[at].Equals(name, StringComparison.Ordinal)) return at;
        }

        return null;
    }

    private static IReadOnlyList<string> Parameters(UStruct body)
    {
        var parameters = new List<string>();

        foreach (var child in body.ChildProperties ?? [])
        {
            if (child is not FProperty parameter) continue;
            if (!parameter.PropertyFlags.HasFlag(EPropertyFlags.Parm)) continue;
            if (parameter.PropertyFlags.HasFlag(EPropertyFlags.ReturnParm)) continue;

            parameters.Add(parameter.Name.Text);
        }

        return parameters;
    }

    private static IReadOnlyList<KismetExpression> Parameters(KismetExpression expression) => expression switch
    {
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        _ => [],
    };

    internal static IEnumerable<KismetExpression?> Children(KismetExpression expression) => expression switch
    {
        EX_Let let => [let.Variable, let.Assignment],
        EX_LetObj let => [let.Variable, let.Assignment],
        EX_LetBool let => [let.Variable, let.Assignment],
        EX_Context context => [context.ObjectExpression, context.ContextExpression],
        EX_Cast cast => [cast.Target],
        EX_Return returned => [returned.ReturnExpression],
        EX_JumpIfNot jump => [jump.BooleanExpression],
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        EX_StructConst constant => constant.Properties,
        EX_StructMemberContext member => [member.StructExpression],
        _ => [],
    };

    private static string? Name(IPropertyHolder modifier) =>
        PropertyWalk.GetStruct(modifier, AttributeField) is { } attribute
            ? PropertyWalk.GetString(attribute, AttributeNameField)
            : null;

    private string? Operation(IPropertyHolder modifier) =>
        EnumOrdinals.Member(mappings, GameplayEffects.OpEnum, PropertyWalk.GetString(modifier, ModifierOpField));

    private static double Scale(IPropertyHolder scalable) =>
        PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;

    private static string? Row(IPropertyHolder scalable) =>
        PropertyWalk.GetStruct(scalable, CurveField) is { } curve
            ? Row(PropertyWalk.GetString(curve, RowField))
            : null;

    private static string? Row(IPropertyHolder? holder, string field) =>
        holder is not null && PropertyWalk.GetStruct(holder, field) is { } scalable ? Row(scalable) : null;

    private static string? Row(string? row) =>
        row is null || row.Length == 0 || row.Equals(NoRow, StringComparison.Ordinal) ? null : row;

    private static string Leaf(string name) => name.Split('.')[^1];

    private static string Leaf(string path, string suffix)
    {
        var named = path.Contains('/') ? AbilityPresentationIndex.AssetName(path) : path;
        var name = named.LastIndexOf('/') is var slash and >= 0 ? named[(slash + 1)..] : named;

        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    private static string? Qualified(IReadOnlyList<string> rows)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var segments = row.Split('.');
            var talent = Array.IndexOf(segments, TalentSegment);
            var from = talent > 0 ? Math.Min(talent, NamespaceSegments) : NamespaceSegments;

            if (segments.Length - 1 <= from) continue;

            found.Add(string.Join('.', segments[from..^1]));
        }

        return found.Count == 1 ? found.Min : null;
    }

    private static IReadOnlyList<string> Except(IReadOnlyList<string> names, Generation reached) =>
        reached.Factored.Count == 0
            ? names
            : [.. names.Where(name => !reached.Factored.Contains(name, StringComparer.Ordinal))];

    private static string? Per(IReadOnlyList<string> rows, string? member, IReadOnlyList<string> spelled)
    {
        var found = Triggers(rows.Select(Leaf).Concat(member is null ? [] : (string[])[member]));

        if (found.Count > 0) return found.Count == 1 ? found.Min : null;

        var fallback = Triggers(spelled);

        return fallback.Count == 1 ? fallback.Min : null;
    }

    private static SortedSet<string> Triggers(IEnumerable<string> spellings)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var name in spellings)
        {
            if (Trailing(name) is { } trigger) found.Add(trigger);
        }

        return found;
    }

    private static IReadOnlyList<string> Spelling(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals)
    {
        var spelled = new List<string>();

        Spellings(expression, locals, spelled, 0);

        return spelled;
    }

    private static string? Trailing(string spelled) => Split(spelled).Trigger;

    private static string Without(string spelled) => Split(spelled).Stem;

    private static (string Stem, string? Trigger) Split(string spelled)
    {
        var name = spelled.Replace(" ", string.Empty);

        for (var at = name.IndexOf(PerToken, StringComparison.Ordinal); at >= 0;
             at = name.IndexOf(PerToken, at + 1, StringComparison.Ordinal))
        {
            var after = at + PerToken.Length;
            if (after >= name.Length || !char.IsUpper(name[after])) continue;

            return (name[..at], Event(name[after..]));
        }

        return (name, null);
    }

    private static string Event(string trailing)
    {
        var breaks = new List<int>();

        for (var at = 1; at < trailing.Length; at++)
        {
            if (char.IsUpper(trailing[at])) breaks.Add(at);
        }

        return breaks.Count < EventWords ? trailing : trailing[..breaks[0]];
    }

    private static bool Marked(string name) =>
        Numbers.Normalize(name).Contains(CritToken, StringComparison.Ordinal);

    private static string Keyed(string name)
    {
        var compact = Numbers.Normalize(name);
        var at = compact.IndexOf(AtCritToken, StringComparison.Ordinal);

        if (at >= 0) return compact.Remove(at, AtCritToken.Length);

        at = compact.IndexOf(CritToken, StringComparison.Ordinal);

        return at >= 0 ? compact.Remove(at, CritToken.Length) : compact;
    }

    private static string Stem(string name) => Keyed(Without(name));

    private readonly record struct Vehicle(
        string Channel, string? Tag, double Sign, double? Share, string? Ceiling, string? Source);

    private readonly record struct Applied(
        Vehicle Vehicle, IReadOnlyList<KismetExpression> Parameters, int Statement);

    private readonly record struct Supplied(string Tag, KismetExpression? Magnitude, int Statement);

    private readonly record struct Called(
        string? Library, string Function, IReadOnlyList<KismetExpression> Parameters, int Statement);

    private readonly record struct Compiled(UStruct Body, UObject Cdo);

    private readonly record struct Repeat(int Start, int End, double Iterations, KismetExpression? Bound);

    private sealed record Generation(
        string Channel,
        KismetExpression? Magnitude,
        double? Value,
        double Sign,
        int Statement,
        bool Supplied)
    {
        public string? Ceiling { get; init; }

        public string? Name { get; init; }

        public double? Factor { get; init; }

        public IReadOnlyList<string> Sources { get; init; } = [];

        public IReadOnlyList<string> Spelled { get; init; } = [];

        public IReadOnlyList<string> Factored { get; init; } = [];

        public IReadOnlyList<string> Rates { get; init; } = [];

        public IReadOnlyList<KismetExpression> Inputs { get; init; } = [];

        public bool Unread { get; init; }
    }

    private sealed record Entry(
        string Channel,
        double? Amount,
        double? Share,
        double? Chance,
        string? Per,
        string? Group,
        string Source,
        string Kind,
        string? Claim,
        string Key,
        string Stem,
        bool Critical,
        bool Unread = false);

    private readonly record struct Scaler(string Row, double Value, IReadOnlyList<string> Reads);

    private readonly record struct Gated(
        IReadOnlyDictionary<int, double> Rolls, IReadOnlyDictionary<int, string> Claims);
}
