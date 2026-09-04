using System.Net;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class TabTests(Export export)
{
    private static readonly EntityType[] Unlisted =
    [
        EntityType.TalentSlot, EntityType.MountPower, EntityType.Modifier, EntityType.Hero,
        EntityType.LegendaryStat, EntityType.GemType,
    ];

    [Fact]
    public void Every_type_names_one_of_the_three_categories()
    {
        Assert.Equal(
            Enum.GetValues<EntityType>().Order().ToArray(),
            PublicTypes.All.Select(type => type.Type).Order().ToArray());

        Assert.All(PublicTypes.All, type => Assert.Contains(type.Category, Categories.All));

        Assert.Equal(
            Unlisted.Order(),
            PublicTypes.All.Where(type => !type.Listed).Select(type => type.Type).Order());
    }

    [Fact]
    public void Every_listed_type_states_a_position_of_its_own()
    {
        Assert.Equal(
            PublicTypes.All.Count,
            PublicTypes.All.Select(type => type.Position).Distinct().Count());

        Assert.All(PublicTypes.Listed, type => Assert.True(type.Listed));
    }

    [Fact]
    public void Each_category_opens_its_tabs_in_the_order_the_code_states()
    {
        Assert.Equal(
            ["Abilities", "Effects", "Talents", "Traits", "Gem Traits", "Set Bonuses"],
            export.View.Codex.Tabs(Categories.Spells).Take(6).Select(tab => tab.Label));

        Assert.Equal(
            ["Items", "Blessings", "Gems", "Currencies", "Mounts", "Companion Pets"],
            export.View.Codex.Tabs(Categories.Items).Select(tab => tab.Label));

        Assert.Equal(
            ["Dungeons", "Creatures"],
            export.View.Codex.Tabs(Categories.Dungeons).Select(tab => tab.Label));
    }

    [Fact]
    public void No_category_offers_a_gem_type_or_a_legendary_stat()
    {
        var tabbed = Categories.All.SelectMany(export.View.Codex.Tabs).Select(tab => tab.Type).ToArray();

        Assert.DoesNotContain(EntityType.GemType, tabbed);
        Assert.DoesNotContain(EntityType.LegendaryStat, tabbed);
        Assert.Contains(EntityType.Gem, tabbed);
    }

    [Fact]
    public void The_codex_lists_neither_talent_slots_nor_mount_powers()
    {
        var listed = export.View.Codex.Types.Select(type => type.Type).ToArray();

        Assert.DoesNotContain(EntityType.TalentSlot, listed);
        Assert.DoesNotContain(EntityType.MountPower, listed);
        Assert.DoesNotContain(EntityType.LegendaryStat, listed);
        Assert.DoesNotContain(EntityType.GemType, listed);

        Assert.Contains(EntityType.Gem, listed);
    }

    [Fact]
    public void The_store_still_holds_every_talent_slot_the_talents_list_reads()
    {
        var slots = export.Store.Entities.OfType<TalentSlotDocument>().ToArray();

        Assert.Equal(216, slots.Length);
        Assert.NotNull(export.Store.Slot(slots[0].Talent.Id));
    }

    [Fact]
    public void An_unlisted_slug_is_no_type_to_filter_by()
    {
        Searching.Ask(export, out var error, ("type", "talent-slot"));

        Assert.Equal(PublicTypes.NoSuchType("talent-slot"), error);

        var offered = error!.Split("The types are ")[1];

        Assert.DoesNotContain("talent-slot", offered, StringComparison.Ordinal);
        Assert.DoesNotContain("mount-power", offered, StringComparison.Ordinal);
        Assert.DoesNotContain("legendary-stat", offered, StringComparison.Ordinal);
        Assert.DoesNotContain("gem-type", offered, StringComparison.Ordinal);
        Assert.Contains("gem", offered, StringComparison.Ordinal);
    }

    [Fact]
    public void A_type_of_another_category_is_no_type_to_filter_by_here()
    {
        Searching.AskIn(export, Categories.Items, out var error, ("type", "ability"));

        Assert.Equal(PublicTypes.NotInCategory("ability", Categories.Items), error);
        Assert.Contains("The Items types are item, blessing, gem", error, StringComparison.Ordinal);

        Searching.AskIn(export, Categories.Items, out var fine, ("type", "gem"));

        Assert.Null(fine);
    }

    [Fact]
    public void A_category_narrows_the_rows_to_its_own_types()
    {
        var query = Searching.AskIn(export, Categories.Dungeons, out var error);

        Assert.Null(error);

        var results = Searching.Run(export, query);

        Assert.All(
            results.Rows,
            row => Assert.Contains(
                EntityTypes.TypeOf(row), new[] { EntityType.Dungeon, EntityType.Creature }));

        Assert.Null(results.Chosen);
    }

    [Fact]
    public void A_type_of_its_own_still_filters()
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
    public async Task A_category_naming_an_unlisted_type_answers_a_problem_naming_the_legal_slugs()
    {
        var page = await client.GetStringAsync(
            "/spells?type=talent-slot", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("is not a type. The types are", page, StringComparison.Ordinal);
        Assert.Contains("That filter could not be read.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("talent-slot,", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_category_naming_another_categorys_type_says_which_types_it_holds()
    {
        var page = await client.GetStringAsync(
            "/items?type=ability", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("is not a Items type", page, StringComparison.Ordinal);
        Assert.Contains("That filter could not be read.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_of_an_unlisted_type_is_not_found()
    {
        foreach (var address in new[] { "/talent-slot/1", "/legendary-stat/1", "/gem-type/1" })
        {
            var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task No_route_answers_for_an_unlisted_type()
    {
        string[] addresses =
        [
            "/api/talent-slot/1", "/api/talent-slot/1/tooltip",
            "/api/mount-power/445", "/api/mount-power/445/tooltip",
            "/api/legendary-stat/1", "/api/legendary-stat/1/tooltip",
            "/api/gem-type/1", "/api/gem-type/1/tooltip",
        ];

        foreach (var address in addresses)
        {
            var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Browse_is_gone_and_the_three_categories_answer_in_its_place()
    {
        var browse = await client.GetAsync("/browse", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, browse.StatusCode);

        foreach (var category in Categories.All)
        {
            var page = await client.GetAsync("/" + category.Slug, Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }
    }

    [Fact]
    public async Task A_category_marks_the_tab_of_the_type_it_is_filtered_to()
    {
        var page = await client.GetStringAsync("/items?type=gem", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("aria-current=\"page\"", page, StringComparison.Ordinal);
        Assert.Contains("Gems", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_catalog_publishes_the_categories_and_the_tabs_in_order()
    {
        var body = await client.GetStringAsync("/api/catalog", Xunit.TestContext.Current.CancellationToken);

        using var reading = System.Text.Json.JsonDocument.Parse(body);

        Assert.Equal(
            ["spells", "items", "dungeons"],
            reading.RootElement.GetProperty("categories").EnumerateArray()
                .Select(category => category.GetProperty("slug").GetString()));

        var types = reading.RootElement.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("ability", types[0].GetProperty("slug").GetString());
        Assert.Equal("spells", types[0].GetProperty("category").GetString());

        var positions = types.Select(type => type.GetProperty("position").GetInt32()).ToArray();

        Assert.Equal(positions.OrderBy(position => position), positions);
        Assert.DoesNotContain(types, type => type.GetProperty("slug").GetString() == "talent-slot");
        Assert.DoesNotContain(types, type => type.GetProperty("slug").GetString() == "gem-type");

        Assert.NotEmpty(reading.RootElement.GetProperty("sortings").EnumerateArray());
    }
}
