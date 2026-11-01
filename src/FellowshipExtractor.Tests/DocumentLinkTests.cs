using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// A document links the abilities and effects its assets reference, and a tuning row names the talent of the row's
/// hero when talents of two heroes hold it.
/// </summary>
public sealed class DocumentLinkTests
{
    private const int AlzeracsShackle = 348;
    private const int AlzeracsShackleAbility = 152;
    private const int AlzeracsShackleSlow = 66;
    private const int PricklyVine = 1057;
    private const int PricklyVineSummon = 1440;
    private const int StormShield = 25;
    private const int StormShieldStrike = 1946;
    private const int StormShieldReduction = 2698;
    private const int BloodStrike = 1410;
    private const int RebelliousStrike = 1411;
    private const int SweepingStrike = 981;
    private const int SpiritedVortex = 1091;
    private const int DebilitatingVortex = 97;
    private const int Enfeebled = 2487;
    private const int SharpenedBlade = 215;
    private const int SharpenedBladeDebuff = 2199;
    private const int EnlightenedSoul = 56;

    private const string ReducedAttackSpeed = "ReducedAttackSpeed.AttackSpeedMultiplier";
    private const string MeikoReducedAttackSpeed = "Meiko.DurationalAoeDamageBuff.Talent.ReducedAttackSpeed.AttackSpeedMultiplier";
    private const string TickInterval = "Vigor.SingleTargetChargedHot.TickInterval";

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export.");

    private static TooltipNumber Audited(string type, int id, string number) =>
        Assert.Single(TooltipAudit.Run(Export.Loaded.Documents), found => found.Type == type && found.Id == id && found.Number == number);

    private static int[] Ids(JsonNode? references) =>
        [.. (references?.AsArray() ?? []).Select(reference => reference!["id"]!.GetValue<int>())];

    [Fact]
    public void AlzeracsShackleSlowsThroughAnEffectOfTheAbilityItGrants()
    {
        Assert.Contains(AlzeracsShackleAbility, Ids(Document(EntityType.WeaponPower, AlzeracsShackle)["grants"]));
        Assert.Equal(AlzeracsShackleAbility, Document(EntityType.Effect, AlzeracsShackleSlow)["partOf"]!["id"]!.GetValue<int>());

        foreach (var number in new[] { "65%", "40%" })
        {
            var slowed = Audited("weapon-power", AlzeracsShackle, number);

            Assert.Equal(TooltipNumberStatus.Elsewhere, slowed.Status);
            Assert.StartsWith($"effect {AlzeracsShackleSlow}:", slowed.Evidence, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PricklyVineHasTheDurationOfTheEffectItApplies()
    {
        Assert.Contains(PricklyVineSummon, Ids(Document(EntityType.Ability, PricklyVine)["applies"]));

        var duration = Audited("ability", PricklyVine, "27");

        Assert.Equal(TooltipNumberStatus.Elsewhere, duration.Status);
        Assert.Equal($"effect {PricklyVineSummon}: 27", duration.Evidence);
    }

    [Fact]
    public void StormShieldGrantsTheAbilityItsModifierActorReferences()
    {
        Assert.Contains(StormShieldStrike, Ids(Document(EntityType.DungeonAffix, StormShield)["grants"]));

        var reduction = Audited("dungeon-affix", StormShield, "50%");

        Assert.Equal(TooltipNumberStatus.Elsewhere, reduction.Status);
        Assert.Contains($"effect {StormShieldReduction}: IncomingDamageMultiplier MultiplyCompound 0.5", reduction.Evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BloodStrikeHasTheStrengthCoefficientOfTheAbilityItsClassDerivesFrom()
    {
        var parent = Document(EntityType.Ability, BloodStrike)["parentClass"]!;

        Assert.Equal(RebelliousStrike, parent["id"]!.GetValue<int>());

        var strength = Audited("ability", BloodStrike, "315%");

        Assert.Equal(TooltipNumberStatus.Elsewhere, strength.Status);
        Assert.Contains($"ability {RebelliousStrike}: 3.15", strength.Evidence, StringComparison.Ordinal);
    }

    /// <summary>
    /// Meiko's ReducedAttackSpeed row is read by Meiko's Spirited Vortex and by Helena's Sweeping Strike, whose
    /// Sharpened Blade effect applies it. Each document names its own hero's talent.
    /// </summary>
    [Theory]
    [InlineData(EntityType.Ability, SpiritedVortex, DebilitatingVortex)]
    [InlineData(EntityType.Effect, Enfeebled, DebilitatingVortex)]
    [InlineData(EntityType.Ability, SweepingStrike, SharpenedBlade)]
    [InlineData(EntityType.Effect, SharpenedBladeDebuff, SharpenedBlade)]
    public void ReducedAttackSpeedNamesTheTalentOfTheDocumentsHero(EntityType type, int id, int talent)
    {
        var row = Document(type, id)["tuning"]![ReducedAttackSpeed]!;

        Assert.Equal(talent, row["talent"]!["id"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(DebilitatingVortex)]
    [InlineData(SharpenedBlade)]
    public void ATalentHoldingReducedAttackSpeedNamesItself(int talent)
    {
        var row = Document(EntityType.Talent, talent)["tuning"]![MeikoReducedAttackSpeed]!;

        Assert.Equal(talent, row["talent"]!["id"]!.GetValue<int>());
    }

    [Fact]
    public void EnlightenedSoulNamesItselfOnItsTickInterval()
    {
        var row = Document(EntityType.Talent, EnlightenedSoul)["tuning"]![TickInterval]!;

        Assert.Equal(EnlightenedSoul, row["talent"]!["id"]!.GetValue<int>());
    }
}
