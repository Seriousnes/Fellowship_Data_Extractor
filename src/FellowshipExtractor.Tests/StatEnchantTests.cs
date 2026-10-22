using Fellowship.SDK.Documents;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class StatEnchantTests
{
    private const string Category = "ItemTrait.TraitCategory.StatEnchant";

    private static IReadOnlyList<TraitDocument> Enchants =>
        [.. EntityStoreCorpusTests.Store.Entities.OfType<TraitDocument>().Where(trait => trait.CategoryTag == Category)];

    [Fact]
    public void EveryEnchantNamesItselfForEveryHeroAndStatesItsStats()
    {
        var heroes = Export.Loaded.Settings.Heroes.Select(hero => hero.Name).Order().ToArray();

        Assert.Equal(6, Enchants.Count);

        foreach (var enchant in Enchants)
        {
            Assert.Equal(heroes, enchant.NameByHero!.Keys.Order());
            Assert.Equal(2, enchant.Stats!.Count(stat => stat.Coefficient > 0 && stat.Lookup is not null));
        }
    }

    [Theory]
    [InlineData(65, "Helena", "Ability_StatEnchant_Strength_Type1")]
    [InlineData(65, "Ardeos", "Ability_StatEnchant_Intellect_Type1")]
    [InlineData(65, "Elarion", "Ability_StatEnchant_Agility_Type1")]
    public void AnEnchantNameIsTheStringItsFunctionChoosesOnTheHerosPrimaryAttribute(int id, string hero, string key)
    {
        _ = Export.Provider;

        Assert.Equal(StringTables.Find("ST_Abilities", key), Enchants.Single(trait => trait.Id == id).NameByHero![hero]);
    }

    [Fact]
    public void TheTwoSecondaryStatsReadFourAtPowerOneAndSixAtPowerThree()
    {
        var crit = Enchants.Single(trait => trait.Id == 65).Stats!.Single(stat => stat.Attribute == "CritRating");

        Assert.Equal(1, crit.Coefficient);
        Assert.Equal("RCIM_Linear", crit.Lookup!.Interpolation);
        Assert.Equal([new(1, 4), new(3, 6)], crit.Lookup.Points);
    }

    [Fact]
    public void ItemLevelStepsThePowerAtOneFiftyAndThreeHundred()
    {
        var power = Export.Loaded.Settings.Items.StatEnchantPower!;

        Assert.Equal("RCIM_Constant", power.Interpolation);
        Assert.Equal([new(1, 1), new(150, 2), new(300, 3)], power.Points);
    }
}
