using System.Text.Json;

using Fellowship.Codex.Mcp;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fellowship.Codex.Tests;

public sealed class McpHost
{
    public IHost Host { get; }

    public CodexTools Tools { get; }

    public IReadOnlyList<McpServerTool> Published { get; }

    public McpHost()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.AddCodexMcp(Export.Found());

        Host = builder.Build();

        Host.Services.LoadExportAsync(CancellationToken.None).GetAwaiter().GetResult();

        Tools = ActivatorUtilities.CreateInstance<CodexTools>(Host.Services);
        Published = [.. Host.Services.GetServices<McpServerTool>()];
    }

    public static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    public static JsonElement Read(CallToolResult result) =>
        JsonDocument.Parse(Text(result)).RootElement.Clone();
}

public sealed class McpTests(McpHost mcp, RouteTests.Factory factory)
    : IClassFixture<McpHost>, IClassFixture<RouteTests.Factory>
{
    private static readonly string[] Names = ["list_types", "find_entity", "get_entity"];

    [Fact]
    public void The_server_publishes_exactly_the_three_tools()
    {
        Assert.Equal(Names.Order(), mcp.Published.Select(tool => tool.ProtocolTool.Name).Order());
    }

    [Fact]
    public void Every_tool_is_marked_read_only()
    {
        Assert.All(mcp.Published, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
    }

    [Fact]
    public void Every_tool_and_parameter_description_ends_in_a_full_stop()
    {
        foreach (var tool in mcp.Published)
        {
            var described = tool.ProtocolTool.Description;

            Assert.False(string.IsNullOrWhiteSpace(described), $"{tool.ProtocolTool.Name} has no description.");
            Assert.EndsWith(".", described!.Trim(), StringComparison.Ordinal);

            if (!tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties)) continue;

            foreach (var parameter in properties.EnumerateObject())
            {
                var says = parameter.Value.TryGetProperty("description", out var text) ? text.GetString() : null;

                Assert.False(
                    string.IsNullOrWhiteSpace(says),
                    $"{tool.ProtocolTool.Name}'s {parameter.Name} has no description.");
                Assert.EndsWith(".", says!.Trim(), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void List_types_states_every_listed_slug_the_roster_and_the_rarities()
    {
        var listed = mcp.Tools.ListTypes();
        var store = RouteTests.Factory.Store;

        Assert.Equal(
            Enum.GetValues<EntityType>().Where(PublicTypes.Listed).Select(EntityTypes.Slug).Order(),
            listed.Types.Select(type => type.Slug).Order());

        Assert.Equal(store.Settings.Heroes.Select(hero => hero.Name).Order(), listed.Heroes.Select(h => h.Name).Order());
        Assert.Equal(store.Settings.Rarities.Select(rung => rung.Tier).Order(), listed.Rarities.Select(r => r.Tier).Order());
        Assert.True(listed.Entities > 0);
    }

    [Fact]
    public void Get_entity_answers_for_one_id_of_every_listed_type()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.Listed))
        {
            var (id, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);

            var answered = mcp.Tools.GetEntity(slug, id, hero);

            Assert.False(
                answered.IsError is true,
                $"{slug} {id} answered an error: {McpHost.Text(answered)}");

            var record = McpHost.Read(answered);

            Assert.Equal(slug, record.GetProperty("type").GetString());
            Assert.Equal(id, record.GetProperty("id").GetInt32());
        }
    }

    [Fact]
    public void Get_entity_names_the_valid_slugs_for_a_word_that_is_not_one()
    {
        var answered = mcp.Tools.GetEntity("wombat", 1);

        Assert.True(answered.IsError);
        Assert.Contains("is not a type", McpHost.Text(answered), StringComparison.Ordinal);
        Assert.Contains(EntityTypes.Slug(EntityType.Ability), McpHost.Text(answered), StringComparison.Ordinal);
    }

    [Fact]
    public void Get_entity_refuses_a_slug_the_codex_does_not_list()
    {
        var answered = mcp.Tools.GetEntity(EntityTypes.Slug(EntityType.TalentSlot), 1);

        Assert.True(answered.IsError);
    }

    [Fact]
    public void Get_entity_faults_for_an_id_the_build_does_not_carry()
    {
        var answered = mcp.Tools.GetEntity(EntityTypes.Slug(EntityType.Ability), int.MaxValue);

        Assert.True(answered.IsError);
        Assert.Contains("no ability", McpHost.Text(answered), StringComparison.Ordinal);
    }

    [Fact]
    public void Get_entity_names_the_roster_for_a_hero_that_is_not_one()
    {
        var (id, _) = Sample(EntityType.Ability);

        var answered = mcp.Tools.GetEntity(EntityTypes.Slug(EntityType.Ability), id, "Wombat");

        Assert.True(answered.IsError);
        Assert.Contains("is not a hero", McpHost.Text(answered), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_entity_speaks_the_sentence_the_route_puts_in_its_problem_detail()
    {
        var id = PerHero();
        var slug = EntityTypes.Slug(EntityType.Item);

        var answered = mcp.Tools.GetEntity(slug, id);

        Assert.True(answered.IsError);

        var response = await factory.CreateClient().GetAsync(
            $"/api/{slug}/{id}", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);
        var detail = JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString();

        Assert.Equal(detail, McpHost.Text(answered));
    }

    [Fact]
    public void Get_entity_reads_an_id_that_needs_a_hero_once_the_hero_is_given()
    {
        var id = PerHero();
        var wearer = RouteTests.Factory.Store.Wearer(RouteTests.Factory.Store.All(EntityType.Item, id)[0])!.Name;

        var answered = mcp.Tools.GetEntity(EntityTypes.Slug(EntityType.Item), id, wearer);

        Assert.False(answered.IsError is true, McpHost.Text(answered));
        Assert.Equal(id, McpHost.Read(answered).GetProperty("id").GetInt32());
    }

    [Fact]
    public void A_record_addresses_its_references_at_the_api_with_no_host()
    {
        var slug = EntityTypes.Slug(EntityType.Trait);
        var trait = RouteTests.Factory.Store.Entities.OfType<TraitDocument>().First(row => row.Power is not null);

        var record = McpHost.Read(mcp.Tools.GetEntity(slug, trait.Id));

        Assert.StartsWith("/api/", record.GetProperty("power").GetProperty("resource").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_names_an_image_the_export_carries_rather_than_addressing_one()
    {
        var item = RouteTests.Factory.Store.Entities
            .OfType<ItemDocument>()
            .First(row => row.Icon is not null && RouteTests.Factory.Store.All(EntityType.Item, row.Id).Count == 1);

        var named = McpHost.Read(mcp.Tools.GetEntity(EntityTypes.Slug(EntityType.Item), item.Id))
            .GetProperty("icon")
            .GetString();

        Assert.Equal(item.Icon, named);
        Assert.True(
            File.Exists(Path.Combine(Export.Found(), ChromeAssetService.Prefix, named!)),
            $"{named} is not a file under the export's {ChromeAssetService.Prefix} folder.");
    }

    [Fact]
    public void Find_entity_matches_a_name_by_prefix_and_not_by_substring()
    {
        var named = RouteTests.Factory.Store.Entities.First(row => row.Name is { Length: > 6 });

        Assert.Contains(mcp.Tools.FindEntity(named.Name!).Results, hit => hit.Name == named.Name);
        Assert.DoesNotContain(mcp.Tools.FindEntity(named.Name![3..]).Results, hit => hit.Name == named.Name);
    }

    [Fact]
    public void Find_entity_honours_the_type_it_is_given()
    {
        var slug = EntityTypes.Slug(EntityType.Ability);

        var found = mcp.Tools.FindEntity("a", slug, 50);

        Assert.NotEmpty(found.Results);
        Assert.All(found.Results, hit => Assert.Equal(slug, hit.Type));
    }

    [Fact]
    public void Find_entity_clamps_the_limit_it_is_given()
    {
        Assert.True(mcp.Tools.FindEntity("a", limit: 5000).Results.Count <= SuggestionService.MaximumLimit);
        Assert.Single(mcp.Tools.FindEntity("a", limit: -3).Results);
    }

    [Fact]
    public void Find_entity_carries_only_what_names_a_row_and_feeds_get_entity()
    {
        var carried = typeof(FoundEntity).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(nameof(SearchResult.Href), carried);
        Assert.DoesNotContain(nameof(SearchResult.Tooltip), carried);
        Assert.Contains(nameof(FoundEntity.Type), carried);
        Assert.Contains(nameof(FoundEntity.Id), carried);
    }

    private static int PerHero() =>
        RouteTests.Factory.Store.Entities
            .OfType<ItemDocument>()
            .First(row => RouteTests.Factory.Store.All(EntityType.Item, row.Id).Count > 1)
            .Id;

    private static (int Id, string? Hero) Sample(EntityType type)
    {
        var document = RouteTests.Factory.Store.Entities.First(candidate => EntityTypes.TypeOf(candidate) == type);
        var readings = RouteTests.Factory.Store.All(type, document.Id);

        return (document.Id, readings.Count > 1 ? RouteTests.Factory.Store.Wearer(document)?.Name : null);
    }
}
