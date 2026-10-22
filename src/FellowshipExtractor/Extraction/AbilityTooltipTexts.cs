using System.Globalization;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;

using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK;

namespace FellowshipExtractor.Extraction;

public sealed class AbilityTooltipTexts
{
    private const string Widget = "tooltip_ability";
    private const string RangeText = "range_text";
    private const string RangeUnit = "range_unit";
    private const string RangeValue = "range_value";
    private const string CastText = "cast_time_text";
    private const string CastUnit = "cast_time_unit";
    private const string CooldownText = "cooldown_text";
    private const string CooldownUnit = "cooldown_unit";
    private const string Divide = "Divide_DoubleDouble";
    private const string GreaterDouble = "Greater_DoubleDouble";
    private const string GreaterInt = "Greater_IntInt";
    private const string Lower = "TextToLower";

    public AbilityTooltipText Text { get; }

    public AbilityTooltipTexts(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var asset = assets.FirstOrDefault(asset => asset.AssetName.Text.Equals(Widget, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No cooked asset is named {Widget}, so no ability tooltip text reads from it.");

        if (!provider.TryLoadPackage(asset.PackageName.Text, out var package))
        {
            throw new InvalidOperationException($"{asset.PackageName.Text} does not load, so no ability tooltip text reads from it.");
        }

        var functions = package.GetExports().OfType<UStruct>()
            .Where(function => function.ScriptBytecode is { Length: > 0 })
            .ToDictionary(function => function.Name, function => function.ScriptBytecode!, StringComparer.Ordinal);

        Text = new AbilityTooltipText
        {
            UnitsPerYard = Constant(functions, RangeValue, Divide),
            MeleeMaximum = Constant(functions, RangeUnit, GreaterDouble),
            PrintedAboveYards = (int)Constant(functions, RangeValue, GreaterInt),
            Range = Keyed(functions, RangeText, "RANGE").ToLower(CultureInfo.InvariantCulture),
            MeleeRange = Keyed(functions, RangeText, "MELEE_RANGE"),
            Self = Keyed(functions, RangeText, "SELF_TARGET_STR"),
            RangeUnit = Literal(functions, RangeUnit),
            Instant = Keyed(functions, CastText, "INSTANT"),
            Cast = Keyed(functions, CastText, "CAST"),
            Channel = Keyed(functions, CastText, "CHANNEL"),
            ChargeUp = Keyed(functions, CastText, "CHARGE_UP"),
            CastUnit = Literal(functions, CastUnit),
            Cooldown = Keyed(functions, CooldownText, "COOLDOWN"),
            CooldownUnit = Literal(functions, CooldownUnit),
        };

        if (!Lowered(functions, RangeText))
        {
            throw new InvalidOperationException($"{Widget}.{RangeText} no longer calls {Lower}, so the range word's case is unread.");
        }
    }

    private static IEnumerable<KismetExpression> All(IReadOnlyDictionary<string, KismetExpression[]> functions, string function) =>
        functions.TryGetValue(function, out var bytecode)
            ? bytecode.SelectMany(statement => BytecodeGates.Descendants(statement, 0))
            : throw new InvalidOperationException($"{Widget} has no function {function}.");

    private static double Constant(IReadOnlyDictionary<string, KismetExpression[]> functions, string function, string called)
    {
        var constants = All(functions, function)
            .OfType<EX_CallMath>()
            .Where(call => call.StackNode.Name == called)
            .Select(call => call.Parameters.ElementAtOrDefault(1) switch
            {
                EX_DoubleConst number => number.Value,
                EX_IntConst number => number.Value,
                EX_FloatConst number => number.Value,
                _ => (double?)null,
            })
            .OfType<double>()
            .Where(value => value != 0)
            .Distinct()
            .ToArray();

        return constants is [var only]
            ? only
            : throw new InvalidOperationException(
                $"{Widget}.{function} compares by {called} against {constants.Length} non-zero constants, not one.");
    }

    private static string Keyed(IReadOnlyDictionary<string, KismetExpression[]> functions, string function, string key)
    {
        foreach (var text in All(functions, function).OfType<EX_TextConst>())
        {
            if (text.Value is { KeyString: EX_StringConst { Value: { } written }, TableIdString: EX_StringConst { Value: { } table } } &&
                written == key &&
                StringTables.Find(table, key) is { Length: > 0 } printed)
            {
                return printed;
            }
        }

        throw new InvalidOperationException($"{Widget}.{function} prints no string table entry {key}.");
    }

    private static string Literal(IReadOnlyDictionary<string, KismetExpression[]> functions, string function)
    {
        foreach (var text in All(functions, function).OfType<EX_TextConst>())
        {
            if (text.Value?.SourceString is not EX_StringConst { Value: { Length: > 0 } source }) continue;

            return text.Value.KeyString is EX_StringConst { Value: { } key } &&
                   text.Value.Namespace is EX_StringConst { Value: var space } &&
                   StringTables.Find(space ?? string.Empty, key) is { Length: > 0 } localized
                ? localized
                : source;
        }

        throw new InvalidOperationException($"{Widget}.{function} prints no literal text.");
    }

    private static bool Lowered(IReadOnlyDictionary<string, KismetExpression[]> functions, string function) =>
        All(functions, function).OfType<EX_CallMath>().Any(call => call.StackNode.Name == Lower);
}
