using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ResourceGainTests
{
    private const string Field = "generates";
    private const string Primary = "Resources";
    private const string Second = "ResourcesSecond";
    private const string Tertiary = "ResourcesTertiary";
    private const string Spirit = "SpiritPoints";
    private const string Mana = "Mana";
    private const string Crit = "crit";
    private const string TalentSegment = "Talent";

    private static JsonArray Generated(EntityType type, int id) =>
        Export.Loaded.Documents
            .First(document => document.Type == type && document.Id == id)
            .Body[Field] as JsonArray
        ?? throw new InvalidOperationException($"{type} {id} states no {Field}.");

    private static JsonNode Into(EntityType type, int id, string channel) =>
        Generated(type, id)
            .OfType<JsonNode>()
            .FirstOrDefault(gain => (string?)gain["resource"] == channel)
        ?? throw new InvalidOperationException($"{type} {id} generates nothing into {channel}.");

    private static string? Talented(string row)
    {
        var segments = row.Split('.');
        var at = Array.IndexOf(segments, TalentSegment);

        return at < 0 ? null : string.Join('.', segments[at..^1]);
    }

    private static IReadOnlyList<string> Sources(JsonNode gain) =>
    [
        .. Named(gain, "source").Concat(Named(gain, "sourceOnCritical")).Distinct(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> Named(JsonNode gain, string field) =>
        ((string?)gain[field] ?? string.Empty)
            .Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(row => !row.Contains('@'));

    [Fact]
    public void TimeShardGeneratesSixAndNineOnACriticalStrike()
    {
        var gains = Generated(EntityType.Ability, 1889);
        var into = Into(EntityType.Ability, 1889, Primary);

        Assert.Single(gains);
        Assert.Equal(6, (double?)into["amount"]);
        Assert.Equal(9, (double?)into["amountOnCritical"]);
        Assert.Equal("Lisa.CastedSingleTargetProjectile.ResourceGain", (string?)into["source"]);
        Assert.Equal("Lisa.CastedSingleTargetProjectile.ResourceGainCrit", (string?)into["sourceOnCritical"]);
        Assert.Equal(ResourceGains.ConstantsRow, (string?)into["sourceKind"]);
    }

    [Fact]
    public void FreezingTorrentGeneratesOnePerTick()
    {
        var gains = Generated(EntityType.Ability, 1027);
        var into = Into(EntityType.Ability, 1027, Primary);

        Assert.Single(gains);
        Assert.Equal(1, (double?)into["amount"]);
        Assert.Equal("Tick", (string?)into["per"]);
        Assert.Equal("Rime.ChanneledBeamSingleDamage.ResourceGainPerTick", (string?)into["source"]);
        Assert.Null((double?)into["amountOnCritical"]);
    }

    [Fact]
    public void WidowsBiteGeneratesTwoWithCritThreeInOneChannelAndThirtyInTheOther()
    {
        var gains = Generated(EntityType.Ability, 1207);
        var points = Into(EntityType.Ability, 1207, Second);
        var energy = Into(EntityType.Ability, 1207, Primary);

        Assert.Equal(2, gains.Count);

        Assert.Equal(2, (double?)points["amount"]);
        Assert.Equal(3, (double?)points["amountOnCritical"]);
        Assert.Equal("Mara.ChargedDoubleAttackBuilder.ComboPointGain", (string?)points["source"]);
        Assert.Equal(
            "Mara.ChargedDoubleAttackBuilder.ComboPointGainAtCrit", (string?)points["sourceOnCritical"]);

        Assert.Equal(30, (double?)energy["amount"]);
        Assert.Null((double?)energy["amountOnCritical"]);
        Assert.Equal("Mara.ChargedDoubleAttackBuilder.EnergyGain", (string?)energy["source"]);
    }

    [Fact]
    public void FrostBoltGeneratesTheWholeLoopNotOneTurnOfIt()
    {
        var gains = Generated(EntityType.Ability, 1029);
        var into = Into(EntityType.Ability, 1029, Primary);

        Assert.Single(gains);
        Assert.Equal(3, (double?)into["amount"]);
        Assert.Equal("Rime.CastedProjectileDamageFiller.ResourceGain", (string?)into["source"]);
        Assert.Equal(ResourceGains.RowProduct, (string?)into["sourceKind"]);
    }

    [Fact]
    public void SkitteringBladesGeneratesOnePerHitAndTwoOnACriticalStrike()
    {
        var gains = Generated(EntityType.Ability, 1213);
        var into = Into(EntityType.Ability, 1213, Second);

        Assert.Single(gains);
        Assert.Equal(1, (double?)into["amount"]);
        Assert.Equal(2, (double?)into["amountOnCritical"]);
        Assert.Equal("Hit", (string?)into["per"]);
        Assert.Equal("Mara.AoeAttackBuilder.ComboPointGainAtCrit", (string?)into["sourceOnCritical"]);
    }

    [Fact]
    public void BackstabStatesThePoisonedAmountAsTheCaseItBelongsTo()
    {
        var plain = Generated(EntityType.Ability, 1129)
            .OfType<JsonNode>()
            .Single(gain => gain["rowGroup"] is null);

        var poisoned = Generated(EntityType.Ability, 1129)
            .OfType<JsonNode>()
            .Single(gain => (string?)gain["rowGroup"] == "Poison");

        Assert.Equal(2, (double?)plain["amount"]);
        Assert.Equal(3, (double?)plain["amountOnCritical"]);
        Assert.Equal(6, (double?)poisoned["amount"]);
        Assert.Equal(9, (double?)poisoned["amountOnCritical"]);
    }

    [Fact]
    public void CullingStrikeStatesNoFuryBecauseBothItsCallSitesSpendIt()
    {
        var gains = Generated(EntityType.Ability, 1228);

        Assert.DoesNotContain(gains.OfType<JsonNode>(), gain => (string?)gain["resource"] == Primary);
        Assert.Equal(1, (double?)Into(EntityType.Ability, 1228, Spirit)["amount"]);
    }

    [Fact]
    public void OmnistrikeGeneratesTheFourSpiritPointsItsOwnTextPrints()
    {
        var into = Into(EntityType.Ability, 1863, Spirit);

        Assert.Equal(4, (double?)into["amount"]);
        Assert.Equal("Sune.AoeMelee.SpiritPointGain", (string?)into["source"]);
    }

    [Fact]
    public void SunStrikeStatesTheChanceItsResourceProcRollsAgainst()
    {
        var into = Into(EntityType.Ability, 1845, Primary);

        Assert.Equal(0.15, (double?)into["chance"]);
    }

    [Fact]
    public void EveryHeroThatStartsWithSpiritPointsStatesTheAmountItsOwnRowSpells()
    {
        Assert.Equal(50, (double?)Into(EntityType.Talent, 677, Spirit)["amount"]);
        Assert.Equal(50, (double?)Into(EntityType.Talent, 99, Spirit)["amount"]);
        Assert.Equal(50, (double?)Into(EntityType.Talent, 81, Spirit)["amount"]);

        Assert.Equal("Ink.Talent.ExtraResourcesAtStart.AdditionalSpirit",
            (string?)Into(EntityType.Talent, 81, Spirit)["source"]);
    }

    private static JsonNode Scaled(int id) =>
        Generated(EntityType.Ability, id)
            .OfType<JsonNode>()
            .Single(gain => Sources(gain).Contains("Warmaster.Toughness.MaxToughnessStrengthScaler"));

    [Fact]
    public void HelenaStatesEachAbilitysToughnessAsTheShareOfHerCeilingItsScalerIs()
    {
        Assert.Equal(0.15, (double?)Scaled(977)["shareOfMaximum"]);
        Assert.Equal(0.25, (double?)Scaled(979)["shareOfMaximum"]);
        Assert.Equal(0.3, (double?)Scaled(984)["shareOfMaximum"]);

        Assert.Null((double?)Scaled(977)["amount"]);
        Assert.Equal(ResourceGains.PoolFraction, (string?)Scaled(977)["sourceKind"]);

        foreach (var id in (int[])[977, 979, 984])
        {
            Assert.Equal(2, Sources(Scaled(id)).Count);
        }
    }

    [Fact]
    public void InfernalWaveGeneratesTheFortyCindersItsOwnTextPrints()
    {
        var into = Into(EntityType.Ability, 1276, Primary);

        Assert.Equal(40, (double?)into["amount"]);
        Assert.Equal(ResourceGains.RowProduct, (string?)into["sourceKind"]);
        Assert.Contains("Firemage.FireBombs.ResourcePerOrb", (string?)into["source"]);
        Assert.Null((string?)into["per"]);
    }

    [Fact]
    public void ColdSnapGeneratesOneWinterOrbIntoTheTertiaryPool()
    {
        var into = Into(EntityType.Ability, 1020, Tertiary);

        Assert.Equal(1, (double?)into["amount"]);
        Assert.Equal("Rime.InstantSingleDamage.OrbGain", (string?)into["source"]);
    }

    [Fact]
    public void BurstingIceStatesTheLiveAnimaRowAndNotTheOneTheDesignersRetired()
    {
        var gains = Generated(EntityType.Ability, 1031);

        Assert.Single(gains);
        Assert.Contains(
            "Rime.CastedDebuffAoeDamage.ResourceGainPerHit",
            (string?)Into(EntityType.Ability, 1031, Primary)["source"] ?? string.Empty);
    }

    [Fact]
    public void TariqsTwoHeavyStrikesStateTheSameShareOfHisFuryPool()
    {
        Assert.Equal(
            (double?)Into(EntityType.Ability, 1239, Primary)["shareOfMaximum"],
            (double?)Into(EntityType.Ability, 1929, Primary)["shareOfMaximum"]);

        Assert.Equal(0.12, (double?)Into(EntityType.Ability, 1929, Primary)["shareOfMaximum"]);
    }

    [Fact]
    public void BlockProcStatesNoComboPointBecauseItsOwnChanceRowIsZero()
    {
        Assert.Null(
            Export.Loaded.Documents
                .First(document => document.Type == EntityType.Ability && document.Id == 969)
                .Body[Field]);
    }

    [Fact]
    public void BrilliantFlashGeneratesTwelvePercentOfMaximumMana()
    {
        var into = Into(EntityType.Ability, 1855, Mana);

        Assert.Equal(0.12, (double?)into["shareOfMaximum"]);
        Assert.Equal(172.8, (double?)into["amount"]);
        Assert.Equal(ResourceGains.PoolFraction, (string?)into["sourceKind"]);
    }

    [Fact]
    public void OmnistrikeStatesItsBaseManaRefundApartFromThePerStackOne()
    {
        var mana = Generated(EntityType.Ability, 1863)
            .OfType<JsonNode>()
            .Where(gain => (string?)gain["resource"] == Mana)
            .ToArray();

        Assert.Equal(2, mana.Length);
        Assert.Contains(mana, gain => (double?)gain["amount"] == 100);
        Assert.Contains(mana, gain => (string?)gain["per"] == "AdditionalStack");
    }

    [Fact]
    public void MartialCommandCarriesTheSpiritPointsShieldThrowGenerates()
    {
        Assert.Equal(2, (double?)Into(EntityType.Talent, 14, Spirit)["amount"]);
        Assert.Equal(2, (double?)Into(EntityType.Ability, 985, Spirit)["amount"]);
    }

    [Fact]
    public void WrathOfWinterCarriesTheOrbTheAbilityItGrantsGenerates()
    {
        var into = Into(EntityType.Effect, 1387, Tertiary);

        Assert.Equal(1, (double?)into["amount"]);
        Assert.Equal("Rime.AutoDamageProjectile.OrbReward", (string?)into["source"]);
    }

    [Fact]
    public void VisionsOfGrandeurStatesOnTheTraitWhatItsPowerStates() =>
        Assert.Equal(
            (string?)Into(EntityType.TraitPower, 363, Spirit)["source"],
            (string?)Into(EntityType.Trait, 47, Spirit)["source"]);

    [Fact]
    public void RunicProliferationStatesTheGenerationItsOwnModifierDeclares()
    {
        var into = Into(EntityType.Effect, 1335, Primary);

        Assert.Equal(3, (double?)into["amount"]);
        Assert.Equal("Vigor.InstantResourceGain.ResourceGain", (string?)into["source"]);
        Assert.Equal(ResourceGains.ConstantsRow, (string?)into["sourceKind"]);
    }

    [Fact]
    public void VanguardStatesTheShareOfMaxToughnessItsOwnModifierCaptures()
    {
        var into = Into(EntityType.Effect, 1224, Second);

        Assert.Equal(
            "Warmaster.MoveToTargetStun.Talent.ComboPointAndToughnessGen.Buff.ToughnessGen",
            (string?)into["source"]);
        Assert.Equal(ResourceGains.PoolFraction, (string?)into["sourceKind"]);
    }

    [Fact]
    public void NaturalKnowledgeCarriesTheManaTheAbilityItGrantsGenerates()
    {
        var gains = Generated(EntityType.Talent, 198);
        var into = Into(EntityType.Talent, 198, Mana);

        Assert.Single(gains);
        Assert.Equal(14.4, (double?)into["amount"]);
        Assert.Equal(
            "Mosse.CastedSingleTargetProjectile.Talent.DamageToMana.ManaGainOfMaxMana",
            (string?)into["source"]);
        Assert.Equal(ResourceGains.PoolFraction, (string?)into["sourceKind"]);
    }

    [Fact]
    public void TheCelestialStatesTheSpiritPointTheAbilityItGrantsGenerates()
    {
        var into = Into(EntityType.Blessing, 702, Spirit);

        Assert.Equal(1, (double?)into["amount"]);
    }

    [Fact]
    public void RestoreManaStatesTheShareOfMaxManaOnThePowerAndOnItsEffect()
    {
        Assert.Equal("RelicPower.ManaPotion.MaxManaFraction",
            (string?)Into(EntityType.RelicPower, 255, Mana)["source"]);

        Assert.Equal("RelicPower.ManaPotion.MaxManaFraction",
            (string?)Into(EntityType.Effect, 159, Mana)["source"]);
    }

    [Fact]
    public void VisionsOfGrandeurStatesTheSpiritPointsItsOwnRowsFeed()
    {
        var into = Into(EntityType.TraitPower, 363, Spirit);

        Assert.Contains("Trait.WeaponAndSpiritPoints.SpiritPointsGain", (string?)into["source"]);
    }

    [Fact]
    public void TheWorkerThatDeclaresTheRowStatesTheGainAndTheLauncherThatOnlySharesItsNamespaceDoesNot()
    {
        Assert.Equal(0.00143, (double?)Into(EntityType.Effect, 1745, Primary)["shareOfMaximum"]);

        Assert.Null(
            Export.Loaded.Documents
                .First(document => document.Type == EntityType.Ability && document.Id == 1235)
                .Body[Field]);
    }

    [Fact]
    public void BothOfGundesStrikesStateThePainTheOneOfThemBindsAndTheOtherPrints()
    {
        Assert.Equal(50, (double?)Into(EntityType.Ability, 1410, Primary)["amount"]);
        Assert.Equal(50, (double?)Into(EntityType.Ability, 1411, Primary)["amount"]);
        Assert.Equal("Pain", (string?)Into(EntityType.Ability, 1410, Primary)["resourceName"]);
    }

    [Fact]
    public void EveryEffectCarryingAGenerationDeclaresItsChannelPrintsItOrBelongsToAnAbilityStatingIt() =>
        Assert.All(
            Export.Loaded.Documents.Where(document => document.Type == EntityType.Effect),
            document =>
            {
                if (document.Body[Field] is not JsonArray gains) return;

                var declared = (document.Body["modifiers"] as JsonArray ?? [])
                    .OfType<JsonNode>()
                    .Select(modifier => (string?)modifier["attribute"])
                    .ToArray();

                var text = document.Body["description"]?.ToString() ?? string.Empty;

                foreach (var gain in gains.OfType<JsonNode>())
                {
                    var owner = document.Body["partOf"] ?? document.Body["appliedBy"];

                    var borrowed = owner is not null &&
                                   Export.Loaded.Documents
                                       .Where(other =>
                                           other.Type == EntityType.Ability &&
                                           other.Id == (int?)owner["id"])
                                       .SelectMany(other => (other.Body[Field] as JsonArray ?? []).OfType<JsonNode>())
                                       .Any(other => (string?)other["source"] == (string?)gain["source"]);

                    Assert.True(
                        declared.Contains((string?)gain["resource"]) ||
                        ((string?)gain["resourceName"] is { Length: > 0 } noun &&
                         text.Contains(noun, StringComparison.OrdinalIgnoreCase)) ||
                        borrowed,
                        $"effect {document.Id} carries {(string?)gain["source"]} and neither declares it, " +
                        "prints it, nor belongs to an ability that states it");
                }
            });

    [Fact]
    public void OneRowAndChannelCarryOneResourceNameEverywhereTheyAreStated() =>
        Assert.All(
            Written()
                .GroupBy(gain => ((string?)gain["resource"], (string?)gain["source"]))
                .ToArray(),
            group => Assert.Single(
                group.Select(gain => (string?)gain["resourceName"]).Distinct(StringComparer.Ordinal)));

    [Fact]
    public void EveryTriggerIsReadFromTheRowThatSuppliesTheNumber() =>
        Assert.All(
            Written(),
            gain =>
            {
                if (gain["per"] is null) return;

                Assert.True(
                    Sources(gain).Count <= 1 ||
                    gain["amount"] is not null ||
                    gain["shareOfMaximum"] is not null,
                    $"{(string?)gain["source"]} states no number and names several rows, " +
                    $"so none of them can be said to be the row 'per {(string?)gain["per"]}' comes from");
            });

    [Fact]
    public void NoGenerationStatesANumberAtOrBelowNothing() =>
        Assert.All(
            Written(),
            gain =>
            {
                if ((double?)gain["amount"] is { } amount) Assert.True(amount > 0);
                if ((double?)gain["amountOnCritical"] is { } critical) Assert.True(critical > 0);
                if ((double?)gain["chance"] is { } chance) Assert.True(chance is > 0 and < 1);
            });

    [Fact]
    public void NoGenerationStatesACriticalOnlyRowAsAnUnconditionalAmount() =>
        Assert.All(
            Written(),
            gain => Assert.False(
                Numbers.Normalize((string?)gain["source"] ?? string.Empty)
                    .Contains(Crit, StringComparison.Ordinal) &&
                gain["amount"] is not null));

    [Fact]
    public void EveryGenerationStatingACriticalAmountBesideAnotherNamesBothRows() =>
        Assert.All(
            Written(),
            gain =>
            {
                if (gain["amountOnCritical"] is null || gain["amount"] is null) return;

                Assert.False(string.IsNullOrEmpty((string?)gain["sourceOnCritical"]));
                Assert.NotEqual((string?)gain["source"], (string?)gain["sourceOnCritical"]);
            });

    [Fact]
    public void NoTwoGenerationsOnOneDocumentShareAChannelAndASource() =>
        Assert.All(
            Carrying(),
            gains => Assert.Equal(
                gains.Count,
                gains.Select(gain => ((string?)gain["resource"], (string?)gain["source"])).Distinct().Count()));

    [Fact]
    public void EveryRowGroupIsSegmentsOfTheRowTheAmountComesFrom() =>
        Assert.All(
            Written(),
            gain =>
            {
                if ((string?)gain["rowGroup"] is not { } group) return;

                Assert.Contains($".{group}.", (string?)gain["source"] ?? string.Empty);
            });

    [Fact]
    public void ATalentScopedAmountOnABaseAbilityStatesTheTalentItIsScopedTo()
    {
        var cleave = Generated(EntityType.Ability, 1926)
            .OfType<JsonNode>()
            .Single(gain => ((string?)gain["source"] ?? string.Empty)
                .Split('.')
                .Contains(TalentSegment, StringComparer.Ordinal));

        Assert.Equal("Talent.Empowered.Cleave", (string?)cleave["rowGroup"]);
    }

    [Fact]
    public void EveryGenerationNamesWhereItsNumberCameFrom() =>
        Assert.All(
            Written(),
            gain =>
            {
                Assert.False(string.IsNullOrEmpty((string?)gain["source"]));
                Assert.False(string.IsNullOrEmpty((string?)gain["sourceKind"]));
            });

    [Fact]
    public void EveryDocumentTypeThatCarriesAGenerationOrdersItTheSameWay() =>
        Assert.All(
            Carrying(),
            gains => Assert.Equal(
                [.. gains
                    .OrderBy(gain => (string?)gain["resource"], StringComparer.Ordinal)
                    .ThenBy(gain => (string?)gain["source"], StringComparer.Ordinal)
                    .ThenBy(gain => (double?)gain["amount"] ?? 0)
                    .Select(gain => gain.ToJsonString())],
                [.. gains.Select(gain => gain.ToJsonString())]));

    [Fact]
    public void NoGenerationStatesAShareOfMoreThanTheWholePool() =>
        Assert.All(
            Written(),
            gain =>
            {
                if ((double?)gain["shareOfMaximum"] is not { } share) return;

                Assert.True(share is > 0 and <= 1, $"{(string?)gain["source"]} states a share of {share}");
            });

    [Fact]
    public void ChainLightningStatesTheFuryItsOwnSentencePrintsAndTheTalentedCaseApart()
    {
        var printed = Into(EntityType.Ability, 1237, Primary);

        Assert.Equal(0.012, (double?)printed["shareOfMaximum"]);
        Assert.Equal("Ink.BouncyProjectile.ResourceGainPerTarget", (string?)printed["source"]);
        Assert.Null(printed["rowGroup"]);

        var talented = Generated(EntityType.Ability, 1236)
            .Single(gain => (double?)gain["shareOfMaximum"] is 0.018);

        Assert.Equal(
            "Talent.BouncyProjectile.IncreasedFuryGeneration", (string?)talented["rowGroup"]);
    }

    [Fact]
    public void SecondWindCarriesNoGenerationBecauseItsOwnTextStatesNone() =>
        Assert.Null(
            Export.Loaded.Documents
                .First(document => document.Type == EntityType.Talent && document.Id == 219)
                .Body[Field]);

    [Fact]
    public void TheStunAndTheDamageChargeAppliesCarryNoneOfTheToughnessItsTalentBuffDeclares()
    {
        Assert.Equal(
            "Warmaster.MoveToTargetStun.Talent.ComboPointAndToughnessGen.Buff.ToughnessGen",
            (string?)Into(EntityType.Effect, 1224, Second)["source"]);

        foreach (var id in (int[])[1255, 1256])
        {
            Assert.Null(
                Export.Loaded.Documents
                    .First(document => document.Type == EntityType.Effect && document.Id == id)
                    .Body[Field]);
        }

        Assert.Null(
            Export.Loaded.Documents
                .First(document => document.Type == EntityType.Ability && document.Id == 974)
                .Body[Field]);
    }

    [Fact]
    public void NoGenerationNamesABytecodeFrameAndNoNumber() =>
        Assert.All(
            Written(),
            gain => Assert.False(
                ((string?)gain["source"] ?? string.Empty).Contains('@') &&
                gain["amount"] is null && gain["amountOnCritical"] is null && gain["shareOfMaximum"] is null));

    [Fact]
    public void EveryTalentScopedSourceStatesTheTalentSegmentInItsRowGroup() =>
        Assert.All(
            Written(),
            gain =>
            {
                var talents = Sources(gain)
                    .Select(Talented)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (talents is not [_]) return;

                Assert.Contains(
                    TalentSegment,
                    ((string?)gain["rowGroup"] ?? string.Empty).Split('.'),
                    StringComparer.Ordinal);
            });

    [Fact]
    public void EntropysClaimGeneratesFourWithCritSixFromTheTwoRowsItsOwnTextPrints()
    {
        var into = Into(EntityType.Ability, 1876, Primary);

        Assert.Equal(4, (double?)into["amount"]);
        Assert.Equal(6, (double?)into["amountOnCritical"]);
        Assert.Equal("Tick", (string?)into["per"]);
    }

    [Fact]
    public void EachChannelCarriesTheNameItsOwnHerosSentencesPrint()
    {
        Assert.Equal("Chrona", (string?)Into(EntityType.Ability, 1889, Primary)["resourceName"]);
        Assert.Equal("Anima", (string?)Into(EntityType.Ability, 1027, Primary)["resourceName"]);
        Assert.Equal("Energy", (string?)Into(EntityType.Ability, 1207, Primary)["resourceName"]);
        Assert.Equal("Combo Points", (string?)Into(EntityType.Ability, 1207, Second)["resourceName"]);
        Assert.Equal("Winter Orbs", (string?)Into(EntityType.Ability, 1020, Tertiary)["resourceName"]);
        Assert.Equal("Toughness", (string?)Into(EntityType.Ability, 977, Second)["resourceName"]);
        Assert.Equal("Pain", (string?)Into(EntityType.Ability, 1411, Primary)["resourceName"]);
    }

    [Fact]
    public void ManaIsNamedOnEveryChannelEntryBecauseNoHerosSentencesCallItAnythingElse() =>
        Assert.All(
            Written().Where(gain => (string?)gain["resource"] == Mana),
            gain => Assert.Equal(Mana, (string?)gain["resourceName"]));

    [Fact]
    public void OneRowAndChannelCarryOneTriggerEverywhereTheyAreStated() =>
        Assert.All(
            Written()
                .Where(gain => gain["per"] is not null)
                .GroupBy(gain => ((string?)gain["resource"], (string?)gain["source"])),
            group => Assert.Single(
                group.Select(gain => (string?)gain["per"]).Distinct(StringComparer.Ordinal)));

    [Fact]
    public void HelenaGeneratesToughnessOnABlockAtTheShareHerOwnScalerDividesTo()
    {
        var block = Into(EntityType.Ability, 970, Second);

        Assert.Equal(0.24 / 7.7, (double?)block["shareOfMaximum"] ?? 0, 9);
        Assert.Equal("Block", (string?)block["rowGroup"]);
    }

    [Fact]
    public void GreaterShockwaveAddsTheShareOfMaximumItsOwnTalentRowStates()
    {
        var added = Generated(EntityType.Ability, 979)
            .Single(gain => (double?)gain["shareOfMaximum"] is 0.1);

        Assert.Equal("Talent.IncreasedToughnessAndDamage", (string?)added["rowGroup"]);
    }

    [Fact]
    public void ExpansiveMindReplenishesTheShareOfMaximumManaItsExecutionDeclares()
    {
        var replenished = Into(EntityType.Talent, 61, Mana);

        Assert.Equal(0.02, (double?)replenished["shareOfMaximum"]);
        Assert.Equal("Tick", (string?)replenished["per"]);
    }

    [Fact]
    public void OwedInBloodStatesTheOrbItsOwnModifierDeclares()
    {
        var owed = Into(EntityType.Effect, 3245, Tertiary);

        Assert.Equal(1, (double?)owed["amount"]);
        Assert.Equal(ResourceGains.ModifierMagnitude, (string?)owed["sourceKind"]);
    }

    [Fact]
    public void WidowsBiteStatesTheStrikeItsOwnSentenceCountsBy() =>
        Assert.Equal("Strike", (string?)Into(EntityType.Ability, 1207, Second)["per"]);

    [Fact]
    public void NoRecordExportsACostBetweenNothingAndOne() =>
        Assert.All(
            Export.Loaded.Documents,
            document =>
            {
                if ((double?)document.Body["cost"] is not { } cost) return;

                Assert.False(
                    cost is > 0 and < 1,
                    $"{document.Type} {document.Id} exports {cost}, a share of a pool rather than an amount");
            });

    [Fact]
    public void HammerStormSpendsTheSameOnItsEffectAsOnTheAbilityThatAppliesIt() =>
        Assert.Equal(
            (double?)Export.Loaded.Documents
                .First(document => document.Type == EntityType.Ability && document.Id == 1242)
                .Body["cost"],
            (double?)Export.Loaded.Documents
                .First(document => document.Type == EntityType.Effect && document.Id == 1782)
                .Body["cost"]);

    private static IReadOnlyList<IReadOnlyList<JsonNode>> Carrying() =>
    [
        .. Export.Loaded.Documents
            .Select(document => document.Body[Field] as JsonArray)
            .OfType<JsonArray>()
            .Select(gains => (IReadOnlyList<JsonNode>)[.. gains.OfType<JsonNode>()]),
    ];

    private static IReadOnlyList<JsonNode> Written() => [.. Carrying().SelectMany(gains => gains)];
}
