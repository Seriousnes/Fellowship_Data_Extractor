using System.Globalization;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class DifficultyTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private LeagueEntry Eternal =>
        Settings.Leagues.Single(league => league.Kind == LeagueKind.Eternal);

    [Fact]
    public void The_ladder_labels_every_difficulty_by_the_league_that_covers_it()
    {
        var ladder = Difficulties.Ladder(Settings);

        Assert.Equal(Settings.DifficultyScaling.Health.Count, ladder.Count);

        var eternal = Eternal;

        var opening = ladder.Single(step => step.Difficulty == eternal.MinimumDifficulty);

        Assert.Equal(eternal.Name, opening.League);
        Assert.Equal(1, opening.Step);
        Assert.Equal($"{eternal.Name} +1", opening.Label);

        var closing = ladder.Single(step => step.Difficulty == eternal.MaximumDifficulty);

        Assert.Equal($"{eternal.Name} +{eternal.Difficulties.Count}", closing.Label);
    }

    [Fact]
    public void No_casual_league_covers_a_step_of_the_ladder()
    {
        var casual = Settings.Leagues
            .Where(league => league.Kind == LeagueKind.Casual)
            .Select(league => league.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(casual);
        Assert.DoesNotContain(Difficulties.Ladder(Settings), step => casual.Contains(step.League));
    }

    [Fact]
    public void A_difficulty_no_ladder_carries_falls_back_to_the_first_one()
    {
        var lowest = Settings.DifficultyScaling.Health.Keys.Min();
        var listed = Eternal.MaximumDifficulty;

        Assert.Equal(lowest, export.Store.Difficulty(null));
        Assert.Equal(lowest, export.Store.Difficulty("nonsense"));
        Assert.Equal(lowest, export.Store.Difficulty("100000"));
        Assert.Equal(listed, export.Store.Difficulty(listed.ToString(CultureInfo.InvariantCulture)));
    }
}
