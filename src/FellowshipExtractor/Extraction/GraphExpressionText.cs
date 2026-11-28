using CUE4Parse.UE4.Kismet;

using Fellowship.SDK.Models;

using FellowshipExtractor.Blueprints;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// A graph expression as text in the graph's own names. A local is replaced by the expression assigned to it, so
/// <c>CallFunc_Greater_IntInt_ReturnValue</c> reads <c>Greater_IntInt(Array_Length(TargetsLeft), 0)</c>. A function has
/// its name without its library class, an instance variable its name alone, an event's parameter the name of the
/// delegate's parameter, and a gameplay tag its name.
/// </summary>
public static class GraphExpressionText
{
    private const int MaxDepth = 32;
    private const int MaxCounterDepth = 3;
    private const string Negation = "Not_PreBool";
    private const string And = "BooleanAND";
    private const string Or = "BooleanOR";
    private const string Validity = "IsValid";
    private const string Literal = "MakeLiteral";
    private const string Tag = "GameplayTag";
    private const string TagContainer = "GameplayTagContainer";
    private const string Attribute = "GameplayAttribute";
    private const string CallLocal = "CallFunc_";
    private const string NodeLocal = "K2Node_";
    private const string LibraryDefault = "Default__";

    private static readonly string[] BoolCasts = ["CST_ObjectToBool", "CST_InterfaceToBool"];

    /// <summary>
    /// The text of an expression read at a statement of a body. <paramref name="at"/> is the statement's index in
    /// <paramref name="statements"/>.
    /// </summary>
    public static string Of(KismetExpression? expression, KismetExpression[] statements, int at) =>
        Render(expression, statements, at, 0, []);

    /// <summary>
    /// The conditions a branch's boolean expression states on one side. <c>Not_PreBool</c> gives its operand on the
    /// other side. A <c>BooleanAND</c> taken true and a <c>BooleanOR</c> taken false give each operand. A literal, a
    /// validity test of the object named by <paramref name="waited"/> and the false side of a test reading a loop
    /// counter give none, because a counted loop always ends through that side.
    /// </summary>
    public static IEnumerable<BranchCondition> Conditions(
        KismetExpression? expression, bool held, KismetExpression[] statements, int at, string? waited, int depth = 0)
    {
        var (value, from) = Assigned(expression, statements, at);

        if (value is EX_True or EX_False) return [];

        if (depth < MaxDepth && RowSinks.Invoking(value) is { } call && RowSinks.Calling(call) is { } called)
        {
            var parameters = RowSinks.Parameters(call);

            if (called == Negation)
            {
                return Conditions(parameters.FirstOrDefault(), !held, statements, from, waited, depth + 1);
            }

            if (called == And && held || called == Or && !held)
            {
                return parameters.SelectMany(parameter => Conditions(parameter, held, statements, from, waited, depth + 1));
            }

            if (called == Validity && waited is not null && DynamicTextFloats.Name(parameters.FirstOrDefault()) == waited) return [];

            if (!held && parameters.Any(parameter => Counts(parameter, statements, from, 0))) return [];
        }

        return [new BranchCondition(Render(expression, statements, at, 0, []), held)];
    }

    /// <summary>
    /// The expression a local holds at a statement, through the locals assigned one from another: the nearest
    /// assignment before the statement, or the first after it where none is before. A local with no assignment and a
    /// loop counter are returned as they are.
    /// </summary>
    private static (KismetExpression? Value, int At) Assigned(KismetExpression? expression, KismetExpression[] statements, int at)
    {
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (expression is not (EX_LocalVariable or EX_LocalOutVariable) || DynamicTextFloats.Name(expression) is not { } local) break;
            if (Counter(local, statements) || Assignment(local, statements, at) is not { } found) break;

            (expression, at) = found;
        }

