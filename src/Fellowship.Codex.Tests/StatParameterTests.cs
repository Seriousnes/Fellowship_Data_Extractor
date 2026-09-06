using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class StatParameterTests(Export export)
{
    [Fact]
    public void Names_every_display_name_the_attribute_table_carries_once()
    {
        var rows = export.Store.Settings.StatCalculation.Attributes;

        var shared = rows.Values
            .Select(row => row.DisplayName)
            .Where(name => name is { Length: > 0 })
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expected = rows.Values
            .Select(row => row.DisplayName)
            .Where(name => name is { Length: > 0 } && !shared.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expected, export.Store.Stats.Names);
    }

    [Fact]
    public void Carries_the_stats_a_hero_sheet_prints_that_no_item_rolls()
    {
        Assert.Contains("Parry", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Dodge", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Cooldown Reduction", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leaves_a_name_two_attributes_share_for_the_printed_line()
    {
        Assert.Null(export.Store.Stats.Resolve("armor"));
        Assert.DoesNotContain("Armor", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolves_a_display_name_written_without_its_spaces()
    {
        Assert.Equal("BlockChance", export.Store.Stats.Resolve("blockchance"));
        Assert.Equal("CritChance", export.Store.Stats.Resolve("Critical Strike"));
    }
}

public sealed class PublishedStatTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public PublishedStatTests(RouteTests.Factory factory) => client = factory.CreateClient();

    [Fact]
    public async Task Every_stat_the_table_names_is_accepted_on_an_ability_tooltip()
    {
        var id = RouteTests.Factory.Store.Entities.First(row => row is AbilityDocument).Id;

        foreach (var stat in RouteTests.Factory.Store.Stats.Names)
        {
            var key = stat.Replace(" ", string.Empty, StringComparison.Ordinal);

            using var response = await client.TooltipAsync(
                EntityType.Ability, id, new TooltipRequest().Rated(key, 10m));

            Assert.True(response.IsSuccessStatusCode, $"'{key}' answered {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task A_name_the_table_does_not_carry_is_refused()
    {
        var id = RouteTests.Factory.Store.Entities.First(row => row is AbilityDocument).Id;

        using var response = await client.TooltipAsync(
            EntityType.Ability, id, new TooltipRequest().Rated("Nonesuch", 10m));

        var body = await response.BodyAsync();

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nonesuch", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Armor_on_an_item_reads_as_the_printed_line()
    {
        var id = RouteTests.Factory.Store.Entities
            .OfType<ItemDocument>()
            .First(row => row.Stats is { Count: > 0 })
            .Id;

        var html = await client.TooltipHtmlAsync(EntityType.Item, id, new TooltipRequest { Armor = 303m });

        Assert.Contains("303", html, StringComparison.Ordinal);
    }
}
