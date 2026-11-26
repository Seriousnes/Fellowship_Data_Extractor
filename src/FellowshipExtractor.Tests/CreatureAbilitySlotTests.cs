using System.Text.Json;
using System.Text.Json.Nodes;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

/// <summary>
/// A creature's ability slots, each ability's targeting and the settings an enemy's actions read, against two creatures
/// of the Pirate Raid: Barbarous Lasher (<c>AIBC_PR_BarbarousLasher</c>) and First Mate Marrow (<c>AIBC_PR_BossAnchor</c>).
/// </summary>
public sealed class CreatureAbilitySlotTests
{
    private static T Document<T>(EntityType type, int id) where T : EntityDocument =>
        Export.Loaded.Documents.Single(row => row.Type == type && row.Id == id).Body.Deserialize<T>(EntityDocuments.Options)!;

    private static CreatureAbility Slot(int creature, int ability) =>
        Document<CreatureDocument>(EntityType.Creature, creature).Abilities!.Single(slot => slot.Ability.Id == ability);

    [Fact]
    public void BarbarousLashersAttackHasAThreeSecondCooldownAtPriorityZeroOnTheThreatTarget()
    {
        var slot = Slot(128, 610);

        Assert.Equal("TimerBased", slot.Activation);
        Assert.Equal(0, slot.Priority);
        Assert.Equal(new PhaseCooldown("FixedValue") { Seconds = 3 }, slot.Cooldown);
        Assert.Null(slot.DifficultyUnlocked);

        var attack = Document<AbilityDocument>(EntityType.Ability, 610);

        Assert.Equal("AIThreat", attack.TargetAcquisition);
        Assert.Equal("Threat", attack.TargetSelection!.Type);
        Assert.Equal("ThreatSorted", attack.TargetSelection.ThreatMode);
        Assert.True(attack.AutoAttack);
    }

    [Fact]
    public void BarbarousLashersCrushArmorUnlocksAtDifficulty19WithAnUninterruptibleCast()
    {
        var slot = Slot(128, 609);

        Assert.Equal(new PhaseCooldown("RandomRange") { MinSeconds = 9, MaxSeconds = 22 }, slot.InitialCooldown);
        Assert.Equal(new PhaseCooldown("FixedValue") { Seconds = 18 }, slot.Cooldown);
        Assert.Equal(new PhaseCooldown("FixedValue") { Seconds = 1.5 }, slot.PostAbilityPadding);
        Assert.Equal(2, slot.Priority);
        Assert.Equal(19, slot.DifficultyUnlocked);

        Assert.False(Document<AbilityDocument>(EntityType.Ability, 609).CastInterruptible);
    }

    [Fact]
    public void FirstMateMarrowsSlotsHaveTheirTimersAndPriorities()
    {
        var strike = Slot(125, 592);

        Assert.Equal(new PhaseCooldown("FixedValue") { Seconds = 3.5 }, strike.Cooldown);
        Assert.Equal(0, strike.Priority);
        Assert.Equal("Threat", Document<AbilityDocument>(EntityType.Ability, 592).TargetSelection!.Type);

        var wavestomp = Slot(125, 599);

        Assert.Equal(7, wavestomp.InitialCooldown!.Seconds);
        Assert.Equal(17, wavestomp.Cooldown!.Seconds);
        Assert.Equal(3, wavestomp.Priority);
        Assert.Equal("RandomActorSet", Document<AbilityDocument>(EntityType.Ability, 599).TargetSelection!.Type);

        var keelhaul = Slot(125, 1763);

        Assert.Equal(new PhaseCooldown("RandomRange") { MinSeconds = 15, MaxSeconds = 15 }, keelhaul.InitialCooldown);
        Assert.Equal(new PhaseCooldown("RandomRange") { MinSeconds = 40, MaxSeconds = 40 }, keelhaul.Cooldown);
        Assert.Equal(9, keelhaul.Priority);
        Assert.Equal(19, keelhaul.DifficultyUnlocked);

        var highTide = Slot(125, 1786);

        Assert.Equal(40, highTide.InitialCooldown!.Seconds);
        Assert.Equal(70, highTide.Cooldown!.Seconds);
        Assert.Equal(10, highTide.Priority);
    }

