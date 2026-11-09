using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class CountReads
{
    private const string Multiply = "Multiply_";
    private const string Subtract = "Subtract_IntInt";
    private const string ScalableRead = "GetScalable";
    private const string EffectQuery = "ActiveEffectsWith";
    private const string EffectLookup = "ActiveEffect";
    private const string AssetTag = "AssetTag";
    private const string StackCount = "StackCount";
    private const string Divide = "Divide_";
    private const string MagnitudeRead = "EffectMagnitudeByTag";
    private const string AttributeRead = "GetFloatAttribute";
    private const string OutPrefix = "CallFunc_";
    private const int MaxDepth = 24;

    public static IReadOnlyDictionary<string, CountRead> Of(UObject? cdo)
    {
        var counts = new Dictionary<string, CountRead>(StringComparer.Ordinal);
        if (cdo?.Owner is not { } package) return counts;

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(export => export.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var functions = bodies
            .GroupBy(body => body.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var members = new Dictionary<string, List<(UStruct Body, KismetExpression Value)>>(StringComparer.Ordinal);

        foreach (var body in bodies)
        {
            foreach (var statement in body.ScriptBytecode!)
            {
                if (Assigned(statement) is var (variable, value) && variable is EX_InstanceVariable &&
                    DynamicTextFloats.Name(variable) is { } member)
                {
                    if (!members.TryGetValue(member, out var written)) members[member] = written = [];
                    written.Add((body, value));
                }
            }
        }

        var walk = new Walk(functions, members);

        foreach (var body in bodies)
        {
            var scope = walk.For(body);

            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Calls(statement, 0))
                {
                    if (RowSinks.Calling(call) is not { } called || !called.StartsWith(Multiply, StringComparison.Ordinal)) continue;

                    var parameters = RowSinks.Parameters(call);
                    if (parameters.Count != 2) continue;

                    for (var side = 0; side < 2; side++)
                    {
                        if (scope.Scaled(parameters[side]) is not { } property) continue;
                        if (scope.Counted(parameters[1 - side]) is not { } read ||
                            (read.Tags.Count == 0 && read.Attribute is null && read.Call is null && read.Inverts is null)) continue;

                        counts[property] = read;
                    }
                }
            }
        }

        return counts;
    }

    /// <summary>
    /// One package's bodies, members and functions, with one <see cref="Scope"/> per body and every
    /// expression's reading kept once found.
    /// </summary>
    private sealed class Walk(
        IReadOnlyDictionary<string, UStruct> functions,
        IReadOnlyDictionary<string, List<(UStruct Body, KismetExpression Value)>> members)
    {
        private readonly Dictionary<UStruct, Scope> _scopes = new(ReferenceEqualityComparer.Instance);

        public IReadOnlyDictionary<string, UStruct> Functions { get; } = functions;

        public IReadOnlyDictionary<string, List<(UStruct Body, KismetExpression Value)>> Members { get; } = members;

        public Visits<CountRead?> Counted { get; } = new();

        public Visits<string?> Scaled { get; } = new();

        public Visits<bool> Reads { get; } = new();

        /// <summary>The number of expressions open on the walk's path.</summary>
        public int Depth { get; set; }

        /// <summary>
        /// The shallowest open expression a revisit returned to since the current expression opened.
        /// </summary>
        public int Revisited { get; set; } = int.MaxValue;

        public Scope For(UStruct body)
        {
            if (!_scopes.TryGetValue(body, out var scope)) _scopes[body] = scope = new Scope(body, this);
            return scope;
        }
    }

    /// <summary>
    /// One reading per expression. An expression the path already has open reads as the cut value, so
    /// a local assigned from itself ends the walk instead of repeating it. A reading that depended on
    /// a cut back to an expression still open is not kept, because a walk from elsewhere reaches that
    /// expression with nothing open.
    /// </summary>
    private sealed class Visits<T>
    {
        private readonly Dictionary<KismetExpression, T> _known = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<KismetExpression, int> _open = new(ReferenceEqualityComparer.Instance);

        public T Visit(Walk walk, KismetExpression expression, T cut, Func<KismetExpression, T> read)
        {
            if (_known.TryGetValue(expression, out var known)) return known;

            if (_open.TryGetValue(expression, out var at))
            {
                walk.Revisited = Math.Min(walk.Revisited, at);
                return cut;
            }

            var depth = walk.Depth++;
            var outer = walk.Revisited;
            walk.Revisited = int.MaxValue;
            _open[expression] = depth;

            try
            {
                var value = read(expression);
                if (walk.Revisited >= depth) _known[expression] = value;
                return value;
            }
            finally
            {
                _open.Remove(expression);
                walk.Depth--;
                walk.Revisited = Math.Min(outer, walk.Revisited);
            }
        }
    }

    private sealed class Scope(UStruct body, Walk walk)
    {
        private readonly Dictionary<string, List<KismetExpression>> _locals = Locals(body);

        private readonly (KismetExpression Statement, KismetExpression Call)[] _calls =
        [
            .. (body.ScriptBytecode ?? []).SelectMany(statement => Calls(statement, 0).Select(call => (statement, call))),
        ];

        public string? Scaled(KismetExpression? expression) =>
            expression is null ? null : walk.Scaled.Visit(walk, expression, null, ScaledFrom);

        private string? ScaledFrom(KismetExpression expression)
        {
            switch (expression)
            {
                case EX_Cast cast:
                    return Scaled(cast.Target);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.GetValueOrDefault(local)?.Select(Scaled).FirstOrDefault(found => found is not null);
            }

            if (RowSinks.Invoking(expression) is { } call && RowSinks.Calling(call)?.StartsWith(ScalableRead, StringComparison.Ordinal) == true)
            {
                return RowSinks.Parameters(call).Select(DynamicTextFloats.Name).FirstOrDefault(found => found is not null);
            }

            return null;
        }

        public CountRead? Counted(KismetExpression? expression) =>
            expression is null ? null : walk.Counted.Visit(walk, expression, null, CountedFrom);

        private CountRead? CountedFrom(KismetExpression expression)
        {
            switch (expression)
            {
                case EX_Cast cast:
                    return Counted(cast.Target);
                case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                    return walk.Members.GetValueOrDefault(member)?
                        .Select(written => walk.For(written.Body).Counted(written.Value))
                        .FirstOrDefault(found => found is not null);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return First(_locals.GetValueOrDefault(local)) ?? Produced(local);
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

            var parameters = RowSinks.Parameters(call);

            if (called.Contains(EffectQuery, StringComparison.Ordinal) ||
                called.Contains(EffectLookup, StringComparison.Ordinal) && called.Contains(AssetTag, StringComparison.Ordinal))
            {
                return new CountRead([.. parameters.SelectMany(parameter => Tags(parameter, 0)).Distinct(StringComparer.Ordinal)], 0);
            }

            if (called.StartsWith(Divide, StringComparison.Ordinal) && parameters.Count == 2 &&
                Scaled(parameters[1]) is { } divisor && Reads(parameters[0]))
            {
                return new CountRead([], 0) { Inverts = divisor };
            }

            if (called.Contains(StackCount, StringComparison.Ordinal))
            {
                return parameters.Select(Counted).FirstOrDefault(found => found is not null) is { } held
                    ? held with { Stacks = true }
                    : null;
            }

            if (called.StartsWith(AttributeRead, StringComparison.Ordinal))
            {
                return parameters.Select(parameter => AttributeName(parameter)).FirstOrDefault(found => found is not null) is { } attribute
                    ? new CountRead([], 0) { Attribute = attribute }
                    : null;
            }

            if (called.StartsWith(Subtract, StringComparison.Ordinal) && parameters.Count == 2 &&
                Integer(parameters[1]) is { } skipped && Counted(parameters[0]) is { } counted)
            {
                return counted with { Skipped = counted.Skipped + skipped };
            }

            return parameters.Select(Counted).FirstOrDefault(found => found is not null);
        }

        private bool Reads(KismetExpression? expression) =>
            expression is not null && walk.Reads.Visit(walk, expression, false, ReadsFrom);

        private bool ReadsFrom(KismetExpression expression)
        {
            switch (expression)
            {
                case EX_Cast cast:
                    return Reads(cast.Target);
                case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                    return walk.Members.GetValueOrDefault(member)?.Any(written => walk.For(written.Body).Reads(written.Value)) == true;
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.GetValueOrDefault(local)?.Any(Reads) == true;
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return false;

            return called.Contains(MagnitudeRead, StringComparison.Ordinal) ||
                   RowSinks.Parameters(call).Any(Reads);
        }

        private CountRead? First(IEnumerable<KismetExpression>? values) =>
            values?.Select(Counted).FirstOrDefault(found => found is not null);

        private CountRead? Produced(string local)
        {
            foreach (var (statement, call) in _calls)
            {
                if (RowSinks.Calling(call) is not { } called || !local.StartsWith(Prefix(called), StringComparison.Ordinal)) continue;
                if (!RowSinks.Parameters(call).Any(parameter => DynamicTextFloats.Name(parameter) == local)) continue;

                if (called.Contains(EffectQuery, StringComparison.Ordinal))
                {
                    return new CountRead([.. RowSinks.Parameters(call).SelectMany(parameter => Tags(parameter, 0)).Distinct(StringComparer.Ordinal)], 0);
                }

                if (walk.Functions.GetValueOrDefault(called) is { } function)
                {
                    var output = local[Prefix(called).Length..];

                    if (walk.For(function).Named(output) is { } found) return found;
                }
                else if (Holder(statement) is { } holder)
                {
                    return new CountRead([], 0) { Call = $"{holder}.{called}" };
                }
            }

            return null;
        }

        private string? Holder(KismetExpression statement)
        {
            var reached = statement switch
            {
                EX_Let { Assignment: { } assigned } => assigned,
                EX_LetObj { Assignment: { } assigned } => assigned,
                _ => statement,
            };

            if (reached is not EX_Context { ObjectExpression: { } owner }) return null;

            return Classed(owner, 1);
        }

        private string? Classed(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            if (expression is EX_LocalVariable or EX_LocalOutVariable && DynamicTextFloats.Name(expression) is { } local)
            {
                foreach (var (_, call) in _calls)
                {
                    if (RowSinks.Calling(call) is not { } called || !local.StartsWith(Prefix(called), StringComparison.Ordinal)) continue;

                    var leaf = RowSinks.Parameters(call).OfType<EX_ObjectConst>().Select(constant => constant.Value.Name).FirstOrDefault(name => name is not null);
                    if (leaf is not null) return leaf.EndsWith("_C", StringComparison.Ordinal) ? leaf[..^2] : leaf;
                }

                return _locals.GetValueOrDefault(local)?.Select(value => Classed(value, depth + 1)).FirstOrDefault(found => found is not null);
            }

            return null;
        }

        private CountRead? Named(string local)
        {
            foreach (var (name, values) in _locals)
            {
                if (!name.Replace(' ', '_').Equals(local, StringComparison.Ordinal)) continue;
                if (First(values) is { } found) return found;
            }

            return null;
        }
    }

    private static string? AttributeName(KismetExpression? expression) => expression switch
    {
        EX_StructConst constant => constant.Properties.Select(property => AttributeName(property)).FirstOrDefault(found => found is not null),
        EX_StringConst text when text.Value.Length > 0 && !text.Value.Contains('.') => text.Value,
        EX_NameConst name when !name.Value.IsNone && !name.Value.Text.Contains('.') => name.Value.Text,
        _ => null,
    };

    private static string Prefix(string called) => $"{OutPrefix}{called.Replace(' ', '_')}_";

    private static int? Integer(KismetExpression expression) => expression switch
    {
        EX_IntConst constant => constant.Value,
        EX_IntOne => 1,
        EX_IntZero => 0,
        _ => null,
    };

    private static Dictionary<string, List<KismetExpression>> Locals(UStruct body)
    {
        var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

        foreach (var statement in body.ScriptBytecode ?? [])
        {
            if (Assigned(statement) is not var (variable, value) || variable is EX_InstanceVariable) continue;
            if (DynamicTextFloats.Name(variable) is not { } local) continue;

            if (!locals.TryGetValue(local, out var values)) locals[local] = values = [];
            values.Add(value);
        }

        return locals;
    }

    private static (KismetExpression Variable, KismetExpression Value)? Assigned(KismetExpression statement) => statement switch
    {
        EX_Let { Variable: { } variable, Assignment: { } value } => (variable, value),
        EX_LetObj { Variable: { } variable, Assignment: { } value } => (variable, value),
        EX_LetBool { Variable: { } variable, Assignment: { } value } => (variable, value),
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

    private static IEnumerable<string> Tags(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_NameConst name && !name.Value.IsNone && name.Value.Text.Contains('.'))
        {
            yield return name.Value.Text;
            yield break;
        }

        foreach (var child in RowSinks.Children(expression))
        {
            foreach (var tag in Tags(child, depth + 1)) yield return tag;
        }
    }
}
