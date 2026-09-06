using System.Net;

using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public sealed class CodexClientTests : IClassFixture<RouteTests.Factory>
{
    private readonly CodexClient codex;

    public CodexClientTests(RouteTests.Factory factory) => codex = new CodexClient(factory.CreateClient());

    private static CancellationToken Stopping => Xunit.TestContext.Current.CancellationToken;

    private sealed class Recorder : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? Address { get; private set; }

        public string? Accept { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellation)
        {
            Method = request.Method;
            Address = request.RequestUri;
            Accept = request.Headers.Accept.ToString();
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellation);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<div></div>", System.Text.Encoding.UTF8, "text/html"),
            };
        }
    }

    private static async Task<Recorder> Sent(TooltipRequest? request)
    {
        var recorder = new Recorder();

        using var http = new HttpClient(recorder);

        await new CodexClient(http).TooltipAsync(EntityType.Item, 5218, request, Stopping);

        return recorder;
    }

    private static int Sample(EntityType type) =>
        RouteTests.Factory.Store.Entities
            .Where(document => EntityTypes.TypeOf(document) == type)
            .Select(document => document.Id)
            .First(id => RouteTests.Factory.Store.All(type, id).Count == 1);

    [Fact]
    public async Task Every_listed_type_reads_back_as_the_record_class_named_for_it()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            var record = await codex.GetAsync(type, Sample(type), cancellation: Stopping);

            Assert.IsType(EntityRecords.Record(type), record);
            Assert.Equal(type, record.Type);
            Assert.False(string.IsNullOrWhiteSpace(record.Name));
        }
    }

    [Fact]
    public async Task The_record_class_the_caller_names_is_the_one_it_is_handed_back()
    {
        var ability = await codex.GetAsync<AbilityRecord>(Sample(EntityType.Ability), cancellation: Stopping);
        var item = await codex.GetAsync<ItemRecord>(Sample(EntityType.Item), cancellation: Stopping);

        Assert.Equal(EntityType.Ability, ability.Type);
        Assert.Equal(EntityType.Item, item.Type);
    }

    [Fact]
    public async Task A_reference_a_record_carries_is_read_back_through_the_client_it_came_from()
    {
        var creature = RouteTests.Factory.Store.Entities
            .OfType<CreatureDocument>()
            .First(row =>
                row.FoundIn is { Count: > 0 } &&
                RouteTests.Factory.Store.All(EntityType.Creature, row.Id).Count == 1);

        var record = await codex.GetAsync<CreatureRecord>(creature.Id, cancellation: Stopping);

        var dungeon = await codex.GetAsync(record.FoundIn![0], cancellation: Stopping);

        Assert.Equal(EntityType.Dungeon, dungeon.Type);
        Assert.Equal(record.FoundIn[0].Id, dungeon.Id);
    }

    [Fact]
    public async Task A_hero_the_roster_does_not_carry_is_raised_as_the_fault_the_codex_states()
    {
        var raised = await Assert.ThrowsAsync<CodexException>(() =>
            codex.GetAsync<ItemRecord>(Sample(EntityType.Item), "Nobody", Stopping));

        Assert.Equal(HttpStatusCode.BadRequest, raised.Status);
        Assert.Equal("No such hero.", raised.Fault.Title);
        Assert.Contains("Nobody", raised.Fault.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_id_the_build_does_not_carry_is_raised_as_a_not_found_fault()
    {
        var raised = await Assert.ThrowsAsync<CodexException>(() =>
            codex.GetAsync<AbilityRecord>(int.MaxValue, cancellation: Stopping));

        Assert.Equal(HttpStatusCode.NotFound, raised.Status);
        Assert.Equal("No such id.", raised.Fault.Title);
    }

    [Fact]
    public async Task A_search_answers_the_groups_and_results_its_query_reaches()
    {
        var name = RouteTests.Factory.Store.Entities.OfType<AbilityDocument>().First(row => row.Name is { Length: > 3 }).Name!;

        var found = await codex.SearchAsync(name[..3], cancellation: Stopping);

        Assert.NotEmpty(found.Results);
        Assert.NotEmpty(found.Groups);
        Assert.True(found.Total >= found.Results.Count);
    }

    [Fact]
    public async Task A_search_narrowed_to_one_type_answers_that_type_alone()
    {
        var found = await codex.SearchAsync("a", EntityType.Item, 5, Stopping);

        Assert.All(found.Results, hit => Assert.Equal(EntityTypes.Slug(EntityType.Item), hit.Type));
        Assert.True(found.Results.Count <= 5);
    }

    [Fact]
    public async Task A_filter_pages_the_entities_it_leaves()
    {
        var page = await codex.EntitiesAsync(
            new EntityFilter { Type = [EntityType.Item], Size = 10, Page = 1 }, Stopping);

        Assert.Equal(1, page.Page);
        Assert.True(page.Rows.Count <= 10);
        Assert.True(page.Pages >= 1);
        Assert.All(page.Rows, row => Assert.Equal(EntityTypes.Slug(EntityType.Item), row.Type));
    }

    [Fact]
    public async Task The_catalog_names_every_type_the_browser_lists()
    {
        var catalog = await codex.CatalogAsync(Stopping);

        Assert.Equal(
            PublicTypes.Listed.Select(type => type.Slug).Order(),
            catalog.Types.Select(type => type.Slug).Order());

        Assert.NotEmpty(catalog.Categories);
        Assert.NotEmpty(catalog.Menu);
        Assert.NotEmpty(catalog.Facets);
        Assert.NotEmpty(catalog.Heroes);
        Assert.NotEmpty(catalog.Rarities);
    }

    [Fact]
    public async Task A_tooltip_comes_back_as_the_fragment_the_codex_draws()
    {
        var html = await codex.TooltipAsync(
            EntityType.Ability, Sample(EntityType.Ability), cancellation: Stopping);

        Assert.Contains("<", html, StringComparison.Ordinal);
        Assert.Contains("style", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tooltip_parameter_the_caller_names_reaches_the_card()
    {
        var item = RouteTests.Factory.Store.Entities
            .OfType<ItemDocument>()
            .First(row =>
                row.Stats is { Count: > 0 } &&
                RouteTests.Factory.Store.All(EntityType.Item, row.Id).Count == 1);

        var html = await codex.TooltipAsync(EntityType.Item, item.Id, new TooltipRequest { Armor = 303m }, Stopping);

        Assert.Contains("303", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tooltip_is_asked_for_with_a_query_that_accepts_html()
    {
        var sent = await Sent(null);

        Assert.Equal(HttpMethod.Query, sent.Method);
        Assert.Equal("api/item/5218/tooltip", sent.Address!.AbsolutePath.TrimStart('/'));
        Assert.Equal("text/html", sent.Accept);
        Assert.Null(sent.Body);
    }

    [Fact]
    public async Task The_request_the_caller_states_is_the_body_the_query_carries()
    {
        var sent = await Sent(new TooltipRequest { Hero = "Rime", ItemLevel = 360, RandomStats = ["Haste", "Haste"] });

        Assert.Equal(
            """{"hero":"Rime","itemLevel":360,"randomStats":["Haste","Haste"]}""",
            sent.Body);
    }

    [Fact]
    public async Task A_stat_rolled_twice_carries_both_magnitudes_as_separate_entries()
    {
        var sent = await Sent(new TooltipRequest().Rated("Haste", 100m, 250m).Percent("Haste", 10m));

        Assert.Equal("""{"stats":{"Haste":{"rating":[100,250],"percentage":[10]}}}""", sent.Body);
    }

    [Fact]
    public void The_client_addresses_the_deployed_codex_when_the_caller_names_no_other()
    {
        using var bare = new HttpClient();

        _ = new CodexClient(bare);

        Assert.Equal(CodexClient.Origin, bare.BaseAddress);
    }
}
