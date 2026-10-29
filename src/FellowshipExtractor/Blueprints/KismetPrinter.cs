using System.Globalization;

using CUE4Parse.GameTypes._2XKO.Kismet;
using CUE4Parse.GameTypes.Borderlands4.Kismet;
using CUE4Parse.GameTypes.DFHO.Kismet;
using CUE4Parse.GameTypes.OtherGames.Objects;
using CUE4Parse.GameTypes.WuWa.Kismet;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Blueprints;

public static class KismetPrinter
{
    public const string Unhandled = "<unhandled ";

    public static string Label(long offset) => $"L{offset}";

    public static string Expression(KismetExpression? expression) => expression switch
    {
        null => "null",

        EX_LocalVariable local => Pointer(local.Variable),
        EX_LocalOutVariable output => Pointer(output.Variable),
        EX_InstanceVariable instance => $"self.{Pointer(instance.Variable)}",
        EX_DefaultVariable defaults => $"default.{Pointer(defaults.Variable)}",
        EX_ClassSparseDataVariable sparse => $"sparse.{Pointer(sparse.Variable)}",

        EX_Self => "self",
        EX_NoObject => "null",
        EX_NoInterface => "null",
        EX_Nothing => "nothing",
        EX_NothingInt32 nothing => $"nothing({nothing.Value})",
        EX_True => "true",
        EX_False => "false",
        EX_IntZero => "0",
        EX_IntOne => "1",
        EX_IntConst number => number.Value.ToString(CultureInfo.InvariantCulture),
        EX_Int64Const number => number.Value.ToString(CultureInfo.InvariantCulture),
        EX_UInt64Const number => number.Value.ToString(CultureInfo.InvariantCulture),
        EX_ByteConst number => number.Value.ToString(CultureInfo.InvariantCulture),
        EX_IntConstByte number => number.Value.ToString(CultureInfo.InvariantCulture),
        EX_FloatConst number => Number(number.Value),
        EX_DoubleConst number => Number(number.Value),
        EX_StringConst text => Quoted(text.Value),
        EX_UnicodeStringConst text => Quoted(text.Value),
        EX_NameConst name => $"'{name.Value.Text}'",
        EX_TextConst text => Text(text.Value),
        EX_ObjectConst constant => Object(constant.Value),
        EX_SoftObjectConst soft => $"SoftObject({Expression(soft.Value)})",
        EX_FieldPathConst field => $"FieldPath({Expression(field.Value)})",
        EX_PropertyConst property => $"Property({Pointer(property.Property)})",
        EX_BitFieldConst bit => $"{Pointer(bit.InnerProperty)}:{bit.ConstValue}",
        EX_SkipOffsetConst skip => $"skip_offset({Label(skip.Value)})",
        EX_VectorConst vector => Vector("FVector", vector.Value),
        EX_Vector3fConst vector => Vector("FVector3f", vector.Value),
        EX_RotationConst rotation => Rotator(rotation.Value),
        EX_TransformConst transform => Transform(transform.Value),
        EX_StructConst structure => $"{Object(structure.Struct, shortName: true)}{{{List(structure.Properties)}}}",
        EX_ArrayConst array => $"[{List(array.Elements)}]",
        EX_SetConst set => $"set{{{List(set.Elements)}}}",
        EX_MapConst map => $"map{{{Pairs(map.Elements)}}}",

        EX_SetArray array => $"{Expression(array.AssigningProperty)} = [{List(array.Elements)}]",
        EX_SetSet set => $"{Expression(set.SetProperty)} = set{{{List(set.Elements)}}}",
        EX_SetMap map => $"{Expression(map.MapProperty)} = map{{{Pairs(map.Elements)}}}",

        EX_Let let => $"{Expression(let.Variable)} = {Expression(let.Assignment)}",
        EX_LetBase let => $"{Expression(let.Variable)} = {Expression(let.Assignment)}",
        EX_LetValueOnPersistentFrame let =>
            $"persistent.{Pointer(let.DestinationProperty)} = {Expression(let.AssignmentExpression)}",

        EX_Context_FailSilent context => $"{Expression(context.ObjectExpression)}?.{Member(context.ContextExpression)}",
        EX_ClassContext context => $"{Expression(context.ObjectExpression)}::{Member(context.ContextExpression)}",
        EX_Context context => $"{Expression(context.ObjectExpression)}.{Member(context.ContextExpression)}",
        EX_InterfaceContext context => Expression(context.InterfaceValue),
        EX_StructMemberContext member => $"{Expression(member.StructExpression)}.{Pointer(member.Property)}",
        EX_ArrayGetByRef element => $"{Expression(element.ArrayVariable)}[{Expression(element.ArrayIndex)}]",

        EX_CallMath call => $"{Function(call.StackNode, withOwner: true)}({List(call.Parameters)})",
        EX_LocalFinalFunction call => $"{Function(call.StackNode, withOwner: false)}({List(call.Parameters)})",
        EX_FinalFunction call => $"{Function(call.StackNode, withOwner: false)}({List(call.Parameters)})",
        EX_LocalVirtualFunction call => $"{Name(call.VirtualFunctionName)}({List(call.Parameters)})",
        EX_VirtualFunction call => $"{Name(call.VirtualFunctionName)}({List(call.Parameters)})",

        EX_Cast cast => $"{CastName(cast.ConversionType)}({Expression(cast.Target)})",
        EX_CastBase cast => $"{cast.GetType().Name[3..]}<{Object(cast.ClassPtr, shortName: true)}>({Expression(cast.Target)})",

        EX_InstanceDelegate instance => $"delegate({Name(instance.FunctionName)})",
        EX_BindDelegate bind =>
            $"{Expression(bind.Delegate)}.Bind({Expression(bind.ObjectTerm)}, {Name(bind.FunctionName)})",
        EX_AddMulticastDelegate add => $"{Expression(add.Delegate)} += {Expression(add.DelegateToAdd)}",
        EX_RemoveMulticastDelegate remove => $"{Expression(remove.Delegate)} -= {Expression(remove.DelegateToAdd)}",
        EX_ClearMulticastDelegate clear => $"{Expression(clear.DelegateToClear)}.Clear()",
        EX_CallMulticastDelegate call =>
            $"{Expression(call.Delegate)}.Broadcast({List(call.Parameters)})",

        EX_SwitchValue choice => Switch(choice),

        EX_Return { ReturnExpression: null or EX_Nothing } => "return",
        EX_Return returned => $"return {Expression(returned.ReturnExpression)}",
        EX_JumpIfNot jump => $"if ({Negated(Expression(jump.BooleanExpression))}) goto {Label(jump.CodeOffset)}",
        EX_Skip skip => $"skip to {Label(skip.CodeOffset)} ({Expression(skip.SkipExpression)})",
        EX_Jump jump => $"goto {Label(jump.CodeOffset)}",
        EX_ComputedJump jump => $"goto computed({Expression(jump.CodeOffsetExpression)})",
        EX_PushExecutionFlow push => $"push_flow {Label(push.PushingAddress)}",
        EX_PopExecutionFlowIfNot pop => $"if ({Negated(Expression(pop.BooleanExpression))}) pop_flow",
        EX_PopExecutionFlow => "pop_flow",

        EX_Assert assert => $"assert({Expression(assert.AssertExpression)})",
        EX_Breakpoint => "breakpoint",
        EX_Tracepoint => "tracepoint",
        EX_WireTracepoint => "wire_tracepoint",
        EX_InstrumentationEvent instrumentation =>
            instrumentation.EventName is { } eventName
                ? $"instrumentation {instrumentation.EventType} {eventName.Text}"
                : $"instrumentation {instrumentation.EventType}",

        EX_AutoRtfmTransact transact =>
            $"autortfm_transact {transact.Id} until {Label(transact.CodeOffset)} ({List(transact.Parameters)})",
        EX_AutoRtfmStopTransact stop => $"autortfm_stop_transact {stop.Id} {stop.Mode}",
        EX_AutoRtfmAbortIfNot => "autortfm_abort_if_not",
        EX_AutoRtfmAbort => "autortfm_abort",

        EX_EndOfScript => "end",
        EX_EndFunctionParms => "end_function_parms",
        EX_EndParmValue => "end_parm_value",
        EX_EndStructConst => "end_struct_const",
        EX_EndArray => "end_array",
        EX_EndArrayConst => "end_array_const",
        EX_EndSet => "end_set",
        EX_EndSetConst => "end_set_const",
        EX_EndMap => "end_map",
        EX_EndMapConst => "end_map_const",
        EX_DeprecatedOp4A => "deprecated_op_4a",

        EX_WuWaInstr1 instruction => $"WuWaInstr1({Vector("FVector", instruction.Pos1)}, {Vector("FVector", instruction.Pos2)})",
        EX_WuWaInstr2 instruction =>
            $"WuWaInstr2(FQuat({Number(instruction.Rotation.X)}, {Number(instruction.Rotation.Y)}, {Number(instruction.Rotation.Z)}, {Number(instruction.Rotation.W)}), " +
            $"{Vector("FVector", instruction.Pos1)}, {Vector("FVector", instruction.Pos2)}, {Vector("FVector", instruction.Scale)})",
        EX_DFInstr instruction => $"DFInstr({Expression(instruction.Left)}, {Expression(instruction.Right)})",
        EX_PalworldInstr1 instruction => $"PalworldInstr1({Quoted(instruction.Value)})",
        EX_GbxDefPtr pointer => $"GbxDefPtr({pointer.Value})",
        EX_GameDataHandle handle => $"GameDataHandle({handle.Value})",
        EX_DamageSourceContainer container => $"DamageSourceContainer({Object(container.Type)}, {container.Value})",
        EX_FixedPointConst number => $"FixedPoint({number.Value})",

        _ => $"{Unhandled}{expression.GetType().Name}>",
    };

