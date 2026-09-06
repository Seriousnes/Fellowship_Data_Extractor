using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

public static class TooltipPayload
{
    private sealed record Reading(string Member, string Property);

    private sealed record StructCall(
        string Function, int DataArgument, int Classes, Reading Duration, Reading? TickInterval);

    private sealed record RangeCall(
        string Function, int MaxRangeArgument, int MinRangeArgument, int Classes);

    private const string Hook = "GetDynamicTooltipData";
    private const string DurationOverwrite = "SetDynamicDuration";
    private const int OverwrittenData = 0;
    private const int OverwrittenDuration = 1;
    private const string TooltipStruct = "AbilityTooltipData";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";

    private static readonly Reading Cast = new("CastingDuration", "CastingData.CastingDuration");
    private static readonly Reading Channel = new("ChannelingDuration", "ChannelingData.ChannelingDuration");
    private static readonly Reading Tick = new("ChannelingTickInterval", "ChannelingData.ChannelingTickInterval");

    private const string MaxRange = "MaxRange";
    private const string MinRange = "MinRange";

    private static readonly StructCall[] StructHelpers =
    [
        new("AddDynamicCastingInfoToAbilityTooltipData", 3, 29, Cast, null),
        new("AddChannelingInfoToAbilityTooltipData", 1, 4, Channel, Tick),
        new("AddDynamicChannelingInfoToAbilityTooltipData", 3, 2, Channel, Tick),
    ];

    private static readonly RangeCall RangeHelper =
        new("AddDynamicRangeInfoToAbilityTooltipData", 2, 3, 9);

    private static readonly Dictionary<string, string> MadeMembers = new(StringComparer.Ordinal)
    {
        ["CastTime"] = Cast.Property,
        ["ChannelTime"] = Channel.Property,
        ["ChannelTickRate"] = Tick.Property,
        [MaxRange] = MaxRange,
        [MinRange] = MinRange,
    };

    public static IReadOnlyList<CurveBinding> Bindings(UObject cdo, ConstantsTable constants)
    {
        if (BareStateWalk.Compiled(cdo, Hook) is not { ScriptBytecode.Length: > 0 } body) return [];

        var walk = new BareStateWalk(body, cdo, constants, null);
        var overwritten = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var found = new List<CurveBinding>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var statement in walk.Statements())
        {
            switch (statement)
            {
                case EX_CallMath call when call.StackNode.Name.Equals(DurationOverwrite, StringComparison.Ordinal):
                    Overwritten(walk, call, overwritten);
                    continue;

                case EX_CallMath call when Helper(call.StackNode.Name) is { } helper:
                    ReadStruct(walk, cdo, constants, call, helper, overwritten, found, claimed);
                    continue;

                case EX_CallMath call
                    when call.StackNode.Name.Equals(RangeHelper.Function, StringComparison.Ordinal):
                    Add(found, claimed, MaxRange, walk.Number(Argument(call, RangeHelper.MaxRangeArgument)));
                    Add(found, claimed, MinRange, walk.Number(Argument(call, RangeHelper.MinRangeArgument)));
                    continue;

                case EX_Let { Variable: EX_StructMemberContext member } assignment
                    when Made(member) is { } property:
                    Add(found, claimed, property, walk.Number(assignment.Assignment));
                    continue;
            }
        }

        return found;
    }

    private static void Overwritten(
        BareStateWalk walk, EX_CallMath call, Dictionary<string, double> overwritten)
    {
        if (DynamicTextFloats.Name(Argument(call, OverwrittenData)) is not { } data) return;
        if (walk.Number(Argument(call, OverwrittenDuration)) is not { } replacement) return;

        overwritten[data] = replacement;
    }

    private static void ReadStruct(
        BareStateWalk walk,
        UObject cdo,
        ConstantsTable constants,
        EX_CallMath call,
        StructCall helper,
        IReadOnlyDictionary<string, double> overwritten,
        List<CurveBinding> found,
        HashSet<string> claimed)
    {
        if (DynamicTextFloats.Name(Argument(call, helper.DataArgument)) is not { } data) return;

        Add(
            found,
            claimed,
            helper.Duration.Property,
            overwritten.TryGetValue(data, out var replacement)
                ? replacement
                : Scalable(cdo, data, helper.Duration.Member, constants));

        if (helper.TickInterval is { } interval)
        {
            Add(found, claimed, interval.Property, Scalable(cdo, data, interval.Member, constants));
        }
    }

    private static KismetExpression? Argument(EX_CallMath call, int at) => call.Parameters.ElementAtOrDefault(at);

    private static StructCall? Helper(string invoked) =>
        Array.Find(StructHelpers, helper => helper.Function.Equals(invoked, StringComparison.Ordinal));

    private static string? Made(EX_StructMemberContext member) =>
        member.Property.New?.ResolvedOwner?.Name.Equals(TooltipStruct, StringComparison.Ordinal) is true &&
        DynamicTextFloats.Name(member) is { } named
            ? MadeMembers.GetValueOrDefault(named)
            : null;

    private static void Add(List<CurveBinding> found, HashSet<string> claimed, string property, double? value)
    {
        if (value is not { } number || number <= 0) return;
        if (!claimed.Add(property)) return;

        found.Add(new CurveBinding(property, Hook) { Value = number, Computed = true });
    }

    private static double? Scalable(UObject cdo, string data, string member, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(cdo, data) is not { } holder) return null;
        if (PropertyWalk.GetStruct(holder, member) is not { } scalable) return null;

        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue);
        var row = PropertyWalk.GetStruct(scalable, CurveField) is { } curve
            ? PropertyWalk.GetString(curve, RowField)
            : null;

        if (row is null || row.Length == 0 || row.Equals(NoRow, StringComparison.Ordinal)) return value;

        return constants.Lookup(row).Record?.Value is { } resolved ? (value ?? 1) * resolved : null;
    }
}
