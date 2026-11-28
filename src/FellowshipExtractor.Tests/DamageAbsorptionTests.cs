namespace FellowshipExtractor.Tests;

public sealed class DamageAbsorptionTests
{
    [Fact]
    public void TheSettingsStateTheSplitOfAHitOverTheAbsorptionEffects()
    {
        var absorption = Export.Loaded.Settings.DamageAbsorption;

        Assert.Equal("EffectType.Absorption", absorption.EffectTag);
        Assert.Equal("SetByCaller.System.EffectAbsorptionPool", absorption.PoolMagnitude);
        Assert.Equal("SetByCaller.System.EffectAbsorptionConsumptionScaler", absorption.ConsumptionScalerMagnitude);
        Assert.Equal(1, absorption.DefaultConsumptionScaler);
        Assert.Equal(0.5, absorption.DeductionCritScaler);
        Assert.Equal("CritChance", absorption.CritChanceAttribute);
        Assert.Equal(30, absorption.MostPasses);
        Assert.Contains("2c ÷ (1 + c)", absorption.DisplayText);
        Assert.Contains("absorption left", absorption.LoggedAmountText);
    }
}