    private static string Member(KismetExpression? expression) => expression switch
    {
        EX_InstanceVariable instance => Pointer(instance.Variable),
        EX_Context_FailSilent context => $"{Member(context.ObjectExpression)}?.{Member(context.ContextExpression)}",
        EX_ClassContext context => $"{Member(context.ObjectExpression)}::{Member(context.ContextExpression)}",
        EX_Context context => $"{Member(context.ObjectExpression)}.{Member(context.ContextExpression)}",
        _ => Expression(expression),
    };

    public static string Pointer(FKismetPropertyPointer? pointer)
    {
        if (pointer?.New?.Path is { Length: > 0 } path) return string.Join('.', path.Select(p => p.Text));
        if (pointer?.Old is { IsNull: false } old) return old.Name;
        return "unnamed";
    }

    public static string Object(FPackageIndex? index, bool shortName = false)
    {
        if (index is null || index.IsNull) return "null";
        if (shortName) return index.Name;
        return index.ResolvedObject?.GetPathName() ?? index.Name;
    }

    public static string Negated(string condition) =>
        condition.StartsWith('!') && !condition.Contains(' ') ? condition[1..]
        : condition.Contains(' ') ? $"!({condition})"
        : $"!{condition}";

    private static string Function(FPackageIndex? node, bool withOwner)
    {
        if (node is null || node.IsNull) return "null";
        if (withOwner && node.ResolvedObject?.Outer?.Name.Text is { Length: > 0 } owner) return $"{owner}::{node.Name}";
        return node.Name;
    }

