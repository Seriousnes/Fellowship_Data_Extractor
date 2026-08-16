using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fellowship.Codex.Tests;

public sealed class RouteTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    private readonly IChromeAssetService chrome;

    private readonly Factory factory;

    public RouteTests(Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        chrome = factory.Services.GetRequiredService<IChromeAssetService>();
    }

    [Fact]
    public void Every_registered_service_is_resolved_through_an_interface()
    {
        Assert.NotNull(factory.Services.GetRequiredService<IExportSource>());
        Assert.NotNull(factory.Services.GetRequiredService<IEntityStoreService>());
    }

    [Fact]
    public void Every_rendering_service_is_resolved_through_an_interface()
    {
        var provider = factory.Services;

        Assert.NotNull(provider.GetRequiredService<IDesignTokenService>());
        Assert.NotNull(provider.GetRequiredService<IRichTextService>());
        Assert.NotNull(provider.GetRequiredService<IChromeAssetService>());
        Assert.NotNull(provider.GetRequiredService<IPlaceholderService>());
        Assert.NotNull(provider.GetRequiredService<ITooltipViewService>());
        Assert.NotNull(provider.GetRequiredService<IFragmentService>());
        Assert.NotNull(provider.GetRequiredService<ITooltipService>());
    }

    [Fact]
    public void Every_browsing_service_is_resolved_through_an_interface()
    {
        var provider = factory.Services;

        Assert.NotNull(provider.GetRequiredService<ICodexService>());
        Assert.NotNull(provider.GetRequiredService<ISearchIndexService>());
        Assert.NotNull(provider.GetRequiredService<ICodexSearchService>());
        Assert.NotNull(provider.GetRequiredService<ICodexViewService>());
        Assert.NotNull(provider.GetRequiredService<ISuggestionService>());
    }

    [Fact]
    public async Task Both_routes_answer_for_one_id_of_every_type()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.Listed))
        {
            var (id, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);
            var wearer = hero is null ? string.Empty : $"?hero={hero}";

            foreach (var address in new[] { $"/api/{slug}/{id}{wearer}", $"/api/{slug}/{id}/tooltip{wearer}" })
            {
                var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

                Assert.True(
                    response.StatusCode == HttpStatusCode.OK,
                    $"{address} answered {(int)response.StatusCode}, not 200. Body: {body}");
            }
        }
    }

    [Fact]
    public async Task A_record_answers_the_type_the_slug_asked_for()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.Listed))
        {
            var (id, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);
            var wearer = hero is null ? string.Empty : $"?hero={hero}";

            var body = await client.GetStringAsync(
                $"/api/{slug}/{id}{wearer}", Xunit.TestContext.Current.CancellationToken);

            using var reading = JsonDocument.Parse(body);

            Assert.Equal(slug, reading.RootElement.GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task An_address_naming_no_type_is_a_routing_404()
    {
        var response = await client.GetAsync("/api/no-such-type/1", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_item_answers_its_record_and_its_tooltip_at_its_own_address()
    {
        var (id, hero) = Sample(EntityType.Item);
        var wearer = hero is null ? string.Empty : $"?hero={hero}";

        var record = await client.GetAsync($"/api/item/{id}{wearer}", Xunit.TestContext.Current.CancellationToken);
        var tooltip = await client.GetAsync(
            $"/api/item/{id}/tooltip{wearer}", Xunit.TestContext.Current.CancellationToken);

        record.EnsureSuccessStatusCode();
        tooltip.EnsureSuccessStatusCode();

        var body = await record.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("\"type\":\"item\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_optional_property_the_binder_fills_reaches_the_card()
    {
        var id = Factory.Store.Entities.OfType<ItemDocument>().First(row => row.Stats is { Count: > 0 }).Id;

        var drawn = await client.GetStringAsync(
            $"/api/item/{id}/tooltip?armor=303", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("303", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_random_stats_the_caller_names_reach_the_card_as_named_lines()
    {
        const int EldrinSignet = 5220;

        var response = await client.GetAsync(
            $"/api/item/{EldrinSignet}/tooltip?hero=Rime&itemLevel=360&randomStats=Haste,Haste",
            Xunit.TestContext.Current.CancellationToken);

        var drawn = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, drawn);

        var at = drawn.IndexOf("<ul class=\"stats\">", StringComparison.Ordinal);
        var stats = drawn[at..drawn.IndexOf("</ul>", at, StringComparison.Ordinal)];

        Assert.Contains("16</span><span>Haste</span>", stats, StringComparison.Ordinal);
        Assert.Contains("class=\"v random\"", stats, StringComparison.Ordinal);
        Assert.DoesNotContain("[Random Stat]", stats, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_random_stat_no_slot_rolls_is_refused()
    {
        var (id, _) = Sample(EntityType.Item);

        var response = await client.GetAsync(
            $"/api/item/{id}/tooltip?randomStats=Armor", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("is not a stat a random slot rolls", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hero_the_roster_does_not_carry_is_refused_by_the_record()
    {
        var (id, _) = Sample(EntityType.Item);

        var response = await client.GetAsync(
            $"/api/item/{id}?hero=nonsense", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("is not a hero", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_item_level_past_the_ceiling_is_refused_by_the_field_that_states_it()
    {
        var (id, hero) = Sample(EntityType.Item);
        var wearer = hero is null ? "?" : $"?hero={hero}&";

        var response = await client.GetAsync(
            $"/api/item/{id}/tooltip{wearer}itemLevel=10001", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ItemLevel", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_state_reaches_the_card_that_draws_one()
    {
        var trait = Factory.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null);

        var drawn = await client.GetStringAsync(
            $"/api/trait/{trait.Id}/tooltip?state=unavailable", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(chrome.TraitNode("unavailable"), drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_item_reads_the_hero_its_id_needs()
    {
        var (id, hero) = Shared();
        var wearer = Uri.EscapeDataString(hero);

        var bare = await client.GetAsync($"/api/item/{id}", Xunit.TestContext.Current.CancellationToken);
        var named = await client.GetAsync(
            $"/api/item/{id}?hero={wearer}", Xunit.TestContext.Current.CancellationToken);

        named.EnsureSuccessStatusCode();

        Assert.True(Factory.Store.All(EntityType.Item, id).Count > 1);
        Assert.Equal(HttpStatusCode.BadRequest, bare.StatusCode);
    }

    [Fact]
    public async Task A_rarity_reaches_a_gem_and_nothing_reaches_it_that_belongs_to_an_item()
    {
        var (id, _) = Sample(EntityType.Gem);

        var rung = await client.GetAsync(
            $"/api/gem/{id}/tooltip?rarity=1", Xunit.TestContext.Current.CancellationToken);
        var level = await client.GetAsync(
            $"/api/gem/{id}/tooltip?itemLevel=100", Xunit.TestContext.Current.CancellationToken);

        rung.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, level.StatusCode);
    }

    [Fact]
    public async Task A_type_naming_no_entity_type_is_refused_by_search()
    {
        var response = await client.GetAsync(
            "/api/search?q=boot&type=no-such-type", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(PublicTypes.NoSuchType("no-such-type"), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_three_cross_type_routes_keep_their_paths()
    {
        foreach (var address in new[] { "/api/search?q=boot", "/api/entities?size=1", "/api/catalog" })
        {
            var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

            Assert.True(response.IsSuccessStatusCode, $"{address} answered {(int)response.StatusCode}.");
        }
    }

    private static (int Id, string? Hero) Sample(EntityType type)
    {
        var document = Factory.Store.Entities.First(candidate => EntityTypes.TypeOf(candidate) == type);
        var readings = Factory.Store.All(type, document.Id);

        var hero = readings.Count > 1 ? Factory.Store.Wearer(document)?.Name : null;

        return (document.Id, hero is null ? null : Uri.EscapeDataString(hero));
    }

    [Fact]
    public async Task An_entity_page_serves_its_name_colour_as_a_custom_property()
    {
        var page = await client.GetStringAsync("/item/5250", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("--cx-name:#", page, StringComparison.Ordinal);
        Assert.Contains("var(--cx-rung, var(--cx-name))", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_offers_more_like_this_and_the_browse_it_points_at_answers()
    {
        var page = await client.GetStringAsync("/item/5191", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("More like this", page, StringComparison.Ordinal);

        var slot = ((ItemDocument)Factory.Store.All(EntityType.Item, 5191)[0]).Slot;

        var browse = await client.GetStringAsync(
            $"/browse?type=item&slot={Uri.EscapeDataString(slot!)}", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("entities", browse, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_draws_the_node_state_its_url_asks_for()
    {
        var trait = Factory.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null);
        var where = $"/{EntityTypes.Slug(EntityType.Trait)}/{trait.Id}";

        var asked = await client.GetStringAsync(
            $"{where}?state=unavailable", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(chrome.TraitNode("unavailable"), asked, StringComparison.Ordinal);

        var silent = await client.GetStringAsync(where, Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(chrome.TraitNode(null), silent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_prerenders_the_card_the_address_asks_for()
    {
        var (id, hero) = Shared();
        var slug = EntityTypes.Slug(EntityType.Item);

        var page = await client.GetStringAsync(
            $"/{slug}/{id}?hero={Uri.EscapeDataString(hero)}&itemLevel=360",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("cx-craft", page, StringComparison.Ordinal);
        Assert.Contains(TooltipView.Root, page, StringComparison.Ordinal);
        Assert.Contains("360", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-stage", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-restage", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_states_its_version_history_once_and_heads_it_with_the_build()
    {
        var page = await client.GetStringAsync("/item/5250?hero=Elarion", Xunit.TestContext.Current.CancellationToken);

        var history = factory.Services.GetRequiredService<IExportHistoryService>();

        Assert.Equal(1, Occurrences(page, "class=\"cx-changes\""));
        Assert.Contains($">{history.Builds[0]}<", page, StringComparison.Ordinal);
        Assert.Contains("cx-change-current", page, StringComparison.Ordinal);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;

        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public async Task A_page_whose_knobs_pick_a_difficulty_renders_inside_the_island()
    {
        var creature = Factory.Store.Entities.OfType<CreatureDocument>().First();
        var slug = EntityTypes.Slug(EntityType.Creature);

        var page = await client.GetStringAsync(
            $"/{slug}/{creature.Id}", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("cx-craft", page, StringComparison.Ordinal);
        Assert.Contains("data-difficulty", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_prerenders_every_parameter_the_address_carries()
    {
        var (id, hero) = Shared();
        var slug = EntityTypes.Slug(EntityType.Item);

        var page = await client.GetStringAsync(
            $"/{slug}/{id}?hero={Uri.EscapeDataString(hero)}&itemLevel=360&randomStats=Haste%2CHaste",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("cx-craft", page, StringComparison.Ordinal);
        Assert.Contains("value=\"360\"", page, StringComparison.Ordinal);
        Assert.Contains("Haste,Haste", page, StringComparison.Ordinal);
    }

    private static (int Id, string Hero) Shared()
    {
        foreach (var document in Factory.Store.Entities)
        {
            if (EntityTypes.TypeOf(document) != EntityType.Item) continue;
            if (Factory.Store.All(EntityType.Item, document.Id).Count < 2) continue;
            if (Factory.Store.Wearer(document) is not { } wearer) continue;

            return (document.Id, wearer.Name);
        }

        throw new InvalidOperationException(
            "No item id the export gives more than one hero's reading of, so no entity page carries a " +
            "hero and a state at once.");
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public static readonly IEntityStoreService Store = BuildStore();

        private static IEntityStoreService BuildStore() =>
            new EntityStoreService(
                new FolderExportSource(Export.Found())
                    .LoadAsync(CancellationToken.None).GetAwaiter().GetResult().Snapshot,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<EntityStoreService>.Instance);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(
                "ConnectionStrings:icons",
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
                "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
                "BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;ContainerName=icons");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExportSource>();
                services.AddSingleton<IExportSource>(new FolderExportSource(Export.Found()));
            });
        }
    }
}
