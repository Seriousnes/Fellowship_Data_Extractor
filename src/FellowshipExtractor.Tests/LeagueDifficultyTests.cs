using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class LeagueDifficultyTests
{
    private const string LevelTable = "fellowship/Content/dungeons/DT_DifficultyToLevel.DT_DifficultyToLevel";
    private const string LeagueTable = "fellowship/Content/MatchMaking/DT_GameFinderLeagues.DT_GameFinderLeagues";
    private const string BucketDifficultyField = "Difficulty";

    private static readonly string[] BucketFields = ["RankedBuckets", "EternalBuckets", "PinnacleBuckets"];

    private static IReadOnlyList<LeagueEntry> Leagues => Export.Loaded.Settings.Leagues;

    [Fact]
    public void TheCreatureLadderIsTheDifficultiesTheLeaguesList()
    {
        var listed = Leagues.SelectMany(league => league.Difficulties).Distinct().Order();

        Assert.Equal(listed, Export.Loaded.Settings.DifficultyScaling.Keys.Order());
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
    public void ABucketDifficultyIsAStepIntoItsLeaguesDifficulties()
    {
        var leagues = new Leagues(Export.Provider, [.. AssetRegistry.ReadAssets(Export.Provider)]);
        var rows = Export.Provider.LoadPackageObject<UDataTable>(LeagueTable).RowMap
            .ToDictionary(entry => entry.Key.Text, entry => entry.Value);

        var stepped = leagues.All
            .Where(league => league.Kind is LeagueKind.Eternal or LeagueKind.Pinnacle)
            .ToArray();

        Assert.All(stepped, league =>
        {
            var steps = BucketFields
                .Select(field => PropertyWalk.Get(rows[league.Row], field)?.GenericValue)
                .OfType<UScriptArray>()
                .SelectMany(buckets => buckets.Properties)
                .Select(bucket => bucket?.GenericValue)
                .OfType<FScriptStruct>()
                .Select(bucket => bucket.StructType)
                .OfType<IPropertyHolder>()
                .Select(bucket => PropertyWalk.AsOrdinal(PropertyWalk.Get(bucket, BucketDifficultyField)?.GenericValue) ?? 0)
                .ToArray();

            Assert.Equal(Enumerable.Range(0, league.Difficulties.Count), steps);
        });

        Assert.Equal(stepped.SelectMany(league => league.Difficulties).Distinct().Order(), leagues.Matched);
        Assert.Equal(21, leagues.Matched.Min());
    }

    [Fact]
    public void TheEternalRowsNameTheDungeonsADifficultyRuns()
    {
        var rows = Export.Provider.LoadPackageObject<UDataTable>(LevelTable).RowMap;

        Assert.Contains(rows.Keys, key => key.Text == "21");
        Assert.Equal(32, Leagues.Single(league => league.Kind == LeagueKind.Eternal).Difficulties.Count);
    }
}
