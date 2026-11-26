using System.Globalization;

using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The split of one hit between the target's absorption effects and its Health. The rule is the shipping binary's
/// (build 26320967: damage driver 0x1450DCF40, absorption routine 0x1450E4BE0, deduction crit 0x1450D90F0, combat log
/// writer 0x145296FB0). The deduction crit scaler is a config value of <c>CRCharacterBase</c>.
/// </summary>
public static class DamageAbsorptions
{
    public const string CharacterSection = "/Script/fellowship.CRCharacterBase";

    private const string DeductionCritScalerKey = "DefaultAbsorbDeductionCritScaler";

    public static DamageAbsorption Read(AbstractFileProvider provider)
    {
        var config = ConfigIni.Read(provider, ItemSettings.ConfigPath, CharacterSection);
        var text = config.Text(DeductionCritScalerKey);

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var critScaler))
        {
            throw new InvalidOperationException(
                $"[{CharacterSection}] in {config.Path} sets {DeductionCritScalerKey} to '{text}', not a number.");
        }

        return new DamageAbsorption
        {
            DisplayText =
                "The effects with the tag split the hit after mitigation, in order of capacity, least first. " +
                "Capacity is pool ÷ (consumption scaler × crit factor). Each pass gives every effect the share " +
                "D ÷ n. When the least capacity is above the share, every effect absorbs the share, its pool " +
                "loses share × consumption scaler × crit factor, and the hit is absorbed. Otherwise the effect of " +
                "least capacity absorbs its capacity and ends, and the rest of the hit goes through mitigation " +
                "and the effects left again. Health loses max(round(T × DamageMitigationScale) − A, 0). " +
                "The crit factor is one roll for each effect and hit, with chance 2c ÷ (1 + c).",
            LoggedAmountText =
                "amount = clamp(|H + A| − absorption left, 0, Health − MinHealth). hitPoints = Health before the " +
                "hit − amount. absorption = the sum of the pools after the hit. absorbed = A + the reductions of " +
                "the hooks after the effects. The amount of each DAMAGE_ABSORBED line is the damage one effect absorbed.",
            Symbols =
            [
                new("D", "damage left of the hit after mitigation"),
                new("n", "number of effects with the tag on the target"),
                new("T", "damage absorbed plus damage left when the passes end"),
                new("A", "damage absorbed by the effects"),
                new("H", "Health lost, after the hooks"),
                new("c", "CritChance of the effect's instigator"),
            ],
            EffectTag = "EffectType.Absorption",
            PoolMagnitude = "SetByCaller.System.EffectAbsorptionPool",
            ConsumptionScalerMagnitude = "SetByCaller.System.EffectAbsorptionConsumptionScaler",
            DefaultConsumptionScaler = 1,
            DeductionCritScaler = critScaler,
            CritChanceAttribute = "CritChance",
            MostPasses = 30,
        };
    }
}
