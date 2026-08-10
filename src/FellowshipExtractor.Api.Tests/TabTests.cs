using System.Net;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TabTests(Export export)
{
    private static readonly EntityType[] Unlisted = [EntityType.TalentSlot, EntityType.MountPower];

    private static readonly EntityType[] Untabbed = [EntityType.Gem, EntityType.LegendaryStat];

    [Fact]
    public void Tabbed_untabbed_and_unlisted_state_every_type_exactly_once()
    {
        var tabbed = PublicTypes.All.Where(type => type.Listed && type.Tabbed).Select(type => type.Type).ToArray();
        var untabbed = PublicTypes.All.Where(type => type.Listed && !type.Tabbed).Select(type => type.Type).ToArray();
        var unlisted = PublicTypes.All.Where(type => !type.Listed).Select(type => type.Type).ToArray();

        var placed = tabbed.Concat(untabbed).Concat(unlisted).ToArray();

        Assert.Equal(Enum.GetValues<EntityType>().Order().ToArray(), placed.Order().ToArray());
        Assert.Equal(placed.Length, placed.Distinct().Count());

        Assert.Equal(Untabbed.Order(), untabbed.Order());
        Assert.Equal(Unlisted.Order(), unlisted.Order());
    }

    [Fact]
    public void A_type_that_holds_no_tab_still_states_a_position()
    {
        Assert.Equal(
            PublicTypes.All.Count,
            PublicTypes.All.Select(type => type.Position).Distinct().Count());

        Assert.All(PublicTypes.Tabs, tab => Assert.True(tab.Tabbed && tab.Listed));
    }

    [Fact]
    public void The_tabs_open_in_the_order_the_code_states()
    {
        Assert.Equal(
            ["Items", "Abilities", "Effects", "Talents", "Blessings", "Gem Traits"],
            export.View.Codex.Tabs.Take(6).Select(tab => tab.Label));
    }

    [Fact]
    public void The_tabs_hold_neither_gems_nor_legendary_stats()
    {
        var tabbed = export.View.Codex.Tabs.Select(tab => tab.Type).ToArray();

        Assert.DoesNotContain(EntityType.Gem, tabbed);
        Assert.DoesNotContain(EntityType.LegendaryStat, tabbed);
    }

    [Fact]
    public void The_codex_lists_neither_talent_slots_nor_mount_powers()
    {
        var listed = export.View.Codex.Types.Select(type => type.Type).ToArray();

        Assert.DoesNotContain(EntityType.TalentSlot, listed);
        Assert.DoesNotContain(EntityType.MountPower, listed);

        Assert.Contains(EntityType.Gem, listed);
        Assert.Contains(EntityType.LegendaryStat, listed);
    }

    [Fact]
    public void The_store_still_holds_every_talent_slot_the_talents_list_reads()
    {
        var slots = export.Store.Entities.OfType<TalentSlotDocument>().ToArray();

        Assert.Equal(216, slots.Length);
        Assert.NotNull(export.Store.Slot(slots[0].TalentId));
    }

    [Fact]
    public void An_unlisted_slug_is_no_type_to_filter_by()
    {
        Searching.Ask(export, out var error, ("type", "talent-slot"));

        Assert.Equal(PublicTypes.NoSuchType("talent-slot"), error);

        var offered = error!.Split("The types are ")[1];

        Assert.DoesNotContain("talent-slot", offered, StringComparison.Ordinal);
        Assert.DoesNotContain("mount-power", offered, StringComparison.Ordinal);
        Assert.Contains("gem", offered, StringComparison.Ordinal);
    }

    [Fact]
    public void An_untabbed_slug_still_filters()
    {
        var results = Searching.Run(export, ("type", "gem"));

        Assert.Equal(EntityType.Gem, results.Chosen);
        Assert.Equal(24, results.Total);
        Assert.All(results.Rows, row => Assert.IsType<GemDocument>(row));
    }

    [Fact]
    public void The_search_index_answers_nothing_of_an_unlisted_type()
    {
        var index = Searching.Index(export);

        Assert.DoesNotContain(
            index.Search("Macabre Stratagem", 50), hit => hit.Document is TalentSlotDocument);

        Assert.DoesNotContain(
            index.Search("Arcalian Steed", 50), hit => hit.Document is MountPowerDocument);
    }
}

public class TabRouteTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task A_browse_naming_an_unlisted_type_answers_a_problem_naming_the_legal_slugs()
    {
        var page = await client.GetStringAsync(
            "/browse?type=talent-slot", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("is not a type. The types are", page, StringComparison.Ordinal);
        Assert.Contains("That filter could not be read.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("talent-slot,", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_of_an_unlisted_type_is_not_found()
    {
        var response = await client.GetAsync("/entity/talent-slot/1", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task No_route_answers_for_an_unlisted_type()
    {
        string[] addresses =
        [
            "/api/talent-slot/1", "/api/talent-slot/1/tooltip",
            "/api/mount-power/445", "/api/mount-power/445/tooltip",
        ];

        foreach (var address in addresses)
        {
            var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_browse_of_an_untabbed_type_marks_its_tab_current()
    {
        var page = await client.GetStringAsync("/browse?type=gem", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("aria-current=\"page\"", page, StringComparison.Ordinal);
        Assert.Contains("Gems", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_catalog_publishes_the_tabs_in_order()
    {
        var body = await client.GetStringAsync("/api/catalog", Xunit.TestContext.Current.CancellationToken);

        using var reading = System.Text.Json.JsonDocument.Parse(body);

        var types = reading.RootElement.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("item", types[0].GetProperty("slug").GetString());
        Assert.True(types[0].GetProperty("tabbed").GetBoolean());

        var positions = types.Select(type => type.GetProperty("position").GetInt32()).ToArray();

        Assert.Equal(positions.OrderBy(position => position), positions);
        Assert.DoesNotContain(types, type => type.GetProperty("slug").GetString() == "talent-slot");

        Assert.NotEmpty(reading.RootElement.GetProperty("sortings").EnumerateArray());
    }
}
