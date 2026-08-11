using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class DifficultyLadderTests
{
    private static readonly Leagues Leagues = Export.Resolver.Leagues;

    private static readonly CreatureStats Stats = Export.Resolver.CreatureStats;

    [Fact]
    public void EveryLeagueCoversDifficultiesTheScalingLadderCarries()
    {
        Assert.NotEmpty(Leagues.All);

        foreach (var league in Leagues.All)
        {
            Assert.NotEmpty(league.Difficulties);
            Assert.Equal(league.MinimumDifficulty, league.Difficulties.Min());
            Assert.Equal(league.MaximumDifficulty, league.Difficulties.Max());

            foreach (var difficulty in league.Difficulties)
            {
                Assert.True(
                    Stats.Scaling.Health.ContainsKey(difficulty),
                    $"{league.Name} covers difficulty {difficulty}, which the health ladder does not carry.");
            }
        }
    }

    [Fact]
    public void OneLeagueRunsOnPastTheOthersAndTheRestAreShortAndOrdered()
    {
        var running = Leagues.All.Where(league => league.Kind == LeagueKind.Eternal).ToArray();

        var open = Assert.Single(running);

        Assert.True(
            open.Difficulties.Count > 100,
            $"{open.Name} covers {open.Difficulties.Count} difficulties, too few to run on.");

        foreach (var league in Leagues.All.Where(league => league.Kind == LeagueKind.Ranked))
        {
            Assert.True(
                league.MaximumDifficulty < open.MinimumDifficulty,
                $"{league.Name} reaches {league.MaximumDifficulty}, past where {open.Name} starts.");
        }
    }

    [Fact]
    public void ANamespaceThatScalesApartCarriesItsOwnLadderAndTheRestReadTheDefault()
    {
        Assert.True(Stats.ScaledApart > 0, "No namespace scales apart from Default.");
        Assert.Equal(Stats.ScaledApart, Stats.Scaling.Namespaces.Count);

        foreach (var (scope, apart) in Stats.Scaling.Namespaces)
        {
            Assert.True(
                apart.Health.Count > 0 || apart.Damage.Count > 0,
                $"{scope} is listed as scaling apart but carries neither ladder.");

            foreach (var difficulty in apart.Health.Keys)
            {
                Assert.True(
                    Stats.Scaling.Health.ContainsKey(difficulty),
                    $"{scope} scales at difficulty {difficulty}, which Default does not carry.");
            }
        }
    }

    [Fact]
    public void EveryCreatureTheTableStatesAnythingAboutStatesAPrimaryAttribute()
    {
        var boss = Stats.For("FK_BossRangerCaptain");

        Assert.NotNull(boss);
        Assert.NotNull(boss.Intellect);
        Assert.NotNull(boss.Strength);
        Assert.NotNull(boss.Agility);

        var creatures = Export.Loaded.Documents.Where(row => row.Type == EntityType.Creature).ToArray();

        Assert.NotEmpty(creatures);

        Assert.All(creatures, row => Assert.NotNull(
            row.Body["intellect"] ?? row.Body["strength"] ?? row.Body["agility"]));
    }

    [Fact]
    public void AnOverriddenPrimaryAttributeWinsOverTheSharedOne()
    {
        var shared = Stats.For("FK_BossRangerCaptain");
        var overridden = Stats.For("CM_BossAina");

        Assert.NotNull(shared);
        Assert.NotNull(overridden);

        Assert.Equal(120, shared.Intellect);
        Assert.Equal(100, overridden.Intellect);
    }
}