    [Fact]
    public void FirstMateMarrowHasItsPhasesByTag()
    {
        var phases = Document<CreatureDocument>(EntityType.Creature, 125).Phases!;

        Assert.Contains(phases, phase => phase.Tag == "AIPhase.DefaultCombat" && phase.Type == "Unique");
        Assert.Contains(phases, phase => phase.Tag == "AIPhase.Custom.PR.BossAnchor.HighTide");
    }

    [Fact]
    public void EverySlotOfADungeonCreatureHasAnActivationAndAPriority()
    {
        var slots = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Creature && row.Body["foundIn"] is JsonArray { Count: > 0 })
            .SelectMany(row => row.Body["abilities"]?.AsArray() ?? [])
            .Where(slot => slot!["ability"]!["type"]!.GetValue<string>() == "ability")
            .ToArray();

        Assert.Equal(738, slots.Length);
        Assert.All(slots, slot => Assert.NotNull(slot!["activation"]));
        Assert.All(slots, slot => Assert.NotNull(slot!["priority"]));
    }

    [Fact]
    public void OfTheDungeonCreaturesAbilities190HaveNoTargetPolicy()
    {
        var slotted = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Creature && row.Body["foundIn"] is JsonArray { Count: > 0 })
            .SelectMany(row => row.Body["abilities"]?.AsArray() ?? [])
            .Where(slot => slot!["ability"]!["type"]!.GetValue<string>() == "ability")
            .Select(slot => slot!["ability"]!["id"]!.GetValue<int>())
            .ToHashSet();

        var abilities = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Ability && slotted.Contains(row.Id))
            .ToArray();

        Assert.Equal(682, abilities.Length);
        Assert.Equal(190, abilities.Count(row => row.Body["targetAcquisition"] is null));
        Assert.Equal(306, abilities.Count(row =>
            row.Body["targetAcquisition"]?.GetValue<string>() == "AIThreat" &&
            row.Body["targetSelection"]?["type"]?.GetValue<string>() == "Threat"));
    }

    [Fact]
    public void TheSettingsHaveThePresetsThreatAndDungeonModifiers()
    {
        var settings = Export.Loaded.Settings;

        Assert.Equal(27, settings.EffectCalculationPresets.Count);
        Assert.Equal(0.2, settings.EffectCalculationPresets["HeroPhysicalAutoAttack"].DefaultSpread);
        Assert.True(settings.EffectCalculationPresets["HeroPhysicalAutoAttack"].Flags["CanMiss"]);

        Assert.Equal(0.97, settings.Threat!.ThreatDecayMultiplierPerTick);
        Assert.Equal(2000, settings.Threat.HealThreatRange);
        Assert.Equal(5000, settings.Threat.SecondaryThreatMaxDistance);
        Assert.Equal(0.5, settings.Threat.NonTankThreatGenerationScaler);

        Assert.Equal(9, settings.AiGlobals!.DefaultCCCooldown);
        Assert.Equal(2, settings.AiGlobals.CCAbilityPadding);
        Assert.Equal(2, settings.AiGlobals.InterruptAbilityPadding);
        Assert.Equal(4, settings.AiGlobals.DefaultAILockoutDurationForCCInterrupts);

        Assert.Equal(1.4, settings.PartyScaling!.HealthScale4p);
        Assert.Equal(1.8, settings.PartyScaling.HealthScale5p);
        Assert.Equal(1.2, settings.PartyScaling.HolyTrinityNerf["HolyTrinityNerf.EnemyHealthModifier.Level0.Multiplier"]);

        Assert.Equal(1728, settings.DungeonModifiers["StormShield.BaseDamage"]);
        Assert.Equal(1.05, settings.DungeonModifiers["Ultimatum.BossBoost.DamageScaler"]);
        Assert.Equal(41, settings.DungeonModifiers["AbilityUnlock.Pinnacle.Ultimate"]);
    }
}
