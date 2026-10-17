using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using Fellowship.SDK;

namespace FellowshipExtractor.Extraction;

public sealed class TargetCountFalloffs(
    TypeMappings mappings,
    ConstantsTable constants,
    IReadOnlyList<TagRecord> tags,
    Func<string, UObject?> effectDefault)
{
    private const string Falloff = "MultiTargetDamageFalloffScaler";
    private const string Assigning = "CalculateAndSet";
    private const string SpecMaking = "MakeEffectSpec";
    private const string OutgoingSpecMaking = "MakeOutgoing";
    private const string EffectTable = "GameplayEffect";
    private const string ClassSuffix = "_C";
    private const string CapField = "MaxNumberOfTargets";
    private const string AcquisitionField = "TargetAcquisitionPolicy";
    private const string SelectionField = "TargetSelectionPolicy";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";
    private const string SetByCallerField = "SetByCallerMagnitude";
    private const string DataTagField = "DataTag";
    private const string TagNameField = "TagName";
    private const double ConstructedCap = 1;
    private const double RoundingBias = 0.5;
    private const int ThresholdParameter = 1;
    private const int SpecParameter = 2;
    private const int TagParameter = 3;
    private const int MaxDepth = 16;

    private readonly Dictionary<string, TagRecord> _effects = tags
        .Where(tag => tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase))
        .GroupBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IReadOnlySet<string>> _readTags = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, bool> _capped = new(StringComparer.Ordinal);

    public int Calls { get; private set; }

    public int Thresholds { get; private set; }

    public int EffectsVerified { get; private set; }

    public int EffectsAmbiguous { get; private set; }

    public int Caps { get; private set; }

    public List<string> Unread { get; } = [];

    public IReadOnlyList<TargetCountFalloffSource>? Of(UObject? cdo)
    {
        if (cdo?.Owner is not { } package) return null;

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(body => body.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var members = new Dictionary<string, List<(KismetExpression Value, Dictionary<string, KismetExpression> Locals)>>(
            StringComparer.Ordinal);
        var locals = new Dictionary<UStruct, Dictionary<string, KismetExpression>>();

        foreach (var body in bodies)
        {
            var held = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
            locals[body] = held;

            foreach (var statement in body.ScriptBytecode!)
            {
                if (DynamicTextFloats.Assigned(statement) is not var (variable, assignment)) continue;
                if (DynamicTextFloats.Name(variable) is not { } name) continue;

                if (variable is EX_InstanceVariable)
                {
                    if (!members.TryGetValue(name, out var written)) members[name] = written = [];
                    written.Add((assignment, held));
                }
                else
                {
                    held.TryAdd(name, assignment);
                }
            }
        }

        var found = new List<TargetCountFalloffSource>();

        foreach (var body in bodies)
        {
            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Falling(statement, 0))
                {
                    Calls++;

                    var where = $"{package.Name}.{body.Name}";

                    if (Threshold(call.Parameters.ElementAtOrDefault(ThresholdParameter), cdo) is not var (threshold, row))
                    {
                        Unread.Add($"{where}: threshold argument is not a readable scalable float");
                        continue;
                    }

                    Thresholds++;

                    IconSource? effect = null;

                    if (call.StackNode.Name.StartsWith(Assigning, StringComparison.Ordinal) &&
                        Tag(call.Parameters.ElementAtOrDefault(TagParameter)) is { } tag)
                    {
                        var reading = Traced(call.Parameters.ElementAtOrDefault(SpecParameter), locals[body], members, cdo, 0)
                            .Where(name => Reads(name, tag))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        if (reading is [var only] && _effects.TryGetValue(only, out var record))
                        {
                            effect = new IconSource(record.Table, record.NetId, record.Name);
                            EffectsVerified++;
                        }
                        else if (reading.Length > 1)
                        {
                            EffectsAmbiguous++;
                            Unread.Add($"{where}: {reading.Length} effects read {tag}: {string.Join(", ", reading)}");
                        }
                    }

                    found.Add(new TargetCountFalloffSource(effect, threshold, row));
                }
            }
        }

        var kept = found
            .GroupBy(source => (source.Effect?.NetId, source.Row, source.Threshold))
            .Select(group => group.First())
            .ToList();

        kept.RemoveAll(source => source.Effect is null &&
                                 kept.Any(other => other.Effect is not null &&
                                                   other.Row == source.Row &&
                                                   other.Threshold.Equals(source.Threshold)));

        return kept.Count == 0 ? null : kept;
    }

    public double? MaxTargets(UObject? cdo)
    {
        if (cdo?.Owner is not { } package) return null;

        var exports = package.GetExports().ToArray();

        if (Subobject(exports, cdo, AcquisitionField) is not { } acquisition) return null;
        if (Subobject(exports, acquisition, SelectionField) is not { } policy) return null;
        if (policy.Class?.Name.Text is not { } type || !Capped(type)) return null;

        Caps++;

        if (PropertyWalk.GetStruct(policy, CapField) is not { } scalable) return ConstructedCap;

        var multiplier = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;
        var row = Row(scalable);

        double cap;

        if (row is null)
        {
            cap = multiplier;
        }
        else if (constants.Lookup(row).Record?.Value is { } value)
        {
            cap = multiplier * value;
        }
        else
        {
            Caps--;
            Unread.Add($"{package.Name}: {CapField} names row {row}, which no constants table has");
            return null;
        }

        return Math.Floor(cap + RoundingBias);
    }

    private static UObject? Subobject(IReadOnlyList<UObject> exports, UObject holder, string field)
    {
        if (PropertyWalk.GetString(holder, field) is not { Length: > 0 } path) return null;

        var name = path[(path.LastIndexOfAny(['.', ':']) + 1)..];

        return exports.FirstOrDefault(export => export.Name.Equals(name, StringComparison.Ordinal));
    }

    private bool Capped(string type)
    {
        if (_capped.TryGetValue(type, out var known)) return known;

        var declared = false;

        for (var name = type; name is not null && !declared;)
        {
            if (!mappings.Types.TryGetValue(name, out var structure)) break;

            declared = structure.Properties.Values.Any(property =>
                property.Name.Equals(CapField, StringComparison.Ordinal));
            name = structure.SuperType;
        }

        return _capped[type] = declared;
    }

    private (double Value, string? Row)? Threshold(KismetExpression? expression, UObject cdo)
    {
        if (expression is not EX_InstanceVariable) return null;
        if (DynamicTextFloats.Name(expression) is not { } property) return null;
        if (PropertyWalk.GetStruct(cdo, property) is not { } scalable) return null;

        var multiplier = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;

        if (Row(scalable) is not { } row) return (multiplier, null);

        return constants.Lookup(row).Record?.Value is { } value ? (multiplier * value, row) : null;
    }

    private static string? Row(IPropertyHolder scalable) =>
        PropertyWalk.GetStruct(scalable, CurveField) is { } curve &&
        PropertyWalk.GetString(curve, RowField) is { Length: > 0 } row &&
        !row.Equals(NoRow, StringComparison.Ordinal)
            ? row
            : null;

    private static string? Tag(KismetExpression? expression) =>
        expression is EX_StructConst inline
            ? inline.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text
            : null;

    private static IEnumerable<EX_CallMath> Falling(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_CallMath call && call.StackNode.Name.Contains(Falloff, StringComparison.Ordinal))
        {
            yield return call;
        }

        foreach (var child in ResourceGains.Children(expression))
        {
            foreach (var found in Falling(child, depth + 1)) yield return found;
        }
    }

    private IEnumerable<string> Traced(
        KismetExpression? expression,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlyDictionary<string, List<(KismetExpression Value, Dictionary<string, KismetExpression> Locals)>> members,
        UObject cdo,
        int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        switch (expression)
        {
            case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                foreach (var (value, held) in members.GetValueOrDefault(member) ?? [])
                {
                    foreach (var name in Traced(value, held, members, cdo, depth + 1)) yield return name;
                }

                yield break;

            case EX_LocalVariable or EX_LocalOutVariable
                when DynamicTextFloats.Name(expression) is { } local && locals.TryGetValue(local, out var assigned):
                foreach (var name in Traced(assigned, locals, members, cdo, depth + 1)) yield return name;
                yield break;

            case EX_Context context:
                foreach (var name in Traced(context.ContextExpression, locals, members, cdo, depth + 1))
                {
                    yield return name;
                }

                yield break;
        }

        if (Invoked(expression) is not var (function, parameters) || parameters.Count == 0) yield break;

        if (function.Contains(SpecMaking, StringComparison.Ordinal) ||
            function.StartsWith(OutgoingSpecMaking, StringComparison.Ordinal))
        {
            if (EffectClass(parameters[0], locals, cdo, depth) is { } effect) yield return effect;
            yield break;
        }

        foreach (var name in Traced(parameters[0], locals, members, cdo, depth + 1)) yield return name;
    }

    private static (string Function, IReadOnlyList<KismetExpression> Parameters)? Invoked(KismetExpression expression) =>
        expression switch
        {
            EX_FinalFunction call => (call.StackNode.Name, call.Parameters),
            EX_VirtualFunction call => (call.VirtualFunctionName.Text, call.Parameters),
            _ => null,
        };

    private static string? EffectClass(
        KismetExpression expression, IReadOnlyDictionary<string, KismetExpression> locals, UObject cdo, int depth)
    {
        if (depth > MaxDepth) return null;

        var path = expression switch
        {
            EX_ObjectConst constant => PropertyWalk.AsString(constant.Value),
            EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member => PropertyWalk.GetString(cdo, member),
            EX_LocalVariable when DynamicTextFloats.Name(expression) is { } local &&
                                  locals.TryGetValue(local, out var assigned) => EffectClass(assigned, locals, cdo, depth + 1),
            _ => null,
        };

        if (path is null) return null;

        var leaf = path[(path.LastIndexOfAny(['.', '/']) + 1)..];

        return leaf.EndsWith(ClassSuffix, StringComparison.Ordinal) ? leaf[..^ClassSuffix.Length] : leaf;
    }

    private bool Reads(string effect, string tag)
    {
        if (!_readTags.TryGetValue(effect, out var read))
        {
            var found = new HashSet<string>(StringComparer.Ordinal);

            if (effectDefault(effect) is { } loaded)
            {
                foreach (var property in PropertyWalk.Walk(loaded))
                {
                    if (!property.Name.Equals(TagNameField, StringComparison.Ordinal)) continue;
                    if (!property.Path.Contains(SetByCallerField, StringComparison.Ordinal)) continue;
                    if (!property.Path.Contains(DataTagField, StringComparison.Ordinal)) continue;
                    if (PropertyWalk.AsString(property.Value) is { } name) found.Add(name);
                }
            }

            _readTags[effect] = read = found;
        }

        return read.Contains(tag);
    }
}
