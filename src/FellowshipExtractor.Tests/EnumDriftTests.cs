using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class EnumDriftTests
{
    [Fact]
    public void MembersAreTheSetTheVocabularyStates()
    {
        var expected = EnumsCommand.Members(Export.Loaded.Vocabulary);

        Assert.Equal(expected["ArmorType"], Enum.GetNames<ArmorType>());
        Assert.Equal(expected["PrimaryStat"], Enum.GetNames<PrimaryStat>());
        Assert.Equal(expected["Hero"], Enum.GetNames<Hero>());
        Assert.Equal(expected["GemTypeName"], Enum.GetNames<GemTypeName>());
    }

    [Fact]
    public void ARarityMemberCarriesItsShippedOrdinal()
    {
        foreach (var (name, tier) in EnumsCommand.Rarities(Export.Loaded.Vocabulary))
        {
            Assert.Equal(tier, (int)Enum.Parse<Rarity>(name));
        }
    }

    [Fact]
    public void HeroesAreNamedAsThePlayerReadsThem()
    {
        var names = Export.Loaded.Vocabulary.Heroes.Select(hero => hero.Name).ToHashSet(StringComparer.Ordinal);
        var enumNames = Enum.GetNames<Hero>();

        foreach (var hero in Export.Loaded.Vocabulary.Heroes)
        {
            if (names.Contains(hero.Codename)) continue;

            Assert.Contains(hero.Name, enumNames);
            Assert.DoesNotContain(hero.Codename, enumNames);
        }
    }
}
