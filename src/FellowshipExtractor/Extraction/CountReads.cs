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

        foreach (var body in bodies)
        {
            var scope = new Scope(body, functions, members);

            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Calls(statement, 0))
                {
                    if (RowSinks.Calling(call) is not { } called || !called.StartsWith(Multiply, StringComparison.Ordinal)) continue;

                    var parameters = RowSinks.Parameters(call);
                    if (parameters.Count != 2) continue;

                    for (var side = 0; side < 2; side++)
                    {
                        if (scope.Scaled(parameters[side], 0) is not { } property) continue;
                        if (scope.Counted(parameters[1 - side], 0) is not { } read ||
                            (read.Tags.Count == 0 && read.Attribute is null && read.Call is null && read.Inverts is null)) continue;

                        counts[property] = read;
                    }
                }
            }
        }

        return counts;
    }

    private sealed class Scope(
        UStruct body,
        IReadOnlyDictionary<string, UStruct> functions,
        IReadOnlyDictionary<string, List<(UStruct Body, KismetExpression Value)>> members)
    {
        private readonly Dictionary<string, List<KismetExpression>> _locals = Locals(body);

        public string? Scaled(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_Cast cast:
                    return Scaled(cast.Target, depth + 1);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.GetValueOrDefault(local)?.Select(value => Scaled(value, depth + 1)).FirstOrDefault(found => found is not null);
            }

            if (RowSinks.Invoking(expression) is { } call && RowSinks.Calling(call)?.StartsWith(ScalableRead, StringComparison.Ordinal) == true)
            {
                return RowSinks.Parameters(call).Select(DynamicTextFloats.Name).FirstOrDefault(found => found is not null);
            }

            return null;
        }

        public CountRead? Counted(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_Cast cast:
                    return Counted(cast.Target, depth + 1);
                case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                    return members.GetValueOrDefault(member)?
                        .Select(written => new Scope(written.Body, functions, members).Counted(written.Value, depth + 1))
                        .FirstOrDefault(found => found is not null);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return First(_locals.GetValueOrDefault(local), depth) ?? Produced(local, depth);
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

            var parameters = RowSinks.Parameters(call);

            if (called.Contains(EffectQuery, StringComparison.Ordinal) ||
                called.Contains(EffectLookup, StringComparison.Ordinal) && called.Contains(AssetTag, StringComparison.Ordinal))
            {
                return new CountRead([.. parameters.SelectMany(parameter => Tags(parameter, 0)).Distinct(StringComparer.Ordinal)], 0);
            }

            if (called.StartsWith(Divide, StringComparison.Ordinal) && parameters.Count == 2 &&
                Scaled(parameters[1], depth + 1) is { } divisor && Reads(parameters[0], depth + 1))
            {
                return new CountRead([], 0) { Inverts = divisor };
            }

            if (called.Contains(StackCount, StringComparison.Ordinal))
            {
                return parameters.Select(parameter => Counted(parameter, depth + 1)).FirstOrDefault(found => found is not null) is { } held
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
                Integer(parameters[1]) is { } skipped && Counted(parameters[0], depth + 1) is { } counted)
            {
                return counted with { Skipped = counted.Skipped + skipped };
            }

            return parameters.Select(parameter => Counted(parameter, depth + 1)).FirstOrDefault(found => found is not null);
        }

        private bool Reads(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return false;

            switch (expression)
            {
                case EX_Cast cast:
                    return Reads(cast.Target, depth + 1);
                case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                    return members.GetValueOrDefault(member)?.Any(written => new Scope(written.Body, functions, members).Reads(written.Value, depth + 1)) == true;
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.GetValueOrDefault(local)?.Any(value => Reads(value, depth + 1)) == true;
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return false;

            return called.Contains(MagnitudeRead, StringComparison.Ordinal) ||
                   RowSinks.Parameters(call).Any(parameter => Reads(parameter, depth + 1));
        }

        private CountRead? First(IEnumerable<KismetExpression>? values, int depth) =>
            values?.Select(value => Counted(value, depth + 1)).FirstOrDefault(found => found is not null);

        private CountRead? Produced(string local, int depth)
        {
            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Calls(statement, 0))
                {
                    if (RowSinks.Calling(call) is not { } called || !local.StartsWith(Prefix(called), StringComparison.Ordinal)) continue;
                    if (!RowSinks.Parameters(call).Any(parameter => DynamicTextFloats.Name(parameter) == local)) continue;

                    if (called.Contains(EffectQuery, StringComparison.Ordinal))
                    {
                        return new CountRead([.. RowSinks.Parameters(call).SelectMany(parameter => Tags(parameter, 0)).Distinct(StringComparer.Ordinal)], 0);
                    }

                    if (functions.GetValueOrDefault(called) is { } function)
                    {
                        var output = local[Prefix(called).Length..];

                        if (new Scope(function, functions, members).Named(output, depth + 1) is { } found) return found;
                    }
                    else if (Holder(statement, depth) is { } holder)
                    {
                        return new CountRead([], 0) { Call = $"{holder}.{called}" };
                    }
                }
            }

            return null;
        }

        private string? Holder(KismetExpression statement, int depth)
        {
            var reached = statement switch
            {
                EX_Let { Assignment: { } assigned } => assigned,
                EX_LetObj { Assignment: { } assigned } => assigned,
                _ => statement,
            };

            if (reached is not EX_Context { ObjectExpression: { } owner }) return null;

            return Classed(owner, depth + 1);
        }

        private string? Classed(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            if (expression is EX_LocalVariable or EX_LocalOutVariable && DynamicTextFloats.Name(expression) is { } local)
            {
                foreach (var statement in body.ScriptBytecode!)
                {
                    foreach (var call in Calls(statement, 0))
                    {
                        if (RowSinks.Calling(call) is not { } called || !local.StartsWith(Prefix(called), StringComparison.Ordinal)) continue;

                        var leaf = RowSinks.Parameters(call).OfType<EX_ObjectConst>().Select(constant => constant.Value.Name).FirstOrDefault(name => name is not null);
                        if (leaf is not null) return leaf.EndsWith("_C", StringComparison.Ordinal) ? leaf[..^2] : leaf;
                    }
                }

                return _locals.GetValueOrDefault(local)?.Select(value => Classed(value, depth + 1)).FirstOrDefault(found => found is not null);
            }

            return null;
        }

        private CountRead? Named(string local, int depth)
        {
            foreach (var (name, values) in _locals)
            {
                if (!name.Replace(' ', '_').Equals(local, StringComparison.Ordinal)) continue;
                if (First(values, depth) is { } found) return found;
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
