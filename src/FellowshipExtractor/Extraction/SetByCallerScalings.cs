using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Reads the set-by-caller magnitudes an ability's Blueprint assigns as a base plus an amount for each unit counted.
/// Hemorrhaging Strike assigns <c>SetByCaller.Duration</c> as <c>Resources Spent x DurationIncreasePerComboPoint +
/// BaseDuration</c>, and Resources Spent is the <c>ResourcesSecond</c> attribute <c>MaraUtils_UseAllComboPoints</c>
/// reads. A count is accepted only where the walk reaches the ability's level or source level, or an attribute the
/// owning hero declares a maximum of, such as <c>MaxResourcesSecond</c>.
/// </summary>
public static class SetByCallerScalings
{
    private const string Assignment = "AssignTagSetByCallerMagnitude";
    private const string ScalableRead = "GetScalableFloat";
    private const string AttributeRead = "GetFloatAttribute";
    private const string RankedRead = "WithAbilityLevel";
    private const string RuntimeUpdate = "SetActiveEffectMagnitude";
    private const string SourceLevel = "SourceLevel";
    private const string AbilityLevel = "AbilityLevel";
    private const string TagStruct = "GameplayTag";
    private const string OutPrefix = "CallFunc_";
    private const string MaximumPrefix = "Max";
    private const int MagnitudeParameter = 2;
    private const int MaxDepth = 24;
    private const int MostCounted = 20;

    /// <summary>A value linear in one unknown: <c>Constant + Slope x Unknown / Every</c>.</summary>
    private sealed record Linear(double Constant, double Slope, Unknown? Unknown, double Every);

    private sealed record Unknown(UStruct Body, KismetExpression Expression);

