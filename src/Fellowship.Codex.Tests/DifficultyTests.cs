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

        Assert.Equal(Settings.DifficultyScaling.Count, ladder.Count);

        var eternal = Eternal;

        var opening = ladder.Single(step => step.Difficulty == eternal.MinimumDifficulty);

        Assert.Equal(eternal.Name, opening.League);
        Assert.Equal(1, opening.Step);
        Assert.Equal($"{eternal.Name} 1", opening.Label);

        var closing = ladder.Single(step => step.Difficulty == eternal.MaximumDifficulty);

        Assert.Equal($"{eternal.Name} {eternal.Difficulties.Count}", closing.Label);
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
    public void A_difficulty_scaling_entry_is_named_by_its_league_step()
    {
        var eternal = Eternal;

        Assert.Equal(("base", "Base"), (Settings.DifficultyScaling[1].Name, Settings.DifficultyScaling[1].Label));
        Assert.Equal("eternal 1", Settings.DifficultyScaling[eternal.MinimumDifficulty].Name);
        Assert.Equal(
            $"Eternal {eternal.Difficulties.Count}",
            Settings.DifficultyScaling[eternal.MaximumDifficulty].Label);
    }

    [Fact]
    public void Health_and_damage_scales_never_fall_as_the_eternal_difficulty_rises()
    {
        var levels = Eternal.Difficulties.Order().Select(difficulty => Settings.DifficultyScaling[difficulty]).ToArray();

        Assert.All(levels.Zip(levels.Skip(1)), pair =>
        {
            Assert.True(pair.Second.Health > pair.First.Health, $"Health falls at {pair.Second.Label}.");
            Assert.True(pair.Second.Damage > pair.First.Damage, $"Damage falls at {pair.Second.Label}.");
        });
    }

    [Fact]
    public void Deathless_katrine_at_eternal_27_has_the_health_fellowshiplogs_reports()
    {
        var katrine = export.Store.Entities.OfType<CreatureDocument>().Single(row => row.Name == "Deathless Katrine");
        var difficulty = Settings.DifficultyScaling.Single(entry => entry.Value.Name == "eternal 27").Key;

        Assert.Equal(6581643, Difficulties.Health(katrine, Settings, difficulty)!.Value, 6581643 * 0.0001);
    }

    [Fact]
    public void A_creature_picks_only_the_difficulties_its_dungeon_runs_at()
    {
        var katrine = export.Store.Entities.OfType<CreatureDocument>().Single(row => row.Name == "Deathless Katrine");
        var dungeon = export.Store.All(katrine.FoundIn![0].Type, katrine.FoundIn[0].Id).OfType<DungeonDocument>().Single();

        Assert.Equal(dungeon.Difficulties, export.Store.Ladder(katrine).Select(step => step.Difficulty));
        Assert.Equal(dungeon.Difficulties![0], export.Store.Difficulty(null, katrine));
    }

    [Fact]
    public void A_difficulty_no_ladder_carries_falls_back_to_the_first_one()
    {
        var lowest = Settings.DifficultyScaling.Keys.Min();
        var listed = Eternal.MaximumDifficulty;

        Assert.Equal(lowest, export.Store.Difficulty(null));
        Assert.Equal(lowest, export.Store.Difficulty("nonsense"));
        Assert.Equal(lowest, export.Store.Difficulty("100000"));
        Assert.Equal(listed, export.Store.Difficulty(listed.ToString(CultureInfo.InvariantCulture)));
    }
}
