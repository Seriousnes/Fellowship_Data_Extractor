namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What a CRDynamicTextToken's Type and Calculation ordinals mean.
///
/// The usmap carries no enum tables, so both fields dump as raw ordinals and their meaning has to
/// come from the corpus. Every token in all 269 CRAbilityDataRow tables was censused - 229 of them -
/// and each ordinal below is settled by where the tokens carrying it land and what they are called.
///
/// Type says where the number comes from:
///   0 (155 tokens) names a gameplay effect, never the token's own ability, and Calculation picks
///                  the quantity off it. Every one of the 155 objects loads.
///   3  (23 tokens) names no object at all and carries the constant itself, at
///                  Magnitude.ScalableFloatMagnitude. All 23 name a row the tuning tables define.
///   2  (30 tokens) and 4 (19 tokens) name a gameplay ability rather than an effect; 44 of those 49
///                  name the ability the token's own row describes, and native code works the number
///                  out from that ability's tuning. They keep the existing reading.
///   1 and 7 (one token each) are left unsettled: one token is no evidence for a rule.
///
/// Calculation, for the Type 0 tokens where it selects:
///   13 (16 tokens) Period. All 16 effects carry a Period and all 16 tokens are named *Period.
///    6  (2 tokens) Period as well. Both effects carry a Period, both tokens are named *Period, and
///                  one of the two also carries a Duration it is demonstrably not naming.
///    7  (4 tokens) Duration. All 4 effects carry a DurationMagnitude and the descriptions read
///                  "dealt over {Duration}", "for {duration}", "After {RestoreLifeDelay} seconds".
///    9 (115 tokens) execution magnitude, the direct damage number.
///   11 (16 tokens) execution magnitude as well, and every one of the 16 is named *Heal.
///    1 and 10 (one token each) also land in the executions and nowhere else, so they are read the
///                  same way; what separates them from 9 is not recoverable from two tokens.
/// All 133 tokens whose Calculation selects an execution magnitude reach at least one modifier
/// magnitude spelled as an FScalableFloat, which is why that is the path the reader narrows to:
/// the attribute-based coefficients sitting beside it are secondary scalers, and leaving them in
/// puts two candidates in front of the coefficient rule that only one of them answers.
///
/// What the ordinal names holds for a Type 2 or 4 token as well, even though the object it selects
/// off is an ability rather than an effect. All five self-naming tokens carrying a Period or a
/// Duration ordinal are named for one - SplinterDuration, IncinerateChannelInterval,
/// LifePetalPeriod, HeartSeekPeriod, ZeralethTickPeriod - and every self-naming token carrying an
/// execution ordinal is named for a hit or a heal. So the ordinal says which of the ability's own
/// rows can answer, and a token asking for a clock is not asking for a coefficient.
/// </summary>
public static class TokenCalculations
{
    private const string PeriodPath = "Period";
    private const string DurationPath = "DurationMagnitude";
    private const string ExecutionPath = "Executions[";
    private const string ScalableMagnitude = "ModifierMagnitude.ScalableFloatMagnitude";

    /// <summary>The Type ordinal whose tokens name a gameplay effect for Calculation to read.</summary>
    public const string EffectType = "0";

    private static readonly Dictionary<string, TokenQuantity> Quantities = new(StringComparer.Ordinal)
    {
        ["6"] = TokenQuantity.Period,
        ["13"] = TokenQuantity.Period,
        ["7"] = TokenQuantity.Duration,
        ["1"] = TokenQuantity.ExecutionMagnitude,
        ["9"] = TokenQuantity.ExecutionMagnitude,
        ["10"] = TokenQuantity.ExecutionMagnitude,
        ["11"] = TokenQuantity.ExecutionMagnitude,
    };

    public static TokenQuantity Of(string? calculation) =>
        calculation is not null && Quantities.TryGetValue(calculation, out var quantity)
            ? quantity
            : TokenQuantity.Unsettled;

    /// <summary>
    /// The bindings on an effect that hold one quantity. An unsettled ordinal keeps every binding,
    /// which is the reading a token got before any of this, and so does a quantity the effect turns
    /// out not to carry.
    /// </summary>
    public static IReadOnlyList<CurveBinding> Select(
        IReadOnlyList<CurveBinding> bindings, TokenQuantity quantity)
    {
        if (quantity == TokenQuantity.Unsettled) return bindings;

        var selected = bindings.Where(b => Holds(b.Property, quantity)).ToArray();
        return selected.Length == 0 ? bindings : selected;
    }

    private static bool Holds(string path, TokenQuantity quantity) => quantity switch
    {
        TokenQuantity.Period => path.Equals(PeriodPath, StringComparison.Ordinal),
        TokenQuantity.Duration => path.StartsWith(DurationPath, StringComparison.Ordinal),
        TokenQuantity.ExecutionMagnitude =>
            path.StartsWith(ExecutionPath, StringComparison.Ordinal) &&
            path.EndsWith(ScalableMagnitude, StringComparison.Ordinal),
        _ => false,
    };
}