    public static IReadOnlyList<SetByCallerScaling> Of(
        UObject? cdo,
        ConstantsTable constants,
        AbstractFileProvider provider,
        Func<string, IReadOnlyDictionary<string, double>?> attributes,
        IReadOnlyList<string> heroes)
    {
        if (cdo?.Owner is not { } package) return [];

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(export => export.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var scope = new Scope(cdo, bodies, constants, provider);
        var found = new List<SetByCallerScaling>();

        foreach (var body in bodies)
        {
            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Calls(statement, 0))
                {
                    if (RowSinks.Calling(call) != Assignment) continue;

                    var parameters = RowSinks.Parameters(call);
                    if (Tag(parameters.ElementAtOrDefault(1)) is not { } tag) continue;

                    if (scope.Evaluate(body, parameters.ElementAtOrDefault(MagnitudeParameter), 0) is not
                        { Slope: not 0, Unknown: { } unknown } linear)
                    {
                        continue;
                    }

                    if (scope.Counted(unknown.Body, unknown.Expression, 0) is not { } count) continue;

                    var values = Counted(count, linear, attributes, heroes);
                    if (values is null && count is not (SourceLevel or AbilityLevel)) continue;

                    var scaling = new SetByCallerScaling(tag, count, Rounded(linear.Constant), Rounded(linear.Slope))
                    {
                        Every = linear.Every == 1 ? null : Rounded(linear.Every),
                        Values = values,
                    };

                    if (!found.Contains(scaling, Comparer)) found.Add(scaling);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The value at each rank <paramref name="ability"/> assigns <paramref name="tag"/> on specs of
    /// <paramref name="effect"/>. An attribute multiplied by a row of 0 at every rank leaves the value, so Topaz Winds
    /// reads <c>CooldownRecovery × (TopazPower × OverCapPerGemPower + 1)</c> as the row alone. A value still scaling
    /// with an attribute or a count at some rank, assignments disagreeing, or a Blueprint updating the tag on the
    /// active effect, gives none: Martial Command assigns 1 and then updates the tag from Toughness.
    /// </summary>
    public static IReadOnlyList<double>? Assigned(
        UObject ability, string tag, string effect, ConstantsTable constants, AbstractFileProvider provider)
    {
        if (ability.Owner is not { } package) return null;

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(export => export.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var scope = new Scope(ability, bodies, constants, provider);
        var magnitudes = new List<(UStruct Body, KismetExpression Magnitude)>();

        foreach (var body in bodies)
        {
            foreach (var call in body.ScriptBytecode!.SelectMany(statement => Calls(statement, 0)))
            {
                if (RowSinks.Calling(call)?.StartsWith(RuntimeUpdate, StringComparison.Ordinal) is true &&
                    RowSinks.Parameters(call).Any(parameter => SetByCallerDurations.Tag(parameter, ability) == tag))
                {
                    return null;
                }

                if (RowSinks.Calling(call) != Assignment) continue;

                var parameters = RowSinks.Parameters(call);

                if (SetByCallerDurations.Tag(parameters.ElementAtOrDefault(1), ability) != tag) continue;
                if (!effect.Equals(scope.Applied(body, parameters.FirstOrDefault()), StringComparison.OrdinalIgnoreCase)) continue;
                if (parameters.ElementAtOrDefault(MagnitudeParameter) is not { } magnitude) continue;

                magnitudes.Add((body, magnitude));
            }
        }

        if (magnitudes.Count == 0) return null;

        IReadOnlyList<double>? agreed = null;

        foreach (var (body, magnitude) in magnitudes)
        {
            if (scope.Ranked(body, magnitude) is not { } ranks) return null;
            if (agreed is not null && !agreed.SequenceEqual(ranks)) return null;

            agreed = ranks;
        }

        return agreed;
    }

    private static readonly ScalingComparer Comparer = new();

    private sealed class ScalingComparer : IEqualityComparer<SetByCallerScaling>
    {
        public bool Equals(SetByCallerScaling? x, SetByCallerScaling? y) =>
            x is not null && y is not null && x.Magnitude == y.Magnitude && x.Count == y.Count && x.Base == y.Base &&
            x.Per == y.Per && x.Every == y.Every;

        public int GetHashCode(SetByCallerScaling scaling) => HashCode.Combine(scaling.Magnitude, scaling.Count, scaling.Base);
    }

    /// <summary>
    /// The magnitude at each count from 1 to the maximum every owning hero declares for the counted attribute.
    /// </summary>
    private static IReadOnlyList<double>? Counted(
        string count,
        Linear linear,
        Func<string, IReadOnlyDictionary<string, double>?> attributes,
        IReadOnlyList<string> heroes)
    {
        if (count is SourceLevel or AbilityLevel || heroes.Count == 0) return null;

        var maxima = heroes
            .Select(hero => attributes(hero)?.GetValueOrDefault(MaximumPrefix + count))
            .Distinct()
            .ToArray();

        if (maxima is not [{ } maximum] || maximum < 1 || maximum > MostCounted || maximum != Math.Floor(maximum)) return null;

        return [.. Enumerable.Range(1, (int)maximum).Select(spent => Rounded(linear.Constant + linear.Slope * spent / linear.Every))];
    }

    private static double Rounded(double value) => Math.Round(value, 4);

    private sealed class Scope(UObject cdo, IReadOnlyList<UStruct> bodies, ConstantsTable constants, AbstractFileProvider provider)
    {
        private readonly Dictionary<UStruct, Dictionary<string, List<KismetExpression>>> _locals = [];
        private readonly Dictionary<string, List<(UStruct Body, KismetExpression Value)>> _members = Members(bodies);

        /// <summary>The rank being read, set only while <see cref="Ranked"/> runs.</summary>
        private int? _rank;

        private int _ranks;

        public string? Applied(UStruct body, KismetExpression? spec) =>
            SetByCallerDurations.Spec(spec, Locals(body), cdo, 0, []);

        /// <summary>The constant the expression evaluates to at each rank of the rows it reads.</summary>
        public IReadOnlyList<double>? Ranked(UStruct body, KismetExpression expression)
        {
            var values = new List<double>();

            _ranks = 1;

            for (_rank = 0; _rank < _ranks; _rank++)
            {
                if (Evaluate(body, expression, 0) is not { Slope: 0 } linear)
                {
                    _rank = null;
                    return null;
                }

                values.Add(Math.Round(linear.Constant, 10));
            }

            _rank = null;

            return values.Distinct().Count() == 1 ? [values[0]] : values;
        }

        private double? Read(UStruct body, string called, KismetExpression? reading)
        {
            if (_rank is { } rank && called.Contains(RankedRead, StringComparison.Ordinal) &&
                DynamicTextFloats.Name(reading) is { } property &&
                ClassDefaults.Inherited(cdo, property, provider)?.GenericValue is FScriptStruct { StructType: IPropertyHolder scalable } &&
                MagnitudeCalculations.ScalableRanks(scalable, constants) is { Count: > 0 } ranks)
            {
                _ranks = Math.Max(_ranks, ranks.Count);

                return ranks[Math.Min(rank, ranks.Count - 1)];
            }

            return new BareStateWalk(body, cdo, constants, null).Reading(reading);
        }

        public Linear? Evaluate(UStruct body, KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_IntConst value: return Constant(value.Value);
                case EX_Int64Const value: return Constant(value.Value);
                case EX_ByteConst value: return Constant(value.Value);
                case EX_IntConstByte value: return Constant(value.Value);
                case EX_FloatConst value: return Constant(value.Value);
                case EX_DoubleConst value: return Constant(value.Value);
                case EX_IntOne: return Constant(1);
                case EX_IntZero: return Constant(0);
                case EX_Cast cast: return Evaluate(body, cast.Target, depth + 1);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return Locals(body).GetValueOrDefault(local) is [var assigned]
                        ? Evaluate(body, assigned, depth + 1)
                        : new Linear(0, 1, new Unknown(body, expression), 1);
                case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                    if (_members.ContainsKey(member)) return new Linear(0, 1, new Unknown(body, expression), 1);
                    return PropertyWalk.AsNumber(PropertyWalk.Get(cdo, member)?.GenericValue) is { } declared ? Constant(declared) : null;
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

            var parameters = RowSinks.Parameters(call);

            if (called.StartsWith(ScalableRead, StringComparison.Ordinal))
            {
                return Read(body, called, parameters.FirstOrDefault()) is { } read ? Constant(read) : null;
            }

            if (_rank is not null && called.StartsWith(AttributeRead, StringComparison.Ordinal))
            {
                return new Linear(0, 1, new Unknown(body, call), 1);
            }

            if (called is "GetSourceLevel" or "GetAbilityLevel")
            {
                return new Linear(0, 1, new Unknown(body, call), 1);
            }

            if (called.StartsWith("Conv_", StringComparison.Ordinal)) return Evaluate(body, parameters.FirstOrDefault(), depth + 1);

            if (parameters.Count != 2) return null;

            var left = Evaluate(body, parameters[0], depth + 1);
            var right = Evaluate(body, parameters[1], depth + 1);
            if (left is null || right is null) return null;

            if (called.StartsWith("Add_", StringComparison.Ordinal)) return Sum(left, right, 1);
            if (called.StartsWith("Subtract_", StringComparison.Ordinal)) return Sum(left, right, -1);

            if (called.StartsWith("Multiply_", StringComparison.Ordinal))
            {
                if (_rank is not null && (left is { Unknown: null, Constant: 0 } || right is { Unknown: null, Constant: 0 })) return Constant(0);
                if (left.Unknown is null && left.Slope == 0) return Scaled(right, left.Constant);
                if (right.Unknown is null && right.Slope == 0) return Scaled(left, right.Constant);
                return null;
            }

            if (called.StartsWith("Divide_", StringComparison.Ordinal) && right is { Unknown: null, Slope: 0, Constant: not 0 } divisor)
            {
                return left with { Constant = left.Constant / divisor.Constant, Every = left.Every * divisor.Constant };
            }

            return null;
        }

        private static Linear Constant(double value) => new(value, 0, null, 1);

        private static Linear Scaled(Linear term, double factor) =>
            term with { Constant = term.Constant * factor, Slope = term.Slope * factor };

        private static Linear? Sum(Linear left, Linear right, int sign)
        {
            if (left.Unknown is not null && right.Unknown is not null) return null;

            var constant = left.Constant + sign * right.Constant;

            return left.Unknown is not null ? new Linear(constant, left.Slope, left.Unknown, left.Every)
                : right.Unknown is not null ? new Linear(constant, sign * right.Slope, right.Unknown, right.Every)
                : Constant(constant);
        }

        /// <summary>
        /// The attribute an unknown counts, or the source level, following instance variables to their writes,
        /// function parameters to their call sites, and a function's output into its body, including a function of
        /// another Blueprint function library.
        /// </summary>
        public string? Counted(UStruct body, KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_Cast cast:
                    return Counted(body, cast.Target, depth + 1);
                case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                    return _members.GetValueOrDefault(member)?
                        .Select(written => Counted(written.Body, written.Value, depth + 1))
                        .FirstOrDefault(found => found is not null);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return Local(body, local, depth);
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

            var parameters = RowSinks.Parameters(call);

            if (called is "GetSourceLevel") return SourceLevel;
            if (called is "GetAbilityLevel") return AbilityLevel;

            if (called.Equals(AttributeRead, StringComparison.Ordinal))
            {
                return parameters.Select(Attribute).FirstOrDefault(found => found is not null);
            }

            return called.StartsWith("Conv_", StringComparison.Ordinal) ? Counted(body, parameters.FirstOrDefault(), depth + 1) : null;
        }

        private string? Local(UStruct body, string local, int depth)
        {
            if (Locals(body).GetValueOrDefault(local) is { Count: > 0 } assigned)
            {
                return assigned.Select(value => Counted(body, value, depth + 1)).FirstOrDefault(found => found is not null);
            }

            if (Parameters(body).IndexOf(local) is var at and >= 0)
            {
                foreach (var caller in Holders(body))
                {
                    foreach (var statement in caller.ScriptBytecode!)
                    {
                        foreach (var call in Calls(statement, 0))
                        {
                            if (RowSinks.Calling(call) != body.Name) continue;
                            if (Counted(caller, RowSinks.Parameters(call).ElementAtOrDefault(at), depth + 1) is { } found) return found;
                        }
                    }
                }

                return null;
            }

            if (!local.StartsWith(OutPrefix, StringComparison.Ordinal)) return null;

            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Calls(statement, 0))
                {
                    if (RowSinks.Calling(call) is not { } called) continue;

                    var prefix = $"{OutPrefix}{called.Replace(' ', '_')}_";
                    if (!local.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (!RowSinks.Parameters(call).Any(parameter => DynamicTextFloats.Name(parameter) == local)) continue;

                    if (Function(statement, called) is not { } function) continue;

                    var output = local[prefix.Length..];
                    var named = Locals(function).Keys.FirstOrDefault(name => name.Replace(' ', '_').Equals(output, StringComparison.Ordinal));

                    if (named is not null && Local(function, named, depth + 1) is { } found) return found;
                }
            }

            return null;
        }

        private IEnumerable<UStruct> Holders(UStruct body) =>
            body.Owner is { } owner && !ReferenceEquals(owner, cdo.Owner)
                ? owner.GetExports().OfType<UStruct>().Where(export => export.ScriptBytecode is { Length: > 0 })
                : bodies;

        /// <summary>
        /// The compiled body of <paramref name="called"/>: one of the ability's own, or one of the Blueprint
        /// function library whose default object the statement calls it on.
        /// </summary>
        private UStruct? Function(KismetExpression statement, string called)
        {
            if (bodies.FirstOrDefault(body => body.Name.Equals(called, StringComparison.Ordinal)) is { } own) return own;

            var context = statement switch
            {
                EX_Context direct => direct,
                EX_Let { Assignment: EX_Context assigned } => assigned,
                _ => null,
            };

            if (context?.ObjectExpression is not EX_ObjectConst { Value: { } library }) return null;
            if (PropertyWalk.AsString(library) is not { } path) return null;

            var packagePath = path.Split('.')[0];
            if (!provider.TryLoadPackage(packagePath, out var package)) return null;

            return package.GetExports()
                .OfType<UStruct>()
                .FirstOrDefault(export => export.Name.Equals(called, StringComparison.Ordinal) && export.ScriptBytecode is { Length: > 0 });
        }

        private Dictionary<string, List<KismetExpression>> Locals(UStruct body)
        {
            if (_locals.TryGetValue(body, out var held)) return held;

            held = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

            foreach (var statement in body.ScriptBytecode ?? [])
            {
                if (Assigned(statement) is not var (variable, value) || variable is EX_InstanceVariable) continue;
                if (DynamicTextFloats.Name(variable) is not { } local) continue;

                if (!held.TryGetValue(local, out var values)) held[local] = values = [];
                values.Add(value);
            }

            return _locals[body] = held;
        }

        private static List<string> Parameters(UStruct body)
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

        private static Dictionary<string, List<(UStruct Body, KismetExpression Value)>> Members(IReadOnlyList<UStruct> bodies)
        {
            var members = new Dictionary<string, List<(UStruct, KismetExpression)>>(StringComparer.Ordinal);

            foreach (var body in bodies)
            {
                foreach (var statement in body.ScriptBytecode!)
                {
                    if (Assigned(statement) is not var (variable, value) || variable is not EX_InstanceVariable) continue;
                    if (DynamicTextFloats.Name(variable) is not { } member) continue;

                    if (!members.TryGetValue(member, out var written)) members[member] = written = [];
                    written.Add((body, value));
                }
            }

            return members;
        }
    }

    private static (KismetExpression Variable, KismetExpression Value)? Assigned(KismetExpression statement) => statement switch
    {
        EX_Let { Variable: { } variable, Assignment: { } value } => (variable, value),
        EX_LetObj { Variable: { } variable, Assignment: { } value } => (variable, value),
        _ => null,
    };

    private static string? Tag(KismetExpression? expression) =>
        expression is EX_StructConst { Struct.Name: TagStruct } inline
            ? inline.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text
            : null;

    private static string? Attribute(KismetExpression? expression) => expression switch
    {
        EX_StructConst constant => constant.Properties.OfType<EX_StringConst>()
            .Select(text => text.Value)
            .FirstOrDefault(text => text.Length > 0 && !text.Contains('.') && !text.Contains('/')),
        _ => null,
    };

    private static IEnumerable<KismetExpression> Calls(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (RowSinks.Invoking(expression) is { } call)
        {
            yield return call;
            foreach (var parameter in RowSinks.Parameters(call))
            {
                foreach (var nested in Calls(parameter, depth + 1)) yield return nested;
            }

            yield break;
        }

        foreach (var child in RowSinks.Children(expression))
        {
            foreach (var nested in Calls(child, depth + 1)) yield return nested;
        }
    }
}