    private static string Name(FName name) => name.IsNone ? "None" : name.Text;

    private static string List(KismetExpression[]? expressions) =>
        expressions is null ? string.Empty : string.Join(", ", expressions.Select(Expression));

    private static string Pairs(KismetExpression[]? elements)
    {
        if (elements is null) return string.Empty;

        var pairs = new List<string>();
        for (var at = 0; at < elements.Length; at += 2)
        {
            pairs.Add(at + 1 < elements.Length
                ? $"{Expression(elements[at])}: {Expression(elements[at + 1])}"
                : Expression(elements[at]));
        }

        return string.Join(", ", pairs);
    }

    private static string Switch(EX_SwitchValue choice)
    {
        var cases = (choice.Cases ?? [])
            .Select(entry => $"{Expression(entry.CaseIndexValueTerm)}: {Expression(entry.CaseTerm)}")
            .Append($"default: {Expression(choice.DefaultTerm)}");

        return $"switch({Expression(choice.IndexTerm)}) {{{string.Join(", ", cases)}}}";
    }

    private static string CastName(ECastToken token)
    {
        var name = token.ToString();
        return name.StartsWith("CST_", StringComparison.Ordinal) ? name[4..] : name;
    }

    private static string Text(FScriptText? text)
    {
        if (text is null) return "Text()";

        return text.TextLiteralType switch
        {
            EBlueprintTextLiteralType.Empty => "Text()",
            EBlueprintTextLiteralType.StringTableEntry =>
                TableEntry(Object(text.StringTableAsset), text.TableIdString, text.KeyString),
            EBlueprintTextLiteralType.InvariantText or EBlueprintTextLiteralType.LiteralString =>
                $"Text({Expression(text.SourceString)})",
            _ => $"Text({Expression(text.Namespace)}, {Expression(text.KeyString)}, {Expression(text.SourceString)})",
        };
    }

    private static string TableEntry(string asset, KismetExpression? table, KismetExpression? key)
    {
        var written = table is EX_StringConst { Value: { } tableId } && key is EX_StringConst { Value: { } keyId }
            ? StringTables.Find(tableId, keyId)
            : null;

        var entry = $"TableText({asset}, {Expression(table)}, {Expression(key)}";
        return written is null ? $"{entry})" : $"{entry}) = {Quoted(written)}";
    }

    private static string Quoted(string? value) =>
        value is null
            ? "null"
            : $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")}\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Vector(string type, FVector value) =>
        $"{type}({Number(value.X)}, {Number(value.Y)}, {Number(value.Z)})";

    private static string Rotator(FRotator value) =>
        $"FRotator({Number(value.Pitch)}, {Number(value.Yaw)}, {Number(value.Roll)})";

    private static string Transform(FTransform value) =>
        $"FTransform(FQuat({Number(value.Rotation.X)}, {Number(value.Rotation.Y)}, {Number(value.Rotation.Z)}, {Number(value.Rotation.W)}), " +
        $"{Vector("FVector", value.Translation)}, {Vector("FVector", value.Scale3D)})";
}
