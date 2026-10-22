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
            Assert.Equal(2, enchant.Stats!.Count);
            Assert.All(enchant.Stats, stat => Assert.Null(stat.Lookup));
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
    public void EachSecondaryStatGrantsOnePerRank() =>
        Assert.All(Enchants.SelectMany(enchant => enchant.Stats!), stat => Assert.Equal(1, stat.Coefficient));

    [Fact]
    public void ItemLevelStepsTheRankAtOneFiftyAndThreeHundred()
    {
        var power = Export.Loaded.Settings.Items.StatEnchantPower!;

        Assert.Equal("RCIM_Constant", power.Interpolation);
        Assert.Equal([new(1, 4), new(150, 5), new(300, 6)], power.Points);
    }

    [Fact]
    public void AnEnchantIsNamedByTheNamesItsFunctionPrintsNotItsTableRow()
    {
        var sphinx = Enchants.Single(trait => trait.Id == 69);

        Assert.Equal("Sphinx's Cunning", sphinx.NameByHero!["Aeona"]);
        Assert.Equal("Ogre's Might / Vampire's Guile / Sphinx's Cunning", sphinx.Name);
        Assert.Equal(sphinx.Name, sphinx.Power!.Name);
    }

    [Fact]
    public void EachStatTokenOfAnEnchantDescriptionPrintsItsCoefficientPerRank()
    {
        foreach (var enchant in Enchants)
        {
            Assert.NotNull(enchant.Description);

            var tokens = enchant.Placeholders!.Where(placeholder => placeholder.PerRank == true).ToArray();

            Assert.Equal(
                enchant.Stats!.Select(stat => (stat.Attribute, stat.Coefficient)).Order(),
                tokens.Select(token => (token.Attribute!, token.Value!.Value)).Order());
        }
    }
}
