using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public sealed class AbilityScalingTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public AbilityScalingTests(RouteTests.Factory factory) => client = factory.CreateClient();

    private static (int Id, string Attribute) Scaling()
    {
        foreach (var document in RouteTests.Factory.Store.Entities.OfType<AbilityDocument>())
        {
            foreach (var token in document.Placeholders ?? [])
            {
                if (token.Coefficient is null) continue;
                if (!string.Equals(token.Attribute, "PrimaryAttribute", StringComparison.Ordinal)) continue;

                return (document.Id, token.Attribute!);
            }
        }

        throw new InvalidOperationException(
            "No ability carries a PrimaryAttribute coefficient, so nothing reads the hero's stat.");
    }

    [Fact]
    public async Task An_ability_prints_the_stat_the_hero_names()
    {
        var (id, _) = Scaling();
        var hero = RouteTests.Factory.Store.Settings.Heroes[0];

        var html = await client.TooltipHtmlAsync(id, new AbilityTooltipRequest { Hero = hero.Name });

        Assert.Contains(hero.PrimaryStat, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hero_and_one_primary_read_the_same_magnitude()
    {
        var (id, _) = Scaling();
        var hero = RouteTests.Factory.Store.Settings.Heroes[0];
        var stat = hero.PrimaryStat;

        var named = await client.TooltipHtmlAsync(
            id, new AbilityTooltipRequest { Hero = hero.Name, Stats = Stats.Rated(stat, 1000m) });

        var supplied = await client.TooltipHtmlAsync(
            id, new AbilityTooltipRequest { Stats = Stats.Rated(stat, 1000m) });

        Assert.Equal(named, supplied);
    }

    [Fact]
    public async Task Several_primaries_at_once_take_the_hero_the_request_names()
    {
        var (id, _) = Scaling();

        var hero = RouteTests.Factory.Store.Settings.Heroes
            .First(candidate => string.Equals(candidate.PrimaryStat, "Strength", StringComparison.Ordinal));

        var both = await client.TooltipHtmlAsync(
            id,
            new AbilityTooltipRequest
            {
                Hero = hero.Name,
                Stats = Stats.Rated("Strength", 1000m).Rated("Intellect", 4000m),
            });

        var strengthOnly = await client.TooltipHtmlAsync(
            id, new AbilityTooltipRequest { Hero = hero.Name, Stats = Stats.Rated("Strength", 1000m) });

        Assert.Equal(strengthOnly, both);
    }

}