        return (expression, at);
    }

    /// <summary>
    /// The variable and the value of an assignment. An array set to no element is the compiler's reset of a call's
    /// output array before the call, and is no assignment.
    /// </summary>
    private static (KismetExpression Variable, KismetExpression Value)? Assigning(KismetExpression statement) => statement switch
    {
        EX_Let { Variable: { } assigned, Assignment: { } to } => (assigned, to),
        EX_LetObj { Variable: { } assigned, Assignment: { } to } => (assigned, to),
        EX_LetBool { Variable: { } assigned, Assignment: { } to } => (assigned, to),
        EX_SetArray { AssigningProperty: { } assigned, Elements.Length: > 0 } array => (assigned, array),
        _ => null,
    };

    private static (KismetExpression Value, int At)? Assignment(string local, KismetExpression[] statements, int at)
    {
        foreach (var index in Nearest(statements.Length, at))
        {
            if (Assigning(statements[index]) is { Variable: EX_LocalVariable or EX_LocalOutVariable } assigning &&
                DynamicTextFloats.Name(assigning.Variable) == local)
            {
                return (assigning.Value, index);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a local is a loop counter: one of its assignments reads the local itself, directly or through the locals
    /// assigned before it, as <c>Temp_int_Loop_Counter_Variable = Add_IntInt(Temp_int_Loop_Counter_Variable, 1)</c> does.
    /// </summary>
    private static bool Counter(string local, KismetExpression[] statements)
    {
        for (var at = 0; at < statements.Length; at++)
        {
            if (Assigning(statements[at]) is not { Variable: EX_LocalVariable or EX_LocalOutVariable } assigning) continue;
            if (DynamicTextFloats.Name(assigning.Variable) != local) continue;
            if (Reads(assigning.Value, local, statements, at, 0)) return true;
        }

        return false;
    }

    private static bool Reads(KismetExpression? expression, string local, KismetExpression[] statements, int at, int depth)
    {
        foreach (var read in Locals(expression, 0))
        {
            if (read == local) return true;
            if (depth >= MaxCounterDepth || Assignment(read, statements, at) is not { } assigned || assigned.At >= at) continue;
            if (Reads(assigned.Value, local, statements, assigned.At, depth + 1)) return true;
        }

        return false;
    }

    /// <summary>Whether an expression read at a statement reads a loop counter.</summary>
    private static bool Counts(KismetExpression? expression, KismetExpression[] statements, int at, int depth)
    {
        foreach (var read in Locals(expression, 0))
        {
            if (Counter(read, statements)) return true;
            if (depth >= MaxCounterDepth || Assignment(read, statements, at) is not { } assigned) continue;
            if (Counts(assigned.Value, statements, assigned.At, depth + 1)) return true;
        }

        return false;
    }

    private static IEnumerable<string> Locals(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_LocalVariable or EX_LocalOutVariable)
        {
            if (DynamicTextFloats.Name(expression) is { } name) yield return name;

            yield break;
        }

        IEnumerable<KismetExpression?> children = expression switch
        {
            EX_Cast cast => [cast.Target],
            EX_CastBase cast => [cast.Target],
            _ when RowSinks.Invoking(expression) == expression => RowSinks.Parameters(expression),
            _ => RowSinks.Children(expression),
        };

        foreach (var child in children)
        {
            foreach (var name in Locals(child, depth + 1)) yield return name;
        }
    }

    /// <summary>The call filling a local through an out parameter.</summary>
    private static (KismetExpression Call, int At)? Filling(string local, KismetExpression[] statements, int at)
    {
        foreach (var index in Nearest(statements.Length, at))
        {
            foreach (var call in CommitTriggers.Invocations(statements[index], 0))
            {
                if (RowSinks.Parameters(call).Any(parameter => Filled(parameter, call) == local)) return (call, index);
            }
        }

        return null;
    }

    /// <summary>The prefix of the locals the compiler makes for a call's outputs: the function's name with each other character as an underscore.</summary>
    private static string Outputs(KismetExpression call) =>
        $"{CallLocal}{string.Concat(RowSinks.Calling(call)!.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '_'))}_";

    /// <summary>The name of a call's parameter where the parameter is a local the compiler made for the call's output.</summary>
    private static string? Filled(KismetExpression parameter, KismetExpression call) =>
        parameter is EX_LocalVariable or EX_LocalOutVariable &&
        DynamicTextFloats.Name(parameter) is { } name &&
        name.StartsWith(Outputs(call), StringComparison.Ordinal)
            ? name
            : null;

    /// <summary>Statement indices before one, nearest first, then those from it onward.</summary>
    private static IEnumerable<int> Nearest(int length, int at)
    {
        for (var back = Math.Min(at, length) - 1; back >= 0; back--) yield return back;
        for (var forward = Math.Max(at, 0); forward < length; forward++) yield return forward;
    }

    private static string Render(KismetExpression? expression, KismetExpression[] statements, int at, int depth, HashSet<string> expanding)
    {
        string Inner(KismetExpression? inner, int from) => Render(inner, statements, from, depth + 1, expanding);

        if (depth > MaxDepth) return KismetPrinter.Expression(expression);

        switch (expression)
        {
            case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
            {
                if (Counter(local, statements) || !expanding.Add(local)) return Unnumbered(local);

                try
                {
                    if (Assignment(local, statements, at) is { } assigned) return Inner(assigned.Value, assigned.At);

                    if (Filling(local, statements, at) is { } filled)
                    {
                        return $"{Call(filled.Call, statements, filled.At, depth + 1, expanding)}.{Unnumbered(local[Outputs(filled.Call).Length..])}";
                    }
                }
                finally
                {
                    expanding.Remove(local);
                }

                // A node's local has the node's kind before its pin: K2Node_CustomEvent_SpawnedActor, K2Node_MakeStruct_ScalableFloat.
                var pin = local.StartsWith(NodeLocal, StringComparison.Ordinal) ? local.IndexOf('_', NodeLocal.Length) : -1;

                return Unnumbered(pin > 0 && pin < local.Length - 1 ? local[(pin + 1)..] : local);
            }

            case EX_InstanceVariable variable:
                return KismetPrinter.Pointer(variable.Variable);

            case EX_ObjectConst constant:
                return KismetPrinter.Object(constant.Value, shortName: true);

            case EX_Cast cast:
            {
                var target = Assigned(cast.Target, statements, at);

                return BoolCasts.Contains(cast.ConversionType.ToString()) && target.Value is not EX_CastBase
                    ? $"{cast.ConversionType.ToString()[4..]}({Inner(cast.Target, at)})"
                    : Inner(cast.Target, at);
            }

            case EX_CastBase cast:
                return $"{cast.GetType().Name[3..]}<{KismetPrinter.Object(cast.ClassPtr, shortName: true)}>({Inner(cast.Target, at)})";

            case EX_InterfaceContext context:
                return Inner(context.InterfaceValue, at);

            case EX_Context context:
            {
                var member = Inner(context.ContextExpression, at);

                return context.ObjectExpression is EX_Self ||
                       context.ObjectExpression is EX_ObjectConst { Value.Name: { } holder } && holder.StartsWith(LibraryDefault, StringComparison.Ordinal)
                    ? member
                    : $"{Inner(context.ObjectExpression, at)}.{member}";
            }

            case EX_StructMemberContext member:
                return $"{Inner(member.StructExpression, at)}.{KismetPrinter.Pointer(member.Property)}";

            case EX_ArrayGetByRef element:
                return $"{Inner(element.ArrayVariable, at)}[{Inner(element.ArrayIndex, at)}]";

            case EX_SetArray array:
                return $"[{string.Join(", ", array.Elements.Select(element => Inner(element, at)))}]";

            case EX_StructConst { Struct.Name: Tag, Properties: [EX_NameConst name, ..] }:
                return name.Value.Text;

            case EX_StructConst { Struct.Name: TagContainer, Properties: [EX_ArrayConst tags, ..] }:
                return $"[{string.Join(", ", tags.Elements.Select(tag => Inner(tag, at)))}]";

            case EX_StructConst { Struct.Name: Attribute, Properties: [EX_StringConst name, ..] }:
                return name.Value;

            case EX_SwitchValue chosen:
            {
                var cases = chosen.Cases
                    .Select(entry => $"{Inner(entry.CaseIndexValueTerm, at)}: {Inner(entry.CaseTerm, at)}")
                    .Append($"default: {Inner(chosen.DefaultTerm, at)}");

                return $"switch({Inner(chosen.IndexTerm, at)}) {{{string.Join(", ", cases)}}}";
            }
        }

        return expression is not null && RowSinks.Invoking(expression) == expression
            ? Call(expression, statements, at, depth, expanding)
            : KismetPrinter.Expression(expression);
    }

    private static string Call(KismetExpression call, KismetExpression[] statements, int at, int depth, HashSet<string> expanding)
    {
        var called = RowSinks.Calling(call)!;

        var parameters = RowSinks.Parameters(call)
            .Where(parameter => Filled(parameter, call) is not { } local || Assignment(local, statements, at) is not null)
            .Select(parameter => Render(parameter, statements, at, depth + 1, expanding))
            .ToArray();

        return called.StartsWith(Literal, StringComparison.Ordinal) && parameters is [var literal]
            ? literal
            : $"{called}({string.Join(", ", parameters)})";
    }

    /// <summary>A compiler's name without the number it adds to a second node of the same kind.</summary>
    private static string Unnumbered(string name)
    {
        var cut = name.LastIndexOf('_');

        return cut > 0 && cut < name.Length - 1 && name[(cut + 1)..].All(char.IsAsciiDigit) ? name[..cut] : name;
    }
}
