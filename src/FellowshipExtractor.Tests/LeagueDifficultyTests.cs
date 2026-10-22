using CUE4Parse.UE4.Assets.Exports.Engine;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class LeagueDifficultyTests
{
    private const string LevelTable = "fellowship/Content/dungeons/DT_DifficultyToLevel.DT_DifficultyToLevel";

    private static IReadOnlyList<LeagueEntry> Leagues => Export.Loaded.Settings.Leagues;

    [Fact]
    public void TheCreatureLadderIsTheDifficultiesTheLeaguesList()
    {
        var listed = Leagues.SelectMany(league => league.Difficulties).Distinct().Order();

        Assert.Equal(listed, Export.Loaded.Settings.DifficultyScaling.Health.Keys.Order());
        Assert.Equal(listed, Export.Loaded.Settings.DifficultyScaling.Damage.Keys.Order());
    }

    [Fact]
    public void ADungeonRunsOnlyAtADifficultyAnEternalOrPinnacleBucketMatches()
    {
        var bucketed = Leagues
            .Where(league => league.Kind is LeagueKind.Eternal or LeagueKind.Pinnacle)
            .SelectMany(league => league.Difficulties)
            .ToHashSet();

        var dungeons = Export.Loaded.Documents.Where(row => row.Type == EntityType.Dungeon).ToArray();

        Assert.Contains(dungeons, row => row.Body["difficulties"] is not null);
        Assert.All(dungeons, row => Assert.All(
            row.Body["difficulties"]?.AsArray().Select(step => step!.GetValue<int>()) ?? [],
            difficulty => Assert.Contains(difficulty, bucketed)));
    }

    [Fact]
    public void EightDungeonsAreAvailableForCasual()
    {
        var casual = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Dungeon && row.Body["availableForCasual"]?.GetValue<bool>() is true)
            .ToArray();

        Assert.Equal(8, casual.Length);
        Assert.All(casual, row => Assert.Null(row.Body["difficulties"]?.AsArray().FirstOrDefault(step => step!.GetValue<int>() < 21)));
    }

    [Fact]
    public void TheEternalRowsNameTheDungeonsADifficultyRuns()
    {
        var rows = Export.Provider.LoadPackageObject<UDataTable>(LevelTable).RowMap;

        Assert.Contains(rows.Keys, key => key.Text == "21");
        Assert.Equal(32, Leagues.Single(league => league.Kind == LeagueKind.Eternal).Difficulties.Count);
    }
}
