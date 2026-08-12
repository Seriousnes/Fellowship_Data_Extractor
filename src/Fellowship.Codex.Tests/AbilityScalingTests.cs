using System.Net;

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

        var drawn = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(hero.PrimaryStat, drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hero_and_one_primary_read_the_same_magnitude()
    {
        var (id, _) = Scaling();
        var hero = RouteTests.Factory.Store.Settings.Heroes[0];
        var stat = hero.PrimaryStat;

        var named = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}&{stat}=1000",
            Xunit.TestContext.Current.CancellationToken);

        var supplied = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?{stat}=1000", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(named, supplied);
    }

    [Fact]
    public async Task Several_primaries_at_once_take_the_hero_the_request_names()
    {
        var (id, _) = Scaling();

        var hero = RouteTests.Factory.Store.Settings.Heroes
            .First(candidate => string.Equals(candidate.PrimaryStat, "Strength", StringComparison.Ordinal));

        var both = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}&Strength=1000&Intellect=4000",
            Xunit.TestContext.Current.CancellationToken);

        var strengthOnly = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}&Strength=1000",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(strengthOnly, both);
    }

    [Fact]
    public async Task An_ability_takes_the_hero_and_nothing_else_the_item_takes()
    {
        var (id, _) = Scaling();

        var response = await client.GetAsync(
            $"/api/ability/{id}/tooltip?itemLevel=200", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
