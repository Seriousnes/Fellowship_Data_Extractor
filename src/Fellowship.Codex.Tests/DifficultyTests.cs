namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class DifficultyTests(Export export)
{
    private const int NoorTheBetrayer = 168;

    private const int BetrayersArrow = 750;

    private const double NoorAtEternal44 = 12_365_031;

    private SettingsFile Settings => export.Store.Settings;

    private LeagueEntry Eternal =>
        Settings.Leagues.Single(league => league.Kind == LeagueKind.Eternal);

    private CreatureDocument Noor =>
        export.Store.Entities.OfType<CreatureDocument>().Single(row => row.Id == NoorTheBetrayer);

    [Fact]
    public void Noor_at_Eternal_44_reads_within_a_hundredth_of_a_percent_of_the_figure_the_game_showed()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var health = Difficulties.Health(Noor, Settings, difficulty);

        Assert.NotNull(health);
        Assert.Equal(NoorAtEternal44, health.Value, NoorAtEternal44 * 0.0001);
        Assert.Equal(12_364_857d, health.Value, 1d);
    }

    [Fact]
    public void The_ladder_labels_every_difficulty_by_the_league_that_covers_it()
    {
        var ladder = Difficulties.Ladder(Settings);

        Assert.Equal(Settings.DifficultyScaling.Health.Count, ladder.Count);

        var eternal = Eternal;

        var opening = ladder.Single(step => step.Difficulty == eternal.MinimumDifficulty);

        Assert.Equal(eternal.Name, opening.League);
        Assert.Equal(0, opening.Step);
        Assert.Equal($"{eternal.Name} +0", opening.Label);

        var deep = ladder.Single(step => step.Difficulty == eternal.MinimumDifficulty + 44);

        Assert.Equal($"{eternal.Name} +44", deep.Label);
    }

    [Fact]
    public void A_difficulty_no_ladder_carries_falls_back_to_the_first_one()
    {
        var lowest = Settings.DifficultyScaling.Health.Keys.Min();

        Assert.Equal(lowest, export.Store.Difficulty(null));
        Assert.Equal(lowest, export.Store.Difficulty("nonsense"));
        Assert.Equal(lowest, export.Store.Difficulty("100000"));
        Assert.Equal(65, export.Store.Difficulty("65"));
    }
}
