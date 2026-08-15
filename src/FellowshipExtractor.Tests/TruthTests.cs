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
    [InlineData("GA_Gunde_Talent_DotTransfer_Aoe", "range", 1000)]
    [InlineData("GA_Mount_MountUp_Base", "castTime", 1.5)]
    [InlineData("GA_Warmaster_InstantMeleeCleave", "range", 500)]
    [InlineData("GA_Bowguy_ChanneledMultiProjectileDebuff", "channelTime", 2)]
    [InlineData("GA_Rime_ChanneledBeamSingleDamage", "channelTime", 2)]
    public void TheTooltipHookArgumentIsTheDisplayedValue(string tag, string field, double expected) =>
        Assert.Equal(expected, (double?)Record(tag)[field]);

    [Theory]
    [InlineData("GE_Bowguy_CastedProjectileDamage_Buff", "Steady Hands")]
    [InlineData("GE_Finesse_CurrentSpiritToBuff_Buff", "The Philosopher")]
    [InlineData("GE_Perk_MaxHealthBasedAbsorb_Absorb", "The Dragon's Embrace")]
    public void AnEffectShipsItsOwnUiDataName(string tag, string expected) =>
        Assert.Equal(expected, (string?)Record(tag)["name"]);

    private static JsonObject Token(string tag, string key) =>
        Record(tag)["placeholders"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(token => (string?)token["key"] == key)
        ?? throw new InvalidOperationException($"{tag} carries no {{{key}}} placeholder.");

    [Theory]
    [InlineData("GA_Weapon_InstantDamageAccumulativeDebuff", "VoidTouchDmg", 42.5, "PrimaryAttribute", true)]
    [InlineData("GA_Ink_AoeFear", "IntimidateDmgBreak", 30, "Strength", false)]
    [InlineData("GA_Sune_SingleTargetAbsorb", "SolarShldAbsrb", 22.998, "Strength", true)]
    public void ATokenTheBodyMultipliesByAnAttributeIsACoefficientNotAValue(
        string tag, string key, double coefficient, string attribute, bool expertise)
    {
        var token = Token(tag, key);

        Assert.Equal(coefficient, (double?)token["coefficient"]);
        Assert.Equal(attribute, (string?)token["attribute"]);
        Assert.Null(token["value"]);
        Assert.Equal(expertise ? true : null, (bool?)token["scalesWithExpertise"]);
    }

    [Fact]
    public void ABodyCombiningReadsAcrossAttributesShipsItsBracket()
    {
        var token = Token("GA_Meiko_DamageSoakBuff", "StoneShieldHealth");

        Assert.Equal(8.624, (double?)token["coefficient"]);
        Assert.Equal("Agility", (string?)token["attribute"]);

        var bracket = token["bracket"]?.AsObject();

        Assert.NotNull(bracket);
        Assert.Equal(-1, (double?)bracket!["addend"]);

        var terms = bracket["terms"]?.AsArray().OfType<JsonObject>()
            .ToDictionary(term => (string)term["attribute"]!, term => (double)term["coefficient"]!);

        Assert.NotNull(terms);
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["CritChance"] = 2, ["Expertise"] = 2, ["Haste"] = 2, ["Spirit"] = 2,
            },
            terms);
    }

    [Theory]
    [InlineData("GA_Lisa_ChanneledHealOrDamageProjectiles", "TemporBarrDmg", 1.91, "Intellect")]
    [InlineData("GA_Lisa_ChanneledHealOrDamageProjectiles", "TemporBarrHeal", 5.22, "Intellect")]
    [InlineData("GA_Weapon_InstantChargedProjectileHealOrDamage", "TwilSkyDmg", 9.552, "Intellect")]
    [InlineData("GA_Weapon_InstantChargedProjectileHealOrDamage", "TwilSkyHeal", 19.104, "Intellect")]
    public void ARangeBodyTheReaderUsedToRefuseNowCarriesItsCoefficient(
        string tag, string key, double coefficient, string attribute)
    {
        var token = Token(tag, key);

        Assert.Equal(coefficient, (double?)token["coefficient"]);
        Assert.Equal(attribute, (string?)token["attribute"]);
        Assert.True((bool?)token["scalesWithExpertise"]);
    }

    [Fact]
    public void ARoundedRangeStillReadsItsExpertiseScaling()
    {
        var token = Token("GA_Rime_CastedProjectileDamageOrbSpender", "GlacialBlast");

        Assert.Equal(10.24, (double?)token["coefficient"]);
        Assert.Equal("Intellect", (string?)token["attribute"]);
        Assert.True((bool?)token["scalesWithExpertise"]);
    }

    [Fact]
    public void AStackCountIsNoneAtRestSoTheBaseHealRemains()
    {
        var token = Token("GA_Meiko_InstantSelfDefensive", "SerenityHeal");

        Assert.Equal(2.2, (double?)token["coefficient"]);
        Assert.Equal("Agility", (string?)token["attribute"]);
        Assert.Equal(0.2, (double?)token["spread"]);
        Assert.True((bool?)token["scalesWithExpertise"]);
        Assert.Null(token["bracket"]);
    }

    [Theory]
    [InlineData("GA_Meiko_MeleeRight", "EarthFistDmg", 0.1)]
    [InlineData("GA_Ink_HeavySingleTargetAttack", "SkullCrushDmg", 0.2)]
    [InlineData("GA_Ink_HeavySingleTargetAttack", "SkullCrushLightnDmg", 0.2)]
    public void ATokenBandsInWhatItsEffectOverridesWithOrTheDefaultItsPresetDeclares(
        string tag, string key, double spread) =>
        Assert.Equal(spread, (double?)Token(tag, key)["spread"]);

    [Theory]
    [InlineData("GA_UB_BossWarrior_RangedDotProjectile", 0.4)]
    [InlineData("GA_UB_BossWarrior_MeleeAutoAttack", 0.2)]
    public void TwoAbilitiesOfOneBossPartOnWhetherTheirEffectBindsASpreadRow(string tag, double spread)
    {
        var coefficient = Record(tag)["coefficients"]?.AsArray().OfType<JsonObject>().Single();

        Assert.NotNull(coefficient);
        Assert.Equal(spread, (double?)coefficient!["spread"]);
    }

    [Theory]
    [InlineData("GA_Rime_Talent_SpendersSwap_SingleTarget", 4.096, true)]
    [InlineData("GA_Rime_Talent_SpendersSwap_MultiTargets", 2.114, false)]
    public void ACoefficientOnTheAbilityAReferenceNamesIsRead(string tag, double coefficient, bool expertise)
    {
        var token = Token(tag, "Talons");

        Assert.Equal(coefficient, (double?)token["coefficient"]);
        Assert.Equal("Intellect", (string?)token["attribute"]);
        Assert.Equal(0.2, (double?)token["spread"]);
        Assert.Equal(expertise ? true : null, (bool?)token["scalesWithExpertise"]);
    }

    private static JsonObject Modifier(string tag, string attribute) =>
        Record(tag)["modifiers"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(modifier => (string?)modifier["attribute"] == attribute)
        ?? throw new InvalidOperationException($"{tag} carries no modifier on {attribute}.");

    [Fact]
    public void AnAbsorbConsumptionScalerIsWhatItsCalculationClampsTo()
    {
        var modifier = Modifier("GE_Vigor_SingleTargetAbsorb_Absorb", "AbsorbConsumptionScaler");

        Assert.Equal(0.5, (double?)modifier["value"]);
        Assert.Null(modifier["op"]);
    }

    [Fact]
    public void ABlockMultiplierIsTheRowItsCalculationReads()
    {
        var modifier = Modifier("GE_HeroAttributesBase", "BlockDamageReductionMultiplier");

        Assert.Equal(0.8, (double?)modifier["value"]);
        Assert.Equal("Multiplicitive", (string?)modifier["op"]);
    }

    [Fact]
    public void AMagnitudeMultiplyingACapturedAttributeIsACoefficient()
    {
        var modifier = Modifier("GE_Ink_MaxRage", "MaxResources");

        Assert.Equal(200, (double?)modifier["coefficient"]);
        Assert.Null(modifier["value"]);
        Assert.Equal(["Strength"], modifier["scalesOff"]?.AsArray().Select(scale => (string?)scale));
    }

    [Theory]
    [InlineData("GE_Vigor_SingleTargetAbsorb_Absorb", "IncreasedDuration", 12, 15)]
    [InlineData("GE_Vigor_SingleTargetDot_Dot", "IncreasedDuration", 24, 30)]
    [InlineData("GE_Vigor_SingleTargetEchoProccer_Buff", "IncreasedDuration", 30, 37.5)]
    [InlineData("GE_Vigor_InstantResourceGain_Buff", "DurationAndCost", 8, 14)]
    [InlineData("GE_Meiko_TauntTotem_SelfBuff", "IncreasedHealthAndDuration", 20, 30)]
    [InlineData("GE_Rime_SelfDefenceBuff_SelfBuff", "DurationBoostAndHealFromDamage", 4, 8)]
    [InlineData("GE_Ink_SelfDefenceBuff_SelfBuff_First", "IncreasedDurationAndTaunt", 4, 8)]
    [InlineData("GE_Mosse_PinkButterfly_Double_Hot", "IncreasedHotDuration", 15, 24)]
    [InlineData("GE_Firemage_CastedSingleHeavyDot_Dot", "ExtendedDuration", 9, 12)]
    [InlineData("GE_Lisa_CastedProjectileDamageIncreaseDebuff_Debuff", "AbilityDamageIncrease", 20, 15)]
    [InlineData("GE_Bowguy_SelfBuffForAoeProjectile_SelfBuff", "ChargesCooldownAndDuration", 4, 15)]
    public void ATalentTheCalculationGatesOnCarriesItsOwnDuration(
        string tag, string talent, double bare, double held)
    {
        var record = Record(tag);

        Assert.Equal(bare, (double?)record["duration"]);
        Assert.Equal(held, (double?)record["talents"]?[talent]?["duration"]);
    }

    [Fact]
    public void ATokenTheUiDataDoesNotDeclareComesFromTheSoleModifier()
    {
        var token = Token("GE_DungeonModifier_AnomalousOrbs_Buff_Amethyst", "Increase");

        Assert.Equal(480, (double?)token["value"]);
        Assert.Equal(480, (double?)Modifier("GE_DungeonModifier_AnomalousOrbs_Buff_Amethyst", "AmethystPower")["value"]);
    }

    [Fact]
    public void TheOneCalculationAnsweringARangeStatesBothEnds()
    {
        var modifier = Modifier("GE_Mara_AoeAttackBuilder_Poison", "DurationMagnitude");

        Assert.Equal(6, (double?)modifier["valueLow"]);
        Assert.Equal(8, (double?)modifier["valueHigh"]);
        Assert.Null(modifier["value"]);
    }

    [Fact]
    public void AnApplierWithAnAssignmentTheWalkCannotReadPublishesNoDuration() =>
        Assert.Null(Record("GE_DR_BossCultLeader_Grip_SingleInstantPullNPC")["duration"]);

    [Theory]
    [InlineData("GE_Mara_StunSpender_Stun", 4)]
    [InlineData("GE_Ink_SwingTimerHitWindow", 0.6)]
    [InlineData("GE_Rime_TargetedPeriodicProjectileAoe_AoeBuff", 20)]
    [InlineData("GE_ST_BossGatekeeper_PullAndCastedAoe_Imobalize", 0.4)]
    public void ADurationTheApplierSetsByCallerIsRecoveredFromItsBytecode(string tag, double expected) =>
        Assert.Equal(expected, (double?)Record(tag)["duration"]);

    [Fact]
    public void APerkWritesEachHeroTheSentenceTheirOwnDynamicTextTableCarries()
    {
        var record = Record("CAA_Perk_IncreasedDurationOrReducedCooldown");
        var bowguy = (string?)record["descriptionsByHero"]?["Bowguy"];

        Assert.Equal("{uniqueHeroDescription}", (string?)record["description"]);
        Assert.NotNull(bowguy);
        Assert.Contains("duration is increased", bowguy, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPerkWhoseDescriptionIsTheSharedTokenCarriesTwelveHeroSentences()
    {
        var filled = Export.Loaded.Documents
            .Where(document => document.Body["descriptionsByHero"] is not null)
            .ToArray();

        Assert.Equal(6, filled.Length);
        Assert.All(filled, document => Assert.Equal(12, document.Body["descriptionsByHero"]!.AsObject().Count));
    }
}
