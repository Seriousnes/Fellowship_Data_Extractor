using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class TruthTests
{
    private static JsonObject Record(string tag) =>
        Export.Loaded.Documents
            .First(document => (string?)document.Body["tag"] == tag)
            .Body;

    [Theory]
    [InlineData("GE_Bowguy_SelfBuffForAoeProjectile_SelfBuff", 4)]
    [InlineData("GA_Bowguy_SelfBuffForAoeProjectile", 4)]
    [InlineData("GE_Vigor_SingleTargetAbsorb_Absorb", 12)]
    [InlineData("GE_Meiko_TauntTotem_SelfBuff", 20)]
    [InlineData("GE_Firemage_InstantSingleTargetDot_Dot", 30)]
    [InlineData("GE_Ink_SelfDefenceBuff_SelfBuff_First", 4)]
    [InlineData("GA_Warmaster_MeleeInterrupt", 4)]
    public void ADurationIsWhatTheCalculationReturnsBare(string tag, double expected) =>
        Assert.Equal(expected, (double?)Record(tag)["duration"]);

    [Theory]
    [InlineData("GA_Vigor_SingleTargetAbsorb", 1)]
    [InlineData("GA_Vigor_SingleTargetChargedHot", 1)]
    [InlineData("GA_Vigor_SingleTargetDot", 1)]
    [InlineData("GA_Lisa_InstantSingleTargetDamageAndDot", 9)]
    [InlineData("GA_Sune_DroppedPulseAoe", 218)]
    [InlineData("GA_Meiko_DurationalMultipleTeleportDamage_New", 100)]
    [InlineData("GA_Ink_AoeAttack", 0.5)]
    public void ACostIsTheCastCostNotATalentRow(string tag, double expected) =>
        Assert.Equal(expected, (double?)Record(tag)["cost"]);

    [Theory]
    [InlineData("GA_Ink_AoeAttack")]
    [InlineData("GA_Ink_HeavySingleTargetAttack")]
    [InlineData("GA_Lisa_InstantDamageSpender")]
    public void AFractionOfTheResourceSaysSoOnTheRow(string tag) =>
        Assert.True((bool?)Record(tag)["costIsFraction"]);

    [Theory]
    [InlineData("GA_Sune_DroppedPulseAoe")]
    [InlineData("GA_Lisa_InstantSingleTargetDamageAndDot")]
    [InlineData("GA_Vigor_SingleTargetAbsorb")]
    public void AnAbsoluteCostCarriesNoFractionFlag(string tag) =>
        Assert.Null(Record(tag)["costIsFraction"]);

    [Theory]
    [InlineData("GE_Bowguy_CastedProjectileDamage_Buff", "Steady Hands")]
    [InlineData("GE_Finesse_CurrentSpiritToBuff_Buff", "The Philosopher")]
    [InlineData("GE_Perk_MaxHealthBasedAbsorb_Absorb", "The Dragon's Embrace")]
    public void AnEffectShipsItsOwnUiDataName(string tag, string expected) =>
        Assert.Equal(expected, (string?)Record(tag)["name"]);
}
