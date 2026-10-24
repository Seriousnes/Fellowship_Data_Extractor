using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The gameplay effects an ability listens for and the per-minute proc rolls it makes. A listener is a call to
/// <c>FellowshipWaitGameplayEffectAppliedToTarget</c> (an effect the owner applies to a target) or
/// <c>FellowshipWaitGameplayEffectDurationRefresh</c> (the restart of such an effect's duration). Its parameters are
/// the owning ability, then the asset, source and target tag requirements, then <c>InTriggerOnce</c>, the optional
/// external owner and, on the applied task alone, <c>InTriggerPeriodicExecutions</c> (reflection records 0x148AC8770
/// and 0x148ACA700 in the 26162532 shipping exe). A requirements argument is a local the graph builds member by member
/// before the call, or an inline constant, so each body is read in statement order and the members last written
/// before the call are the ones it passes. A roll is a call to <c>CRRandomStreamComponent:RollProcPerMinute</c>
/// with the stream tag, the procs per minute and <c>ScaleWithHaste</c>; the procs per minute is a local the graph
/// reads from a scalable float property of the ability, resolved here per rank.
/// </summary>
public sealed class EffectTriggers(AbstractFileProvider provider, ConstantsTable constants)
{
    private const string AppliedTask = "FellowshipWaitGameplayEffectAppliedToTarget";
    private const string RefreshTask = "FellowshipWaitGameplayEffectDurationRefresh";
    private const string AppliedEvent = "EffectApplied";
    private const string RefreshEvent = "EffectDurationRefreshed";
    private const string ProcRollFunction = "RollProcPerMinute";
    private const string ScalableReads = "GetScalableFloatValue";
    private const string RequireField = "RequireTags";
    private const string IgnoreField = "IgnoreTags";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";
    private const int AssetParameter = 1;
    private const int SourceParameter = 2;
    private const int TargetParameter = 3;
    private const int OnceParameter = 4;
    private const int PeriodicParameter = 6;
    private const int StreamParameter = 0;
    private const int RateParameter = 1;
    private const int HasteParameter = 2;
    private const int RequireIndex = 0;
    private const int IgnoreIndex = 1;
    private const int MaxDepth = 16;

    public int Triggers { get; private set; }

    public int Rolls { get; private set; }

    public int UnreadRates { get; private set; }

