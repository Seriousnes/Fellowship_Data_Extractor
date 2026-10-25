using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed record GatedRead(string Tag, string? Property, string? Effect)
{
    public string? Owner { get; init; }
}

public static class TagGatedReads
{
    private const string TalentPrefix = "Talents.";
    private const string Both = "BooleanAND";
    private const string ClassSuffix = "_C";
    private const string EffectPrefix = "GE_";
    private const int MaxDepth = 32;

    private static readonly HashSet<string> TagTests = new(StringComparer.Ordinal)
    {
        "HasMatchingGameplayTag",
        "HasAnyMatchingGameplayTags",
        "HasAllMatchingGameplayTags",
        "HasTag",
    };

    public static IReadOnlyList<GatedRead> Of(UObject? asset)
    {
        if (asset is null) return [];

        var reads = new List<GatedRead>();
        var seen = new HashSet<GatedRead>();

        foreach (var export in asset.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } } body) continue;

            foreach (var read in Gated(body, asset))
            {
                if (seen.Add(read)) reads.Add(read);
            }
        }

        return reads;
    }

    private static IEnumerable<GatedRead> Gated(UStruct body, UObject cdo)
    {
        if (BytecodeGates.Of(body, cdo) is not { } gates) yield break;

        var bytecode = body.ScriptBytecode;
        var branches = gates.Branches.ToDictionary(branch => branch.At, branch => branch.Condition);
        if (branches.Count == 0) yield break;

        var statements = bytecode.ToDictionary(statement => statement.StatementIndex);
        var own = RowSinks.OwnerClass(cdo.Class?.Name.Text);
        var running = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

        for (var at = 0; at < bytecode.Length; at++)
        {
            if (branches.TryGetValue(at, out var condition))
            {
                var tags = new SortedSet<string>(StringComparer.Ordinal);

                Tested(condition, running, tags, 0);

                if (tags.Count > 0)
                {
                    var read = new SortedSet<(string Property, string? Owner)>();
                    var applied = new SortedSet<string>(StringComparer.Ordinal);

                    foreach (var offset in gates.Only(at))
                    {
                        if (statements.TryGetValue(offset, out var statement)) Reads(statement, own, read, applied, 0);
                    }

                    foreach (var tag in tags)
                    {
                        foreach (var (property, owner) in read) yield return new GatedRead(tag, property, null) { Owner = owner };
                        foreach (var effect in applied) yield return new GatedRead(tag, null, effect);
                    }
                }
            }

            if (Writes(bytecode[at]) is { } assignment) running[assignment.Name] = assignment.Value;
        }
    }

    private static void Tested(
        KismetExpression? condition,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ISet<string> tags,
        int depth)
    {
        if (depth > MaxDepth) return;
        if (Invoking(Bare(condition, locals, 0)) is not { } called) return;
        if (Calling(called) is not { } invoked) return;

        if (invoked.StartsWith(Both, StringComparison.Ordinal))
        {
            foreach (var operand in Parameters(called)) Tested(operand, locals, tags, depth + 1);

            return;
        }

        if (!TagTests.Contains(invoked)) return;

        foreach (var argument in Parameters(called)) Literals(Bare(argument, locals, 0), locals, tags, 0);
    }

    private static void Literals(
        KismetExpression? expression,
        IReadOnlyDictionary<string, KismetExpression> locals,
        ISet<string> tags,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return;

        if (expression is EX_NameConst name)
        {
            if (TagNames.IsTalent(name.Value.Text)) tags.Add(name.Value.Text);

            return;
        }

        foreach (var child in Children(expression)) Literals(Bare(child, locals, 0), locals, tags, depth + 1);
    }

    private static void Reads(
        KismetExpression? expression, string? own, ISet<(string, string?)> read, ISet<string> applied, int depth)
    {
        if (expression is null || depth > MaxDepth) return;

        if (expression is EX_InstanceVariable && DynamicTextFloats.Name(expression) is { } property)
        {
            var owner = RowSinks.OwnerClass(((EX_InstanceVariable)expression).Variable.New?.ResolvedOwner?.Name);
            read.Add((property, owner == own ? null : owner));

            return;
        }

        if (expression is EX_ObjectConst { Value.Name: { } named } &&
            named.EndsWith(ClassSuffix, StringComparison.Ordinal))
        {
            if (named.StartsWith(EffectPrefix, StringComparison.Ordinal)) applied.Add(named[..^ClassSuffix.Length]);

            return;
        }

        foreach (var child in Children(expression)) Reads(child, own, read, applied, depth + 1);
    }

    private static KismetExpression? Bare(
        KismetExpression? expression, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;
        if (expression is EX_Cast cast) return Bare(cast.Target, locals, depth + 1);
        if (expression is not (EX_LocalVariable or EX_LocalOutVariable)) return expression;

        return DynamicTextFloats.Name(expression) is { } name && locals.TryGetValue(name, out var assigned)
            ? Bare(assigned, locals, depth + 1)
            : expression;
    }

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

    private static KismetExpression? Invoking(KismetExpression? expression) => expression switch
    {
        EX_Context context => Invoking(context.ContextExpression),
        EX_CallMath or EX_LocalFinalFunction or EX_FinalFunction
            or EX_LocalVirtualFunction or EX_VirtualFunction => expression,
        _ => null,
    };

    private static string? Calling(KismetExpression expression) => expression switch
    {
        EX_CallMath call => call.StackNode.Name,
        EX_LocalFinalFunction call => call.StackNode.Name,
        EX_FinalFunction call => call.StackNode.Name,
        EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
        EX_VirtualFunction call => call.VirtualFunctionName.Text,
        _ => null,
    };

    private static IReadOnlyList<KismetExpression> Parameters(KismetExpression expression) => expression switch
    {
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        _ => [],
    };

    private static IEnumerable<KismetExpression?> Children(KismetExpression expression) => expression switch
    {
        EX_Let let => [let.Variable, let.Assignment],
        EX_LetObj let => [let.Variable, let.Assignment],
        EX_LetBool let => [let.Variable, let.Assignment],
        EX_Context context => [context.ObjectExpression, context.ContextExpression],
        EX_InterfaceContext context => [context.InterfaceValue],
        EX_Cast cast => [cast.Target],
        EX_DynamicCast cast => [cast.Target],
        EX_Return returned => [returned.ReturnExpression],
        EX_JumpIfNot jump => [jump.BooleanExpression],
        EX_PopExecutionFlowIfNot branch => [branch.BooleanExpression],
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        EX_StructConst constant => constant.Properties,
        EX_ArrayConst constant => constant.Elements,
        EX_SetArray array => array.Elements.Prepend(array.AssigningProperty),
        EX_ArrayGetByRef element => [element.ArrayVariable, element.ArrayIndex],
        EX_SwitchValue chosen => chosen.Cases
            .SelectMany(c => new KismetExpression?[] { c.CaseIndexValueTerm, c.CaseTerm })
            .Prepend(chosen.IndexTerm)
            .Append(chosen.DefaultTerm),
        EX_StructMemberContext member => [member.StructExpression],
        _ => [],
    };
}
