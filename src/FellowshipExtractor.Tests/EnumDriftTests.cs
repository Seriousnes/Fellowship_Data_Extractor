using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The enums name words the build states. A build that adds a hero or renames an armour type has to
/// fail here rather than be absorbed.
///
/// What these compare is the members the source generator declared from the vocabulary committed
/// under src/Fellowship.SDK against the same projection over the live export. That is stronger than
/// comparing a committed file against itself: a committed vocabulary left behind by a newer export
/// fails here.
/// </summary>
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
        // A hero's internal codename is not a name a player reads. A codename that resolves in the
        // enum is a name no reader could look up, the same failure EntityStore already closed for
        // the hero query parameter. Skip a hero whose codename happens to equal another hero's
        // shipped name, since the enum legitimately carries that word as the other hero's name and
        // asserting its absence would be asserting against the vocabulary itself.
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
