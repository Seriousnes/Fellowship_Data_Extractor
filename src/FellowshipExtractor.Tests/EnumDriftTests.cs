using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class EnumDriftTests
{
    [Fact]
    public void MembersAreTheSetTheSettingsState()
    {
        var expected = SettingsEnums.Members(Export.Loaded.Settings);

        Assert.Equal(expected["ArmorType"], Enum.GetNames<ArmorType>());
        Assert.Equal(expected["PrimaryStat"], Enum.GetNames<PrimaryStat>());
        Assert.Equal(expected["Hero"], Enum.GetNames<Hero>());
        Assert.Equal(expected["GemType"], Enum.GetNames<GemType>());
    }

    [Fact]
    public void ARarityMemberCarriesItsShippedOrdinal()
    {
        foreach (var (name, tier) in SettingsEnums.Rarities(Export.Loaded.Settings))
        {
            Assert.Equal(tier, (int)Enum.Parse<Rarity>(name));
        }
    }

    [Fact]
    public void AnAbilityCategoryMemberCarriesItsShippedOrdinal()
    {
        var categories = SettingsEnums.Categories(Export.Loaded.Settings);

        Assert.NotEmpty(categories);

        foreach (var (name, value) in categories)
        {
            Assert.Equal(value, (int)Enum.Parse<AbilityCategory>(name));
        }

        Assert.Equal(categories.Select(category => category.Name), Enum.GetNames<AbilityCategory>());
    }
}
