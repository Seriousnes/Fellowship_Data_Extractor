using System.Globalization;
using System.Text.RegularExpressions;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record ModifierCandidate
{
    public required string Row { get; init; }

    public double? Value { get; init; }

    public double? ValueLow { get; init; }

    public double? ValueHigh { get; init; }

    public string? Attribute { get; init; }

    public string? Op { get; init; }

    public double? Coefficient { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<string> Uses { get; init; } = [];

    public string? Text { get; init; }
}

public enum ModifierBasis
{
    Attribute,
    Property,
    Bytecode,
    Keyword,
}

public sealed record ModifierClass(ModifierType Type, ModifierBasis Basis, string Evidence)
{
    public double? Value { get; init; }

    public double? ValueLow { get; init; }

    public double? ValueHigh { get; init; }

    public string? Attribute { get; init; }

    public string? Conversion { get; init; }
}

public static partial class ModifierTypes
{
    private const string Executions = "Executions[";
    private const string Modifiers = "Modifiers[";
    private const string DurationMagnitude = "DurationMagnitude";
    private const string StackLimit = "StackCountMax";
    private const string Coefficient = "Coefficient";

    private const string AddBase = "Additive";
    private const string AddFinal = "AddFinal";
    private const string MultiplyAdditive = "Multiplicitive";
    private const string MultiplyCompound = "MultiplyCompound";
    private const string Divide = "Division";
    private const string Override = "Override";

    private const string DamageScale = "DamageScale";
    private const string HealScale = "HealScale";
    private const string CritChance = "CritChance";
    private const string CritMultiplier = "CritMultiplier";

    private const string CastingHook = "return:ModifyCastingDuration";
    private const string ChargesHook = "return:GetMaximumCharges";
    private const string CooldownReducer = "ReduceCooldownForAbility#";
    private const string DurationExtender = "IncreaseActiveEffectDuration#";
    private const string DynamicDuration = "SetDynamicDuration#";
    private const string TickPeriod = "TickPeriodScaler";
    private const string PeriodScaler = "PeriodScaler";
    private const string SetConsumer = "set:";
    private const string Clamp = "Clamp";
    private const string SetByCaller = "AssignTagSetByCallerMagnitude#";
    private const string SetByCallerDuration = "[SetByCaller.Duration]";
    private const string DistanceFilter = "AddFilter_Distance#";
    private const string DamageScaleTag = "<tag=SetByCaller.DamageScale>";
    public const string CountedScale = "the damage scale is 1 plus the value for each count";

    private static readonly string[] RandomRolls = ["ControlledRandomBool#", "RandomBoolWithWeight#"];
    private static readonly string[] Waits = ["WaitDelayCR#", "Delay#", "K2_SetTimerDelegate#", "K2_SetTimer#"];
    private const string PerMinute = "RollProcPerMinute#";

    private static readonly HashSet<ModifierType> Multipliers =
    [
        ModifierType.DamageMultiplier,
        ModifierType.HealingMultiplier,
        ModifierType.AttributeMultiplier,
        ModifierType.CastTimeMultiplier,
        ModifierType.DurationMultiplier,
        ModifierType.CritDamageMultiplier,
        ModifierType.TickRateMultiplier,
    ];

    public static bool IsMultiplier(ModifierType type) => Multipliers.Contains(type);

    private static readonly HashSet<ModifierType> Seconds =
    [
        ModifierType.CastTime,
        ModifierType.CastTimeIncrease,
        ModifierType.CastTimeReduction,
        ModifierType.Cooldown,
        ModifierType.CooldownReduction,
        ModifierType.Duration,
        ModifierType.DurationIncrease,
        ModifierType.Period,
    ];

    private static bool PrintsShare(ModifierCandidate candidate)
    {
        if (HeroNames.LastSegment(candidate.Row).Contains("Percent", StringComparison.OrdinalIgnoreCase)) return true;
        if (candidate.Value is not { } value || candidate.Text is not { Length: > 0 } text) return false;

        return PrintsPercent(text, value);
    }

    /// <summary>True when <paramref name="text"/> prints <paramref name="value"/> times 100 as a percent.</summary>
    public static bool PrintsPercent(string text, double value) =>
        PercentPattern().Matches(text).Any(match =>
            Near(double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture), value * 100));

    /// <summary>
    /// True when an execution multiplying <paramref name="attribute"/> by the magnitude reads it as a coefficient of
    /// that attribute. An execution scales damage, healing and critical damage by a factor instead.
    /// </summary>
    public static bool IsCoefficient(string attribute, string op, bool executed) =>
        executed && op is MultiplyAdditive or MultiplyCompound && attribute is not (DamageScale or HealScale or CritMultiplier);

    public static ModifierClass? Classify(ModifierCandidate candidate)
    {
        if (candidate.Value is null && candidate.ValueLow is null && candidate.Coefficient is null) return null;

        if (candidate.Attribute is null && Stated(candidate) is var (attribute, op))
        {
            candidate = candidate with { Attribute = attribute, Op = op };
        }

        var proposed = FromAttribute(candidate)
            ?? FromProperty(candidate)
            ?? FromBytecode(candidate)
            ?? (Unmapped(candidate) ? null : FromKeyword(candidate));

        if (proposed is null) return null;
        if (proposed.Basis == ModifierBasis.Keyword && Seconds.Contains(proposed.Type) && PrintsShare(candidate)) return null;

        var valued = proposed with
        {
            Value = proposed.Value ?? candidate.Value,
            ValueLow = proposed.ValueLow ?? candidate.ValueLow,
            ValueHigh = proposed.ValueHigh ?? candidate.ValueHigh,
        };

        return IsMultiplier(valued.Type) && valued.Conversion is null ? Factor(candidate, valued) : valued;
    }

    private static IEnumerable<string> Uses(ModifierCandidate candidate, string prefix) =>
        candidate.Uses
            .Where(use => use.StartsWith(prefix, StringComparison.Ordinal))
            .Select(use => use.IndexOf(RowSinks.GateMark, StringComparison.Ordinal) is var mark and >= 0
                ? use[prefix.Length..mark]
                : use[prefix.Length..]);

    private static IEnumerable<string> Properties(ModifierCandidate candidate) =>
        Uses(candidate, RowSinks.PropertyUse)
            .Concat(Uses(candidate, RowSinks.VariableUse).Where(path =>
                path.Contains('.') && !UserStructMember().IsMatch(path)));

    private static (string Attribute, string? Op)? Stated(ModifierCandidate candidate)
    {
        var stated = Uses(candidate, RowSinks.AttributeUse)
            .Select(use => use.Split('|'))
            .Select(parts => (Attribute: parts[0], Op: parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null))
            .Distinct()
            .ToArray();

        return stated is [var only] ? only : null;
    }

    private static bool Executed(ModifierCandidate candidate) =>
        Properties(candidate).Any(path => path.StartsWith(Executions, StringComparison.Ordinal)) ||
        Uses(candidate, RowSinks.AttributeUse).Any(use =>
            use.StartsWith($"{candidate.Attribute}|", StringComparison.Ordinal) &&
            use.EndsWith($"|{RowSinks.Execution}", StringComparison.Ordinal));

    private static bool Unmapped(ModifierCandidate candidate) =>
        Properties(candidate).Any(path =>
            !path.StartsWith(Executions, StringComparison.Ordinal) &&
            !path.StartsWith(Modifiers, StringComparison.Ordinal));

    private static ModifierClass? FromAttribute(ModifierCandidate candidate)
    {
        if (candidate.Attribute is not { } attribute) return null;

        var evidence = $"{attribute} {candidate.Op}".Trim();

        if (attribute == DurationMagnitude) return new(ModifierType.Duration, ModifierBasis.Attribute, evidence);
        var op = candidate.Op ?? AddBase;

        var execution = Executed(candidate);
        var scope = execution ? "execution" : "modifier";
        evidence = $"{scope} {evidence}";

        var multiplies = op is MultiplyAdditive or MultiplyCompound;
        var adds = op is AddBase or AddFinal;

        ModifierClass Typed(ModifierType type, string? named = null) =>
            new(type, ModifierBasis.Attribute, evidence) { Attribute = named };

        if (op == Override) return Typed(ModifierType.AttributeOverride, attribute);

        if (execution)
        {
            if (attribute == DamageScale && (multiplies || op == Divide))
            {
                return Divided(Typed(ModifierType.DamageMultiplier), candidate, op);
            }

            if (attribute == HealScale && (multiplies || op == Divide))
            {
                return Divided(Typed(ModifierType.HealingMultiplier), candidate, op);
            }

            if (attribute == CritChance && adds) return Typed(ModifierType.CritChanceIncrease);
            if (attribute == CritMultiplier && multiplies) return Divided(Typed(ModifierType.CritDamageMultiplier), candidate, op);
            if (multiplies) return Typed(ModifierType.Coefficient, attribute);
        }

        if (multiplies || op == Divide) return Divided(Typed(ModifierType.AttributeMultiplier, attribute), candidate, op);
        if (adds) return Typed(ModifierType.AttributeIncrease, attribute);

        return null;
    }

    private static ModifierClass Divided(ModifierClass typed, ModifierCandidate candidate, string op)
    {
        var direct = Properties(candidate).Any(path =>
            path.StartsWith(Executions, StringComparison.Ordinal) || path.StartsWith(Modifiers, StringComparison.Ordinal));

        var computed = Uses(candidate, RowSinks.SinkUse).Any(sink => sink.Contains('|') && sink.Contains('['));

        if (!direct && computed) return typed with { Evidence = $"{typed.Evidence} through a computed SetByCaller magnitude" };

        if (op is MultiplyAdditive or MultiplyCompound) return typed with { Conversion = $"{op} magnitude is the factor" };

        return typed with
        {
            Value = candidate.Value is { } value and not 0 ? 1 / value : null,
            ValueLow = candidate.ValueHigh is { } high and not 0 ? 1 / high : null,
            ValueHigh = candidate.ValueLow is { } low and not 0 ? 1 / low : null,
            Conversion = "Division divides by the magnitude, so the factor is its reciprocal",
        };
    }

    private static ModifierClass? FromProperty(ModifierCandidate candidate)
    {
        foreach (var path in Properties(candidate))
        {
            if (path.StartsWith(Executions, StringComparison.Ordinal) || path.StartsWith(Modifiers, StringComparison.Ordinal))
            {
                continue;
            }

            if (path.Equals(StackLimit, StringComparison.Ordinal))
            {
                return new(ModifierType.MaxStacks, ModifierBasis.Property, path);
            }

            if (path.EndsWith($".{Coefficient}", StringComparison.Ordinal)) continue;

            if (Numbers.Field(path) is { } field && FieldTypes.GetValueOrDefault(field) is { } type)
            {
                return new(type, ModifierBasis.Property, path);
            }
        }

        return null;
    }

    private static readonly Dictionary<string, ModifierType> FieldTypes = new(StringComparer.Ordinal)
    {
        ["castTime"] = ModifierType.CastTime,
        ["channelTime"] = ModifierType.ChannelTime,
        ["channelTick"] = ModifierType.ChannelTick,
        ["cooldown"] = ModifierType.Cooldown,
        ["range"] = ModifierType.Range,
        ["minRange"] = ModifierType.MinRange,
        ["radius"] = ModifierType.Radius,
        ["duration"] = ModifierType.Duration,
        ["period"] = ModifierType.Period,
    };

    private readonly record struct Combination(string Op, int At, IReadOnlyList<string> Others);

    private static IReadOnlyList<Combination> Steps(string sink)
    {
        var bar = sink.IndexOf('|');
        if (bar < 0) return [];

        return [.. sink[(bar + 1)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(step =>
            {
                var match = StepPattern().Match(step);

                return new Combination(
                    match.Groups["op"].Value,
                    int.Parse(match.Groups["at"].Value, CultureInfo.InvariantCulture),
                    match.Groups["others"].Value.Split(';'));
            })];
    }

    private static string Consumer(string sink) => sink.IndexOf('|') is var bar and >= 0 ? sink[..bar] : sink;

    private static (string Op, int Sign)? Combined(IReadOnlyList<Combination> steps)
    {
        var sign = 1;

        for (var at = steps.Count - 1; at >= 0; at--)
        {
            var step = steps[at];

            if (step.Op.StartsWith("Add_", StringComparison.Ordinal)) return ("Add", sign);
            if (step.Op.StartsWith("Subtract_", StringComparison.Ordinal))
            {
                if (step.At == 1) return ("Add", -sign);

                if (steps.Take(at).Any(outer => outer.Op.StartsWith("Subtract_", StringComparison.Ordinal) && outer.At == 1))
                {
                    sign = -sign;
                }

                return ("Add", sign);
            }

            if (step.Op.StartsWith("Multiply", StringComparison.Ordinal)) return ("Multiply", 1);
            if (step.Op.StartsWith("Divide_", StringComparison.Ordinal) || step.Op.StartsWith("SafeDivide", StringComparison.Ordinal))
            {
                return step.At == 0 ? ("Multiply", 1) : ("Divide", 1);
            }
        }

        return null;
    }

    private static ModifierClass? FromBytecode(ModifierCandidate candidate)
    {
        foreach (var sink in Uses(candidate, RowSinks.SinkUse))
        {
            var consumer = Consumer(sink);
            var steps = Steps(sink);
            var combined = Combined(steps);

            ModifierClass Typed(ModifierType type) => new(type, ModifierBasis.Bytecode, sink);

            if (consumer == CastingHook)
            {
                if (combined is ("Add", > 0)) return Typed(ModifierType.CastTimeIncrease);
                if (combined is ("Add", < 0)) return Typed(ModifierType.CastTimeReduction);
                if (combined is ("Multiply", _)) return Typed(ModifierType.CastTimeMultiplier);
            }

            if (consumer == ChargesHook)
            {
                if (combined is null) return Typed(ModifierType.Charges);
            }

            if (consumer.StartsWith(CooldownReducer, StringComparison.Ordinal) && combined is null or ("Add", _))
            {
                return Typed(ModifierType.CooldownReduction);
            }

            if (consumer.StartsWith(DurationExtender, StringComparison.Ordinal) && combined is null or ("Add", _))
            {
                return Typed(ModifierType.DurationIncrease);
            }
            if ((consumer.Contains(TickPeriod, StringComparison.Ordinal) ||
                 (consumer.StartsWith(SetConsumer, StringComparison.Ordinal) && consumer.Contains(PeriodScaler, StringComparison.Ordinal))) &&
                steps.Any(step => (step.Op.StartsWith("Divide_", StringComparison.Ordinal) ||
                                   step.Op.StartsWith("SafeDivide", StringComparison.Ordinal)) &&
                                  step.At == 1 && step.Others.Contains("1")))
            {
                return Typed(ModifierType.TickRateMultiplier) with
                {
                    Conversion = $"the period is divided by the value ({sink})",
                };
            }

            if (steps.Any(step => step.Op.StartsWith(Clamp, StringComparison.Ordinal) && step.At == 2))
            {
                return Typed(ModifierType.StacksPerApplicationMax);
            }

            if (consumer.StartsWith(DynamicDuration, StringComparison.Ordinal))
            {
                if (combined is ("Add", > 0)) return Typed(ModifierType.DurationIncrease);
                if (combined is null) return Typed(ModifierType.Duration);
            }

            if (consumer.StartsWith(SetByCaller, StringComparison.Ordinal) &&
                consumer.EndsWith(SetByCallerDuration, StringComparison.Ordinal))
            {
                return combined is null ? Typed(ModifierType.Duration)
                    : combined is ("Add", > 0) ? Typed(ModifierType.DurationIncrease)
                    : null;
            }

            if (Array.Exists(RandomRolls, roll => consumer.StartsWith(roll, StringComparison.Ordinal)) && combined is null)
            {
                return Typed(ModifierType.ProcChance);
            }

            if (consumer.StartsWith(PerMinute, StringComparison.Ordinal) && combined is null)
            {
                return Typed(ModifierType.ProcsPerMinute);
            }

            if (Array.Exists(Waits, wait => consumer.StartsWith(wait, StringComparison.Ordinal)) && combined is null)
            {
                return Typed(ModifierType.Delay);
            }

            if (consumer.StartsWith(DistanceFilter, StringComparison.Ordinal) && combined is null)
            {
                return Typed(ModifierType.Radius);
            }

            if (consumer.Contains(DamageScaleTag, StringComparison.Ordinal) &&
                steps is [{ } outer, { } inner] &&
                outer.Op.StartsWith("Add_", StringComparison.Ordinal) && outer.Others is ["1"] &&
                inner.Op.StartsWith("Multiply", StringComparison.Ordinal) && inner.Others is ["_"])
            {
                return Typed(ModifierType.DamageMultiplier) with { Conversion = CountedScale };
            }
        }

        return null;
    }

    private static ModifierClass? FromKeyword(ModifierCandidate candidate)
    {
        var leaf = HeroNames.LastSegment(candidate.Row);

        if (ReductionLeaf().IsMatch(leaf) && candidate.Text is { } text)
        {
            var cast = CastTimeWords().IsMatch(text);
            var cooldown = CooldownWords().IsMatch(text);

            if (cast != cooldown)
            {
                return new(cast ? ModifierType.CastTimeReduction : ModifierType.CooldownReduction, ModifierBasis.Keyword,
                    $"{leaf} with the description naming {(cast ? "cast time" : "cooldown")}");
            }
        }

        foreach (var (pattern, type) in Keywords)
        {
            if (!pattern.IsMatch(leaf)) continue;

            var attribute = type == ModifierType.Coefficient ? CoefficientAttribute(leaf) : null;
            if (type == ModifierType.Coefficient && attribute is null) continue;

            return new(type, ModifierBasis.Keyword, $"{pattern} on {leaf}") { Attribute = attribute };
        }

        return null;
    }

    private static string? CoefficientAttribute(string leaf)
    {
        var all = Coefficients.All(
            new Dictionary<string, object?> { [leaf] = 1d },
            new HashSet<string> { leaf },
            new HashSet<string>());

        return all.Count == 1 ? all[0].Attribute : null;
    }

    public static IReadOnlyList<(Regex Pattern, ModifierType Type)> Keywords { get; } =
    [
        (new Regex("Coefficient$"), ModifierType.Coefficient),
        (new Regex("^Spread$"), ModifierType.Spread),
        (new Regex("CastTime(Increase|Increased|Addition)"), ModifierType.CastTimeIncrease),
        (new Regex("CastTime(Reduction|Decrease|Reduced)"), ModifierType.CastTimeReduction),
        (new Regex("CastTime(Multiplier|Scaler|Scale)$"), ModifierType.CastTimeMultiplier),
        (new Regex("^(CastTime|CastingDuration)$"), ModifierType.CastTime),
        (new Regex("Cooldown(Reduction|Decrease|Reduced)"), ModifierType.CooldownReduction),
        (new Regex("^(Cooldown|CooldownDuration|BaseCooldown)$"), ModifierType.Cooldown),
        (new Regex("(Duration|Time)(Increase|Extension|Added)|^(ExtendedDuration|DurationExtension|AdditionalDuration|IncreasedDuration)"), ModifierType.DurationIncrease),
        (new Regex("^(DurationMultiplier|DurationScaler)$"), ModifierType.DurationMultiplier),
        (new Regex("^(Duration|BuffDuration|DebuffDuration|DotDuration|HotDuration|EffectDuration)$"), ModifierType.Duration),
        (new Regex("^(TickInterval|Period|TickRate|PulsePeriod|DotTickInterval)$"), ModifierType.Period),
        (new Regex("^(MaxStacks|MaxStackCount|StackLimit|MaximumStacks)$"), ModifierType.MaxStacks),
        (new Regex("^(Stacks|NumStacks|NumOfStacks|StacksToApply|StackCount|StacksGranted|NumberOfStacksApplied)$"), ModifierType.Stacks),
        (new Regex("^(MaxCharges|Charges|NumCharges)$"), ModifierType.Charges),
        (new Regex("^(MaxTargets|TargetCount|NumTargets|MaxTargetCount|TotalTargets|MaxNumTargets|MaxNumOfTargets)$"), ModifierType.TargetCount),
        (new Regex("^(MaxRange|Range|MeleeRange)$"), ModifierType.Range),
        (new Regex("^MinRange$"), ModifierType.MinRange),
        (new Regex("^(Radius|AoeRadius|MaxRadius|AOERadius)$"), ModifierType.Radius),
        (new Regex("Cost(Reduction|Decrease)"), ModifierType.CostReduction),
        (new Regex("Cost(Increase)"), ModifierType.CostIncrease),
        (new Regex("^(Cost|ManaCost|ResourceCost|OrbCost)$"), ModifierType.Cost),
        (new Regex("^(ProcChance|Chance|TriggerChance)$"), ModifierType.ProcChance),
        (new Regex("^(ProcsPerMinute|PPM)$"), ModifierType.ProcsPerMinute),
        (new Regex("^(CritChance|CriticalChance|CritChanceOverride)$"), ModifierType.CritChance),
        (new Regex("^(CritChanceIncrease|AddedCritChance|AdditionalCritChance|IncreasedCritChance|CritChanceBonus)$"), ModifierType.CritChanceIncrease),
        (new Regex("^(CritDamageMultiplier|CritMultiplier|CriticalDamageMultiplier)$"), ModifierType.CritDamageMultiplier),
        (new Regex("^(Heal|Healing)(Multiplier|Scaler|Increase|IncreaseMultiplier)$|^HotScaler$"), ModifierType.HealingMultiplier),
        (new Regex("^(Damage|AdditionalDamage|IncreasedDamage|BonusDamage)?(Multiplier|Scaler|Increase|IncreaseMultiplier)$|^(AdditionalDamage|IncreasedDamage|DamageIncrease|DamageBonus|DamageScale)$"), ModifierType.DamageMultiplier),
    ];

    private static ModifierClass? Factor(ModifierCandidate candidate, ModifierClass typed)
    {
        if (candidate.Value is not { } value) return null;

        foreach (var sink in Uses(candidate, RowSinks.SinkUse))
        {
            var steps = Steps(sink);

            var unity = steps.ToList().FindIndex(step =>
                step.Op.StartsWith("Add_", StringComparison.Ordinal) && step.Others.Contains("1"));

            if (unity >= 0 && steps.Skip(unity + 1).Any(step =>
                    step.Op.StartsWith("Multiply", StringComparison.Ordinal) && step.Others.Contains("_")))
            {
                return typed with { Conversion = CountedScale };
            }

            if (steps.Any(step => step.Op.StartsWith("FClamp", StringComparison.Ordinal) && step.At > 0))
            {
                return null;
            }

            for (var at = steps.Count - 1; at >= 0; at--)
            {
                var step = steps[at];
                var other = step.Others.FirstOrDefault(entry => entry != "_");

                if (step.Op.StartsWith("SelectFloat", StringComparison.Ordinal) && step.At < 2 && other is "1")
                {
                    return typed with { Conversion = $"the untalented branch selects 1 ({sink})" };
                }

                if (step.Op.StartsWith("SelectFloat", StringComparison.Ordinal) && step.At < 2 && other is "0")
                {
                    return Increment(typed, value, $"the untalented branch selects 0 ({sink})");
                }

                if (step.Op.StartsWith("Add_", StringComparison.Ordinal) && other is "1")
                {
                    return Increment(typed, value, $"the value is added to 1 ({sink})");
                }

                if (step.Op.StartsWith("Subtract_", StringComparison.Ordinal) && step.At == 1 && other is "1")
                {
                    return Increment(typed, -value, $"the value is subtracted from 1 ({sink})");
                }

                if (step.Op.StartsWith("Multiply", StringComparison.Ordinal))
                {
                    return typed with { Conversion = $"the value is multiplied in directly ({sink})" };
                }

                if (step.Op.StartsWith("Add_", StringComparison.Ordinal) || step.Op.StartsWith("Subtract_", StringComparison.Ordinal))
                {
                    break;
                }
            }
        }

        foreach (var printed in Uses(candidate, RowSinks.PrintedUse))
        {
            if (printed.Contains("format=delta", StringComparison.Ordinal) ||
                printed.Contains("format=reduction", StringComparison.Ordinal) ||
                (printed.Contains(";percent", StringComparison.Ordinal) &&
                 printed.Contains(";multiplies", StringComparison.Ordinal)))
            {
                return typed with { Conversion = $"the placeholder prints the factor ({printed})" };
            }
        }

        if (Printed(candidate.Text, value) is { } reading) return reading(typed, value);

        return null;
    }

    private static ModifierClass Increment(ModifierClass typed, double increment, string evidence) =>
        typed with
        {
            Value = Math.Round(1 + increment, 10),
            ValueLow = typed.ValueLow is { } low ? Math.Round(1 + (increment < 0 ? -low : low), 10) : null,
            ValueHigh = typed.ValueHigh is { } high ? Math.Round(1 + (increment < 0 ? -high : high), 10) : null,
            Conversion = $"increment: {evidence}",
        };

    private static Func<ModifierClass, double, ModifierClass?>? Printed(string? text, double value)
    {
        if (text is not { Length: > 0 }) return null;

        foreach (Match match in PercentPattern().Matches(text))
        {
            var number = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
            var after = text.Substring(match.Index + match.Length, Math.Min(48, text.Length - match.Index - match.Length));
            var start = Math.Max(0, match.Index - 48);
            var before = text[start..match.Index];
            var excerpt = $"\"{before}{match.Value}{after}\"";

            if (Near(number, value * 100))
            {
                if (ReducedByPattern().IsMatch(before))
                {
                    return (typed, v) => Increment(typed, -v, $"the description prints reduced by {number}% {excerpt}");
                }

                if (IncreasedByPattern().IsMatch(before))
                {
                    return (typed, v) => Increment(typed, v, $"the description prints increased by {number}% {excerpt}");
                }

                if (LessPattern().IsMatch(after))
                {
                    return (typed, v) => Increment(typed, -v, $"the description prints {number}% reduced {excerpt}");
                }

                if (MorePattern().IsMatch(after))
                {
                    return (typed, v) => Increment(typed, v, $"the description prints {number}% more {excerpt}");
                }

                return (_, _) => null;
            }

            if (Near(number, Math.Abs(value - 1) * 100) && value != 0)
            {
                return (typed, _) => typed with { Conversion = $"the description prints the factor's excess {excerpt}" };
            }
        }

        return null;
    }

    private static bool Near(double printed, double computed) => Math.Abs(printed - computed) < 0.05;

    [GeneratedRegex(@"\b(increas\w*|rais\w*)\b[^.%]{0,40}\bby\s*(<[^>]*>)*\s*\+?$", RegexOptions.IgnoreCase)]
    private static partial Regex IncreasedByPattern();

    [GeneratedRegex(@"\b(reduc\w*|decreas\w*|lower\w*)\b[^.%]{0,40}\bby\s*(<[^>]*>)*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ReducedByPattern();

    [GeneratedRegex(@"^(Reduction|ReductionInSeconds|ReducedBySeconds)$")]
    private static partial Regex ReductionLeaf();

    [GeneratedRegex(@"\bcast time\b", RegexOptions.IgnoreCase)]
    private static partial Regex CastTimeWords();

    [GeneratedRegex(@"\bcooldowns?\b", RegexOptions.IgnoreCase)]
    private static partial Regex CooldownWords();

    [GeneratedRegex(@"_\d+_[0-9A-F]{32}$")]
    private static partial Regex UserStructMember();

    [GeneratedRegex(@"(?<op>[A-Za-z_]+)@(?<at>\d+)\((?<others>[^)]*)\)")]
    private static partial Regex StepPattern();

    [GeneratedRegex(@"(?<n>\d+(?:\.\d+)?)%")]
    private static partial Regex PercentPattern();

    [GeneratedRegex(@"^[^.]{0,24}?\b(less|reduced|reduction|lower|fewer|decreased)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LessPattern();

    [GeneratedRegex(@"^[^.]{0,24}?\b(more|increased|increase|additional|bonus|extra|higher|greater|faster)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MorePattern();
}
