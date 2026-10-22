using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ItemPowerSettingsTests
{
    private static ItemSettingsFile Power => Export.Loaded.Settings.Items;

    private static ItemPowerPotential Potential => Power.PowerPotential;

    [Fact]
    public void TheItemLevelLadderIsTheOneTheConfigStates()
    {
        Assert.Equal(420, Power.MaxItemLevel);
        Assert.Equal(15, Power.ItemLevelsPerUpgrade);
    }

    [Fact]
    public void EveryScoreFactorTheStructDeclaresIsRead()
    {
        Assert.Equal(14d, Potential.ItemLevelScoreFactor);
        Assert.Equal(20d, Potential.TemperScoreFactor);
        Assert.Equal(20d, Potential.ModSlotScoreFactor);
        Assert.Equal(30d, Potential.GemSocketScoreFactor);
        Assert.Equal(80d, Potential.WeaponAbilityScoreFactor);
        Assert.Equal(80d, Potential.RelicAbilityScoreFactor);
        Assert.Equal(20d, Potential.WeaponTraitTreeScoreFactor);
        Assert.Equal(20d, Potential.NecklaceTraitTreeScoreFactor);
        Assert.Equal(0d, Potential.SetBonusScoreFactor);
        Assert.Equal(1000d, Potential.LegendaryScoreFactor);
        Assert.Equal(1.04d, Potential.AllocatedTalentPointPowerPotential);
        Assert.Equal(40, Potential.ScorePerAllocatedWeaponTrait);
        Assert.Equal(60, Potential.ScorePerAllocatedNecklaceTrait);
    }

    [Fact]
    public void AGemScoresUnderTheRungItPrintsRatherThanTheTierTheConfigKeysBy()
    {
        Assert.Equal(
            ["Champion", "Epic", "Rare", "Uncommon"], Potential.ScorePerGemType.Keys.Order(StringComparer.Ordinal));

        Assert.Equal(20, Potential.ScorePerGemType["Uncommon"]);
        Assert.Equal(35, Potential.ScorePerGemType["Rare"]);
        Assert.Equal(50, Potential.ScorePerGemType["Epic"]);
        Assert.Equal(65, Potential.ScorePerGemType["Champion"]);
    }

    [Fact]
    public void EverySocketTierTheConfigStatesMultipliesByOne()
    {
        Assert.Equal([1, 2, 3], Power.GemSocketPowerMultipliers.Keys.Order());
        Assert.All(Power.GemSocketPowerMultipliers.Values, multiplier => Assert.Equal(1d, multiplier));
    }

    [Fact]
    public void EveryGemPowerIsFiledUnderTheSlotNameTheGamePrints()
    {
        Assert.Equal(
            Export.Loaded.Settings.SlotIcons.Keys.Order(StringComparer.Ordinal),
            Power.ItemGemPowers.Keys.Order(StringComparer.Ordinal));

        Assert.All(Power.ItemGemPowers.Values, gemPower => Assert.Equal(100d, gemPower));
    }
}

public sealed class ConfigLiteralTests
{
    [Fact]
    public void AMapNestedInAStructKeepsItsOwnPairsRatherThanSplittingOnTheCommasInside()
    {
        var written =
            "(ItemLevelScoreFactor=14.000000,ScorePerGemType=((Tier1, 20),(Tier2, 35)),ScorePerAllocatedWeaponTrait=20)";

        var block = Assert.IsType<ConfigList>(ConfigIni.Parse(written));

        Assert.Equal("14.000000", block.Field("ItemLevelScoreFactor")!.Text);
        Assert.Equal("20", block.Field("ScorePerAllocatedWeaponTrait")!.Text);

        var tiers = Assert.IsType<ConfigList>(block.Field("ScorePerGemType"));

        Assert.Equal([("Tier1", "20"), ("Tier2", "35")], tiers.Pairs().Select(pair => (pair.Key, pair.Value.Text)));
    }

    [Fact]
    public void AQuotedValueKeepsTheSeparatorsInsideIt()
    {
        var block = Assert.IsType<ConfigList>(ConfigIni.Parse("""(TagName="ItemID.Mount.CatBeta",Rank=2)"""));

        Assert.Equal("ItemID.Mount.CatBeta", block.Field("TagName")!.Text);
        Assert.Equal("2", block.Field("Rank")!.Text);
    }

    [Fact]
    public void AnUnclosedLiteralIsRefusedRatherThanReadingBackShort() =>
        Assert.Throws<InvalidOperationException>(() => ConfigIni.Parse("(A=1,B=2"));
}
