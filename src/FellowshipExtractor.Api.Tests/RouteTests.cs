using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FellowshipExtractor.Api.Tests;

public sealed class RouteTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public RouteTests(Factory factory) => client = factory.CreateClient();

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
    public async Task An_unknown_type_answers_404_rather_than_throwing()
    {
        var response = await client.GetAsync("/api/tooltip/no-such-type/1", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public static readonly EntityStore Store = BuildStore();

        private static EntityStore BuildStore()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = ExportFolder() })
                .Build();

            return new EntityStore(
                new ExportPaths(configuration), Microsoft.Extensions.Logging.Abstractions.NullLogger<EntityStore>.Instance);
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
                var seeder = services.SingleOrDefault(descriptor => descriptor.ImplementationType == typeof(IconSeeder));
                if (seeder is not null) services.Remove(seeder);
            });
        }
    }
}
