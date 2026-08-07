namespace FellowshipExtractor.Tooltip;

public static class TokenCalculations
{
    private const string PeriodPath = "Period";
    private const string DurationPath = "DurationMagnitude";
    private const string ExecutionPath = "Executions[";
    private const string ScalableMagnitude = "ModifierMagnitude.ScalableFloatMagnitude";

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

    public static TokenQuantity Named(string tokenName)
    {
        foreach (var quantity in Enum.GetValues<TokenQuantity>())
        {
            if (quantity == TokenQuantity.Unsettled) continue;
            if (tokenName.EndsWith(quantity.ToString(), StringComparison.OrdinalIgnoreCase)) return quantity;
        }

        return TokenQuantity.Unsettled;
    }

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