    public (IReadOnlyList<EffectTrigger>? Triggers, IReadOnlyList<ProcRoll>? Rolls) Of(UObject? cdo)
    {
        if (cdo?.Owner is not { } package) return (null, null);

        var triggers = new List<EffectTrigger>();
        var rolls = new List<ProcRoll>();

        foreach (var body in package.GetExports().OfType<UStruct>().Where(body => body.ScriptBytecode is { Length: > 0 }))
        {
            var members = new Dictionary<(string Local, string Member), KismetExpression>();
            var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Flattened(statement, 0))
                {
                    if (Called(call) is not var (name, arguments)) continue;

                    if (name is AppliedTask or RefreshTask)
                    {
                        triggers.Add(Trigger(name, arguments, members));
                    }
                    else if (name == ProcRollFunction && Roll(cdo, arguments, locals) is { } roll)
                    {
                        rolls.Add(roll);
                    }
                }

                Remember(statement, members, locals);
            }
        }

        Triggers += triggers.Count;
        Rolls += rolls.Count;

        return (triggers.Count == 0 ? null : triggers, rolls.Count == 0 ? null : rolls);
    }

    private static EffectTrigger Trigger(
        string task, IReadOnlyList<KismetExpression> arguments, Dictionary<(string, string), KismetExpression> members)
    {
        var (assetRequired, assetIgnored) = Requirements(arguments.ElementAtOrDefault(AssetParameter), members);
        var (sourceRequired, sourceIgnored) = Requirements(arguments.ElementAtOrDefault(SourceParameter), members);
        var (targetRequired, targetIgnored) = Requirements(arguments.ElementAtOrDefault(TargetParameter), members);

        return new EffectTrigger(task == AppliedTask ? AppliedEvent : RefreshEvent)
        {
            PeriodicExecutions = task == AppliedTask ? Boolean(arguments.ElementAtOrDefault(PeriodicParameter)) : null,
            TriggerOnce = Boolean(arguments.ElementAtOrDefault(OnceParameter)) is true ? true : null,
            AssetTagsRequired = assetRequired,
            AssetTagsIgnored = assetIgnored,
            SourceTagsRequired = sourceRequired,
            SourceTagsIgnored = sourceIgnored,
            TargetTagsRequired = targetRequired,
            TargetTagsIgnored = targetIgnored,
        };
    }

    private ProcRoll? Roll(UObject cdo, IReadOnlyList<KismetExpression> arguments, Dictionary<string, KismetExpression> locals)
    {
        if (Tags(arguments.ElementAtOrDefault(StreamParameter)) is not [var stream]) return null;

        var rate = arguments.ElementAtOrDefault(RateParameter);
        if (DynamicTextFloats.Name(rate) is { } local && rate is not EX_InstanceVariable && locals.TryGetValue(local, out var held))
        {
            rate = held;
        }

        var values = Rate(cdo, rate);
        if (values is null) UnreadRates++;

        var uniform = values is { Count: > 0 } && values.All(value => value == values[0]);

        return new ProcRoll(stream)
        {
            ProcsPerMinute = uniform ? values![0] : null,
            ProcsPerMinuteByRank = values is not null && !uniform ? values : null,
            ScalesWithHaste = Boolean(arguments.ElementAtOrDefault(HasteParameter)),
        };
    }

    private IReadOnlyList<double?>? Rate(UObject cdo, KismetExpression? expression)
    {
        switch (expression)
        {
            case EX_FloatConst literal: return [literal.Value];
            case EX_DoubleConst literal: return [literal.Value];
            case EX_CallMath call when call.StackNode.Name.StartsWith(ScalableReads, StringComparison.Ordinal):
                return Rate(cdo, call.Parameters.ElementAtOrDefault(0));
            case EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } property:
                if (Struct(ClassDefaults.Inherited(cdo, property, provider)?.GenericValue) is not { } scalable) return null;

                var coefficient = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;
                var row = PropertyWalk.GetStruct(scalable, CurveField) is { } curve ? PropertyWalk.GetString(curve, RowField) : null;

                if (row is null || row.Length == 0 || row.Equals(NoRow, StringComparison.Ordinal)) return [coefficient];
                if (constants.Lookup(row).Record is not { } record) return null;

                return record.Values is { Length: > 1 } byRank
                    ? [.. byRank.Select(value => value * coefficient)]
                    : [record.Value * coefficient];
            default:
                return null;
        }
    }

    private static (IReadOnlyList<string>? Required, IReadOnlyList<string>? Ignored) Requirements(
        KismetExpression? argument, Dictionary<(string, string), KismetExpression> members)
    {
        if (argument is EX_StructConst inline)
        {
            return (Tags(inline.Properties.ElementAtOrDefault(RequireIndex)), Tags(inline.Properties.ElementAtOrDefault(IgnoreIndex)));
        }

        if (DynamicTextFloats.Name(argument) is not { } local) return (null, null);

        return (
            members.TryGetValue((local, RequireField), out var required) ? Tags(required) : null,
            members.TryGetValue((local, IgnoreField), out var ignored) ? Tags(ignored) : null);
    }

    private static void Remember(
        KismetExpression statement,
        Dictionary<(string, string), KismetExpression> members,
        Dictionary<string, KismetExpression> locals)
    {
        if (statement is not EX_Let { Assignment: { } value } let) return;

        if (let.Variable is EX_StructMemberContext member)
        {
            if (DynamicTextFloats.Name(member) is { } field && DynamicTextFloats.Name(member.StructExpression) is { } held)
            {
                members[(held, field)] = value;
            }
            return;
        }

        if (let.Variable is not EX_InstanceVariable && DynamicTextFloats.Name(let.Variable) is { } local) locals[local] = value;
    }

    private static IReadOnlyList<string>? Tags(KismetExpression? expression)
    {
        var tags = Flattened(expression, 0)
            .OfType<EX_NameConst>()
            .Select(name => name.Value.Text)
            .Where(name => !name.Equals(NoRow, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return tags.Length == 0 ? null : tags;
    }

    private static bool? Boolean(KismetExpression? expression) => expression switch
    {
        EX_True => true,
        EX_False => false,
        _ => null,
    };

    private static (string Name, IReadOnlyList<KismetExpression> Arguments)? Called(KismetExpression expression) =>
        expression switch
        {
            EX_CallMath call => (call.StackNode.Name, call.Parameters),
            EX_FinalFunction call => (call.StackNode.Name, call.Parameters),
            _ => null,
        };

    private static IEnumerable<KismetExpression> Flattened(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        yield return expression;

        var children = expression switch
        {
            EX_ArrayConst array => array.Elements,
            _ => ResourceGains.Children(expression),
        };

        foreach (var child in children)
        {
            foreach (var nested in Flattened(child, depth + 1)) yield return nested;
        }
    }

    private static IPropertyHolder? Struct(object? value) => value switch
    {
        FScriptStruct { StructType: IPropertyHolder inner } => inner,
        IPropertyHolder direct => direct,
        _ => null,
    };
}
