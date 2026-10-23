using Fellowship.SDK.Documents;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class StatEnchantTests
{
    private const string Category = "ItemTrait.TraitCategory.StatEnchant";

    private static IReadOnlyList<EnchantDocument> Enchants =>
        [.. EntityStoreCorpusTests.Store.Entities.OfType<EnchantDocument>()];

    [Fact]
    public void EveryEnchantIsOneDocumentPerPrimaryStatAndStatesItsStats()
    {
        var stats = Export.Loaded.Settings.Heroes.Select(hero => hero.PrimaryStat).Distinct().Order().ToArray();

        Assert.Equal(6, Enchants.Select(enchant => enchant.Id).Distinct().Count());
        Assert.DoesNotContain(
            EntityStoreCorpusTests.Store.Entities.OfType<TraitDocument>(), trait => trait.CategoryTag == Category);

        foreach (var versions in Enchants.GroupBy(enchant => enchant.Id))
        {
            Assert.Equal(stats, versions.Select(enchant => enchant.PrimaryStat.ToString()).Order());

            foreach (var enchant in versions)
            {
                Assert.Equal(Category, enchant.CategoryTag);
                Assert.Equal(2, enchant.Stats!.Count);
                Assert.All(enchant.Stats, stat => Assert.Null(stat.Lookup));
            }
        }
    }

    [Theory]
    [InlineData(65, PrimaryStat.Strength, "Ability_StatEnchant_Strength_Type1")]
    [InlineData(65, PrimaryStat.Intellect, "Ability_StatEnchant_Intellect_Type1")]
    [InlineData(65, PrimaryStat.Agility, "Ability_StatEnchant_Agility_Type1")]
    public void AnEnchantNameIsTheStringItsFunctionChoosesOnThePrimaryStat(int id, PrimaryStat stat, string key)
    {
        _ = Export.Provider;

        Assert.Equal(
            StringTables.Find("ST_Abilities", key),
            Enchants.Single(enchant => enchant.Id == id && enchant.PrimaryStat == stat).Name);
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
    public void AnEnchantIsNamedByTheNameItsFunctionPrintsAndItsPowerByTheSameStat()
    {
        var sphinx = Enchants.Single(enchant => enchant.Id == 69 && enchant.PrimaryStat == PrimaryStat.Intellect);

        Assert.Equal("Sphinx's Cunning", sphinx.Name);
        Assert.Equal(sphinx.Name, sphinx.Power!.Name);
        Assert.DoesNotContain(EntityStoreCorpusTests.Store.Entities, document => document.Name?.Contains(" / ") is true &&
            document is EnchantDocument or TraitPowerDocument { PrimaryStat: not null } or EffectDocument { PrimaryStat: not null });
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

    [Fact]
    public void TheEnchantCardTextIsTheTraitTooltipWidgetsOwn()
    {
        var text = Export.Loaded.Settings.TraitTooltip!;

        Assert.Equal("Equipped Rank", text.EquippedRank);
        Assert.Equal("Rank Sources", text.RankSources);
        Assert.Equal("- Gear slot: ", text.GearSlot);
        Assert.Equal("Head", text.ItemTypes[0].Type);
        Assert.Contains(text.ItemTypes, type => type is { Type: "Necklace", Printed: "Necklace" });
    }
}
