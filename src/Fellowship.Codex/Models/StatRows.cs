using Fellowship.SDK;

namespace Fellowship.Codex.Models;

public sealed record StatBlock(
    float PrimaryWeight,
    float SecondaryWeight,
    float StaminaWeight,
    float PrimaryMultiplier,
    float SecondaryMultiplier,
    float WeightMultiplier,
    int StaminaMultiplierItemLevelThreshold,
    float StaminaMultiplierAtThreshold);

public static class StatRows
{
    public const int PrimaryDivisible = 15;

    public const float SecondaryBaseMultiplier = 72f;

    public const float SecondaryPoolMultiplier = 2.4f;

    private const float NativePrimaryWeight = 0.25f;
    private const float NativeSecondaryWeight = 0.3f;
    private const float NativeStaminaWeight = 0.5f;
    private const float NativePrimaryMultiplier = 0.7f;
    private const float NativeWeightMultiplier = 0.15f;
    private const int NativeStaminaThreshold = 315;
    private const float NativeStaminaAtThreshold = 2f;

    private const float NativeBaseSecondaryMultiplier = 0.6f;
    private const float NativeFixedSecondaryMultiplier = 1.5f;

    public static StatBlock Block(SlotStatSettings shipped, float nativeSecondaryMultiplier) => new(
        NativePrimaryWeight,
        NativeSecondaryWeight,
        (float?)shipped.StaminaWeight ?? NativeStaminaWeight,
        (float?)shipped.PrimaryMultiplier ?? NativePrimaryMultiplier,
        (float?)shipped.SecondaryMultiplier ?? nativeSecondaryMultiplier,
        (float?)shipped.WeightMultiplier ?? NativeWeightMultiplier,
        NativeStaminaThreshold,
        (float?)shipped.StaminaMultiplierAtThreshold ?? NativeStaminaAtThreshold);

    public static StatBlock Guaranteed(StatCalculationFile calculation) =>
        Block(calculation.BaseSlot, NativeBaseSecondaryMultiplier);

    public static StatBlock Rolled(StatCalculationFile calculation) =>
        Block(calculation.FixedSlot, NativeFixedSecondaryMultiplier);

    public static float PrimaryPool(StatCalculationFile calculation, int itemLevel) =>
        MathF.Pow((float)calculation.PrimaryPowerBase, itemLevel / PrimaryDivisible)
        * MathF.Sqrt(itemLevel)
        * (float)calculation.PrimaryAndStaminaMultiplier;

    public static float SecondaryPool(int itemLevel) =>
        MathF.Sqrt(itemLevel * SecondaryBaseMultiplier) * SecondaryPoolMultiplier;

    public static float BlockTerm(StatBlock block, bool stamina, bool onPrimaryPool, int itemLevel)
    {
        if (stamina)
        {
            var bonus = itemLevel >= block.StaminaMultiplierItemLevelThreshold
                ? block.StaminaMultiplierAtThreshold
                : 1f;

            return block.StaminaWeight * bonus / block.WeightMultiplier * block.PrimaryMultiplier;
        }

        return onPrimaryPool
            ? block.PrimaryWeight / block.WeightMultiplier * block.PrimaryMultiplier
            : block.SecondaryWeight / block.WeightMultiplier * block.SecondaryMultiplier;
    }

    public static float[] Weights(IReadOnlyList<int> declared)
    {
        var remainder = 100f;
        var built = new float[declared.Count + 1];

        for (var at = 0; at < declared.Count; at++)
        {
            remainder -= declared[at];
            built[at] = declared[at] * 0.01f;
        }

        built[^1] = MathF.Max(remainder, 0f) * 0.01f;

        return built;
    }

    public static float Share(float[] weights, int rowOrdinal) =>
        weights.Length == 0 ? 0f : weights[Math.Min(rowOrdinal, weights.Length - 1)];

    public static float Magnitude(
        float pool, float slotStatWeight, float blockTerm, float share, int tempers, bool guaranteed)
    {
        var magnitude = MathF.Max(pool * slotStatWeight * blockTerm * share, 1f);

        return guaranteed ? magnitude + tempers : magnitude;
    }

    public static decimal RoundHalfToEven(float value)
    {
        var integral = MathF.Truncate(value);
        var fraction = value - integral;

        if (value >= 0f)
        {
            if (MathF.Abs(fraction - 0.5f) <= 1e-8f) fraction = 0.5f;
        }
        else if (MathF.Abs(fraction + 0.5f) <= 1e-8f)
        {
            fraction = -0.5f;
        }

        var adjusted = fraction + integral;
        var even = ((int)MathF.Floor(MathF.Abs(adjusted)) & 1) == 0;

        var rounded = adjusted >= 0f
            ? even ? MathF.Ceiling(adjusted - 0.5f) : MathF.Floor(adjusted + 0.5f)
            : even ? MathF.Floor(adjusted + 0.5f) : MathF.Ceiling(adjusted - 0.5f);

        return (decimal)rounded;
    }
}
