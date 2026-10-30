using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class StringTableSourceTests
{
    private const string ChallengeStrings = "ST_Challenges";
    private const string TopRated = "Challenge_Dungeon_TopRated_Title";
    private const string AbilityStrings = "ST_Abilities";

    [Fact]
    public void AStringTableEntryTheLocresLacksReadsItsSourceString()
    {
        _ = Export.Provider;

        Assert.Equal("Top Rated", StringTables.Find(ChallengeStrings, TopRated));
    }

    [Fact]
    public void EveryChallengeExtensionNamesADefinedChallenge()
    {
        var challenges = new ChallengeRewards(Export.Provider, [.. AssetRegistry.ReadAssets(Export.Provider)]);

        Assert.True(challenges.Challenges > 0);
    }

    [Fact]
    public void AQuotedMultilineSourceStringMatchesTheLocres()
    {
        var localized = LocalizedText.Read(Export.Provider, LocalizedText.DefaultCulture)
            .Where(entry => entry.Namespace == AbilityStrings)
            .ToDictionary(entry => entry.Key, entry => entry.Text);

        var sources = StringTableSources.Read(Export.Provider)
            .Where(entry => entry.Namespace == AbilityStrings)
            .ToArray();

        Assert.Contains(sources, entry => entry.Text.Contains('\n'));
        Assert.All(sources, entry => Assert.Equal(localized[entry.Key], entry.Text));
    }
}
