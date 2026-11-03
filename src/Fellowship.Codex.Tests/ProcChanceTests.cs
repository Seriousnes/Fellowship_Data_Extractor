using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ProcChanceTests(Export export)
{
    private const int DragonboneDice = 778;

    private ProcRollSource Offered(EntityType type, string name) =>
        Assert.Single(ProcChance.Sources(export.Store),
            source => source.Type == type && PublicTypes.NameOf(source.Face) == name);

    [Fact]
    public void Dragonbone_Dice_is_offered_as_its_relic_and_rolls_its_rate_times_one_plus_crit_chance()
    {
        var relic = Offered(EntityType.Item, "Orb of Awakened Gaze");
        var roll = Assert.Single(relic.Rolls);

        Assert.Equal("item", relic.Key);
        Assert.IsType<ItemDocument>(relic.Face);
        Assert.Equal(DragonboneDice, relic.Carrier.Id);
        Assert.IsType<RelicPassiveDocument>(relic.Carrier);
        Assert.True(ProcChance.Evaluable(roll));
        Assert.Equal(1.25, ProcChance.Rate(roll, attribute => attribute == "CritChance" ? 0.25 : 0));

        Assert.DoesNotContain(ProcChance.Sources(export.Store), source => source.Type == EntityType.RelicPassive);
        Assert.Same(relic.Face, ProcChance.Source(export.Store, EntityType.RelicPassive, DragonboneDice)!.Face);
    }

    [Fact]
    public void Detonate_rolls_only_behind_Reign_of_Fire_so_the_talent_is_offered_and_the_ability_is_not()
    {
        var talents = ProcChance.Sources(export.Store)
            .Where(source => source.Type == EntityType.Talent && PublicTypes.NameOf(source.Face) == "Reign of Fire")
            .ToArray();

        Assert.NotEmpty(talents);
        Assert.All(talents, talent => Assert.Contains(talent.Rolls,
            roll => roll.Row.EndsWith(".CastedDotDetonateChargeRecoveryProc.PPM", StringComparison.Ordinal) && roll.ScalesWithHaste));
        Assert.DoesNotContain(ProcChance.Sources(export.Store), source => PublicTypes.NameOf(source.Face) == "Detonate");

        var detonates = export.Store.Entities
            .OfType<AbilityDocument>()
            .Where(ability => ability is { Name: "Detonate", ProcRolls.Count: > 0 })
            .ToArray();

        Assert.NotEmpty(detonates);

        foreach (var detonate in detonates)
        {
            Assert.Empty(ProcChance.Baseline(detonate));
            Assert.All(detonate.ProcRolls!, roll =>
                Assert.Contains(talents, talent => talent.Face.Id == roll.Talent?.Id && roll.Talent.Type == EntityType.Talent));
        }
    }

    [Fact]
    public void Every_offered_source_is_named_resolves_by_its_face_and_every_roll_evaluates()
    {
        foreach (var source in ProcChance.Sources(export.Store))
        {
            var found = ProcChance.Source(export.Store, source.Type, source.Face.Id);

            Assert.NotNull(found);
            Assert.NotNull(source.Face.Name);
            Assert.Equal(source.Rolls.Count, found.Rolls.Count);
            Assert.All(source.Rolls, roll => Assert.True(ProcChance.Evaluable(roll)));
            Assert.All(source.Rolls, roll => Assert.Null(roll.Talent));
        }
    }

    [Fact]
    public void A_hasted_roll_multiplies_its_rate_by_one_plus_haste()
    {
        var hasted = Assert.Single(ProcChance.Sources(export.Store)
            .Where(source => source.Type == EntityType.Talent && PublicTypes.NameOf(source.Face) == "The Best Defense")
            .SelectMany(source => source.Rolls)
            .DistinctBy(roll => roll.Row));

        Assert.Equal("Warmaster.BouncyProjectile.Talent.DamageCooldownReset.PPM", hasted.Row);
        Assert.True(hasted.ScalesWithHaste);
        Assert.Equal(2.6, ProcChance.Rate(hasted, attribute => attribute == "Haste" ? 0.3 : 0)!.Value, 10);
    }

    [Fact]
    public void One_roll_at_one_per_minute_after_one_second_is_one_sixtieth()
    {
        var settings = export.Store.Settings.ProcPerMinuteRoll;
        var roll = ProcChance.Roll(1, 1, 0, false, settings);

        Assert.Equal(1.0 / 60, roll.Base, 10);
        Assert.Equal(1, roll.Drought);
        Assert.Null(roll.Floor);
        Assert.Equal(1.0 / 60, roll.Chance, 10);
    }

    [Fact]
    public void A_long_drought_raises_the_chance_and_caps_at_a_thousand_seconds()
    {
        var settings = export.Store.Settings.ProcPerMinuteRoll;

        Assert.Equal(1, ProcChance.Roll(1, 1, 90, false, settings).Drought);
        Assert.Equal(2.5, ProcChance.Roll(1, 1, 120, false, settings).Drought, 10);
        Assert.Equal(ProcChance.Roll(1, 1, 1000, false, settings).Drought, ProcChance.Roll(1, 1, 5000, false, settings).Drought);
    }

    [Fact]
    public void Before_the_first_proc_the_chance_is_at_least_the_rate_over_eight()
    {
        var settings = export.Store.Settings.ProcPerMinuteRoll;
        var roll = ProcChance.Roll(2, 0, 0, true, settings);

        Assert.Equal(0.25, roll.Floor);
        Assert.Equal(0.25, roll.Chance);
    }
}
