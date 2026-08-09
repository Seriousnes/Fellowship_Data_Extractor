using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FellowshipExtractor.Api.Tests;

public sealed class RouteTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    private readonly ChromeAssets chrome;

    private readonly Factory factory;

    public RouteTests(Factory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        chrome = factory.Services.GetRequiredService<ChromeAssets>();
    }

    [Fact]
    public void Every_registered_service_is_resolved_through_an_interface()
    {
        Assert.NotNull(factory.Services.GetRequiredService<IExportPathService>());
        Assert.NotNull(factory.Services.GetRequiredService<IEntityStoreService>());
    }

    [Fact]
    public async Task Tooltip_route_answers_200_for_one_id_of_every_type()
    {
        foreach (var type in Enum.GetValues<EntityType>())
        {
            var (id, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);
            var address = hero is null ? $"/api/tooltip/{slug}/{id}" : $"/api/tooltip/{slug}/{id}?hero={hero}";

            var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

            var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{address} answered {(int)response.StatusCode}, not 200. Body: {body}");
        }
    }

    [Fact]
    public async Task Entity_route_answers_a_record_typed_as_the_slug_asked_for()
    {
        foreach (var type in Enum.GetValues<EntityType>())
        {
            var (id, _) = Sample(type);
            var slug = EntityTypes.Slug(type);

            var response = await client.GetAsync(
                $"/api/entity/{slug}/{id}", Xunit.TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);
            using var readings = JsonDocument.Parse(body);

            Assert.True(readings.RootElement.GetArrayLength() > 0, $"{slug}/{id} answered no readings.");

            foreach (var reading in readings.RootElement.EnumerateArray())
            {
                Assert.Equal(slug, reading.GetProperty("type").GetString());
            }
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
        var page = await client.GetStringAsync("/entity/item/5250", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("--cx-name:#", page, StringComparison.Ordinal);
        Assert.Contains("var(--cx-rung, var(--cx-name))", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_offers_more_like_this_and_the_browse_it_points_at_answers()
    {
        var page = await client.GetStringAsync("/entity/item/5191", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("More like this", page, StringComparison.Ordinal);

        var slot = ((ItemDocument)Factory.Store.All(EntityType.Item, 5191)[0]).Slot;

        var browse = await client.GetStringAsync(
            $"/browse?type=item&slot={Uri.EscapeDataString(slot!)}", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("entities", browse, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_type_answers_404_rather_than_throwing()
    {
        var response = await client.GetAsync("/api/tooltip/no-such-type/1", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_entity_page_draws_the_node_state_its_url_asks_for()
    {
        var trait = Factory.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null);
        var where = $"/entity/{EntityTypes.Slug(EntityType.Trait)}/{trait.Id}";

        var asked = await client.GetStringAsync(
            $"{where}?state=unavailable", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(chrome.TraitNode("unavailable"), asked, StringComparison.Ordinal);

        var silent = await client.GetStringAsync(where, Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(chrome.TraitNode(null), silent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_carries_the_hero_and_the_state_into_its_refetch_address()
    {
        var (id, hero) = Shared();
        var slug = EntityTypes.Slug(EntityType.Item);
        var wearer = Uri.EscapeDataString(hero);

        var page = await client.GetStringAsync(
            $"/entity/{slug}/{id}?hero={wearer}&state=unavailable", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(
            $"data-stage=\"/api/tooltip/{slug}/{id}?hero={wearer}&amp;state=unavailable\"",
            page,
            StringComparison.Ordinal);
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

        private static IEntityStoreService BuildStore()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = ExportFolder() })
                .Build();

            return new EntityStoreService(
                new ExportPathService(configuration),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<EntityStoreService>.Instance);
        }

        private static string ExportFolder()
        {
            for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
            {
                var export = Path.Combine(here.FullName, "output", "export");

                if (File.Exists(Path.Combine(export, "entities.jsonl"))) return export;
            }

            throw new InvalidOperationException("No output/export above the test binary.");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("FELLOWSHIP_EXPORT", ExportFolder());

            builder.UseSetting(
                "ConnectionStrings:icons",
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
                "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
                "BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;ContainerName=icons");

            builder.ConfigureServices(services =>
            {
                var seeder = services.SingleOrDefault(
                    descriptor => descriptor.ImplementationType == typeof(IconSeederService));
                if (seeder is not null) services.Remove(seeder);
            });
        }
    }
}
