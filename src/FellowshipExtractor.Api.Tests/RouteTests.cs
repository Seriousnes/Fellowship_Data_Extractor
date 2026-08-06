using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The app's own route table, hosted for real through <c>WebApplicationFactory&lt;Program&gt;</c>
/// rather than by calling the handler classes directly, so this is the same slug parsing and the
/// same status codes a caller actually reaches.
///
/// <see cref="IconSeeder"/> is removed from the host's hosted services: it uploads to the icon
/// container over the network on startup, and this suite has no blob store to upload to. Removing
/// the seeder changes nothing about routing or about <see cref="EntityStore"/>, which reads the
/// export straight off disk.
/// </summary>
public sealed class RouteTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public RouteTests(Factory factory) => client = factory.CreateClient();

    /// <summary>
    /// /api/tooltip/{slug}/{id} answers 200 for one id of each of the 21 types. Every talent-slot id
    /// splits by hero with no single-reading one to fall back on, so the sample carries a hero query
    /// wherever its id is one of the 207 that split (188 item ids, 19 talent-slot ids) - the wearer
    /// <see cref="EntityStore.Wearer"/> already answers for it, the same pairing a caller resolves an
    /// ambiguous id with.
    /// </summary>
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

    /// <summary>
    /// /api/entity/{slug}/{id} answers a record whose own "type" field is the slug asked for, for
    /// every one of the 21 types. Unlike the tooltip route this one is never ambiguous - it answers
    /// every reading of the id at once - so no hero query is needed here.
    /// </summary>
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

    /// <summary>
    /// One id of the given type, and the hero query it needs to resolve without ambiguity - null
    /// where the id carries only one reading and needs none.
    /// </summary>
    private static (int Id, string? Hero) Sample(EntityType type)
    {
        var document = Factory.Store.Entities.First(candidate => EntityTypes.TypeOf(candidate) == type);
        var readings = Factory.Store.All(type, document.Id);

        var hero = readings.Count > 1 ? Factory.Store.Wearer(document)?.Name : null;

        return (document.Id, hero is null ? null : Uri.EscapeDataString(hero));
    }

    /// <summary>A slug the corpus does not spell answers 404, not a 500 or a silently empty page.</summary>
    [Fact]
    public async Task An_unknown_type_answers_404_rather_than_throwing()
    {
        var response = await client.GetAsync("/api/tooltip/no-such-type/1", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// The same store the assertions above read ids from, built once against the same export
        /// the hosted app reads, so a sampled id is guaranteed to be one the running app can answer.
        /// </summary>
        public static readonly EntityStore Store = BuildStore();

        private static EntityStore BuildStore()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = ExportFolder() })
                .Build();

            return new EntityStore(
                new ExportPaths(configuration), Microsoft.Extensions.Logging.Abstractions.NullLogger<EntityStore>.Instance);
        }

        /// <summary>
        /// The repo's own export, found the same way <see cref="Export"/> finds it: by walking up
        /// from the test binary.
        /// </summary>
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

            // A syntactically valid address nothing serves, the same accommodation Export.cs makes
            // for CodexView's own blob client: the icon URLs it composes are never fetched by
            // anything this suite asks of the app. The well-known Azurite credentials are what make
            // this parse as a connection string at all; the port is unreachable, so nothing on it is
            // ever actually called.
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
