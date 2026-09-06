using System.Globalization;
using System.Net;
using System.Text;

using Fellowship.SDK.Client;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fellowship.Codex.Tests;

public sealed class QueryTransportTests : IClassFixture<RouteTests.Factory>
{
    private const string HasteStat = "Haste";

    private const string Trimmed = "0.##########";

    private readonly HttpClient client;

    public QueryTransportTests(RouteTests.Factory factory) => client = factory.CreateClient();

    private static IEntityStoreService Store => RouteTests.Factory.Store;

    [Fact]
    public async Task A_query_carrying_no_body_draws_the_reading_an_empty_body_draws()
    {
        var ability = Ability();

        using var response = await client.TooltipAsync(EntityType.Ability, ability.Id);

        var card = await response.BodyAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"A QUERY carrying no body answered {(int)response.StatusCode}, not 200. Body: {card}");

        var empty = await client.TooltipHtmlAsync(EntityType.Ability, ability.Id, new TooltipRequest());

        Assert.Equal(empty, card);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_refused_as_a_bad_request()
    {
        var ability = Ability();

        using var message = new HttpRequestMessage(
            HttpMethod.Query, $"/{CodexAddresses.Tooltip(EntityType.Ability, ability.Id)}")
        {
            Content = new StringContent("{\"hero\":", Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(message, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_stat_the_table_does_not_carry_is_refused_by_name()
    {
        var ability = Ability();

        using var response = await client.TooltipAsync(
            EntityType.Ability, ability.Id, new TooltipRequest().Rated("Nonesuch", 10m));

        var body = await response.BodyAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nonesuch", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rating_roll_and_a_percentage_roll_of_one_stat_both_reach_the_card()
    {
        var resolved = Store.Stats.Resolve(HasteStat);

        Assert.NotNull(resolved);

        var ability = Hastening();
        var row = Store.Stats.Row(resolved);

        var bare = await client.TooltipHtmlAsync(EntityType.Ability, ability.Id, new TooltipRequest());

        var rated = await client.TooltipHtmlAsync(
            EntityType.Ability, ability.Id, new TooltipRequest().Rated(HasteStat, 1000m));

        var percentage = await client.TooltipHtmlAsync(
            EntityType.Ability, ability.Id, new TooltipRequest().Percent(HasteStat, 10m));

        var both = await client.TooltipHtmlAsync(
            EntityType.Ability,
            ability.Id,
            new TooltipRequest().Rated(HasteStat, 1000m).Percent(HasteStat, 10m));

        Assert.Contains(Haste(StatConversion.FromPercentage(10m, row)), percentage, StringComparison.Ordinal);
        Assert.NotEqual(bare, rated);
        Assert.NotEqual(bare, percentage);
        Assert.NotEqual(rated, both);
        Assert.NotEqual(percentage, both);
    }

    [Fact]
    public async Task Two_ratings_of_one_stat_stay_two_rolls_where_one_rating_is_one_roll()
    {
        var (item, label) = Declared();

        var separate = await client.TooltipHtmlAsync(
            EntityType.Item, item.Id, new TooltipRequest().Rated(label, 100m, 250m));

        var single = await client.TooltipHtmlAsync(
            EntityType.Item, item.Id, new TooltipRequest().Rated(label, 350m));

        Assert.Contains(
            $"<span class=\"v random\">&#x2B;350</span><span>{label}</span>", separate, StringComparison.Ordinal);

        Assert.Contains(
            $"<span class=\"v\">&#x2B;350</span><span>{label}</span>", single, StringComparison.Ordinal);
    }

    private static string Haste(decimal share) =>
        $"for {(share * 100m).ToString(Trimmed, CultureInfo.InvariantCulture)}% haste";

    private static AbilityDocument Ability()
    {
        foreach (var ability in Store.Entities.OfType<AbilityDocument>())
        {
            if (Store.All(EntityType.Ability, ability.Id).Count == 1) return ability;
        }

        throw new InvalidOperationException(
            "No ability id the export gives one reading of, so no tooltip answers without a hero.");
    }

    private static AbilityDocument Hastening()
    {
        foreach (var ability in Store.Entities.OfType<AbilityDocument>())
        {
            if (ability.ScalesWithHaste is not true) continue;
            if (ability.CastTime is not > 0) continue;
            if (Store.All(EntityType.Ability, ability.Id).Count != 1) continue;

            return ability;
        }

        throw new InvalidOperationException(
            "No ability casts over time and scales with haste, so no card prints a cast a haste roll shortens.");
    }

    private static (ItemDocument Item, string Label) Declared()
    {
        foreach (var item in Store.Entities.OfType<ItemDocument>())
        {
            if (item.Stats is not { Count: > 0 } slots) continue;
            if (Store.All(EntityType.Item, item.Id).Count != 1) continue;

            foreach (var slot in slots)
            {
                var repeats = slots.Count(other =>
                    string.Equals(other.Stat, slot.Stat, StringComparison.OrdinalIgnoreCase));

                if (repeats != 1) continue;

                if (!Store.Settings.StatCalculation.Attributes.TryGetValue(slot.Stat, out var attribute)) continue;
                if (attribute.DisplayName is not { Length: > 0 } label) continue;
                if (!string.Equals(Store.Stats.Resolve(label), slot.Stat, StringComparison.OrdinalIgnoreCase)) continue;

                return (item, label);
            }
        }

        throw new InvalidOperationException(
            "No item declares one slot of a stat a request can name, so no request states the magnitude " +
            "that slot prints.");
    }
}

public sealed class QueryCacheTests : IClassFixture<QueryCacheTests.Factory>
{
    private readonly HttpClient client;

    private readonly CountingCache cache;

    public QueryCacheTests(Factory factory)
    {
        client = factory.CreateClient();
        cache = (CountingCache)factory.Services.GetRequiredService<IOutputCacheStore>();
    }

    [Fact]
    public async Task One_body_keeps_one_cache_entry_and_a_second_body_keeps_its_own()
    {
        var ability = AbilityWithArt();
        var before = cache.Keys.Count;

        var first = await client.TooltipHtmlAsync(EntityType.Ability, ability.Id, new TooltipRequest());
        var again = await client.TooltipHtmlAsync(EntityType.Ability, ability.Id, new TooltipRequest());

        var withArt = await client.TooltipHtmlAsync(
            EntityType.Ability, ability.Id, new TooltipRequest { ShowIcon = true });

        var keys = cache.Keys.Skip(before).ToArray();

        Assert.Equal(first, again);
        Assert.DoesNotContain("<figure class=\"fx-slot", first, StringComparison.Ordinal);
        Assert.Contains("<figure class=\"fx-slot", withArt, StringComparison.Ordinal);
        Assert.Equal(2, keys.Length);
        Assert.Equal(2, keys.Distinct(StringComparer.Ordinal).Count());
    }

    private static AbilityDocument AbilityWithArt()
    {
        foreach (var ability in RouteTests.Factory.Store.Entities.OfType<AbilityDocument>())
        {
            if (ability.Icon is null) continue;
            if (RouteTests.Factory.Store.All(EntityType.Ability, ability.Id).Count != 1) continue;

            return ability;
        }

        throw new InvalidOperationException(
            "No ability carries art of its own, so no request draws a slot beside the card.");
    }

    public sealed class CountingCache(IOutputCacheStore inner) : IOutputCacheStore
    {
        private readonly List<string> keys = [];

        public IReadOnlyList<string> Keys
        {
            get
            {
                lock (keys)
                {
                    return [.. keys];
                }
            }
        }

        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellation) =>
            inner.EvictByTagAsync(tag, cancellation);

        public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellation) =>
            inner.GetAsync(key, cancellation);

        public ValueTask SetAsync(
            string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellation)
        {
            lock (keys)
            {
                keys.Add(key);
            }

            return inner.SetAsync(key, value, tags, validFor, cancellation);
        }
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("S3:Bucket", "icons");
            builder.UseSetting("AWS:Region", "us-east-1");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExportSource>();
                services.AddSingleton<IExportSource>(new FolderExportSource(Export.Found()));

                var descriptor = services.Last(service =>
                    service.ServiceType == typeof(IOutputCacheStore) && !service.IsKeyedService);

                services.Remove(descriptor);

                services.AddSingleton<IOutputCacheStore>(provider => new CountingCache(Inner(descriptor, provider)));
            });
        }

        private static IOutputCacheStore Inner(ServiceDescriptor descriptor, IServiceProvider provider) =>
            (IOutputCacheStore)(descriptor.ImplementationInstance
                ?? descriptor.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!));
    }
}
