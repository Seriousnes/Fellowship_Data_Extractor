using Fellowship.Codex.Usage;

namespace Fellowship.Codex.Tests;

public class UserAgentTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36", "Chrome", 141, "Windows", false)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.3537.57", "Edge", 141, "Windows", false)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 OPR/124.0.0.0", "Opera", 124, "macOS", false)]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:144.0) Gecko/20100101 Firefox/144.0", "Firefox", 144, "Linux", false)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.6 Mobile/15E148 Safari/604.1", "Safari", 18, "iOS", true)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", "Safari", 26, "macOS", false)]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/28.0 Chrome/130.0.0.0 Mobile Safari/537.36", "Samsung Internet", 28, "Android", true)]
    [InlineData("Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)", "Discordbot", 0, "Other", false)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_10_1) AppleWebKit/600.2.5 (KHTML, like Gecko) Version/8.0.2 Safari/600.2.5 (Applebot/0.1)", "Applebot", 0, "macOS", false)]
    [InlineData("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; GPTBot/1.2; +https://openai.com/gptbot)", "GPTBot", 0, "Other", false)]
    [InlineData("Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.7390.122 Mobile Safari/537.36 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", "Googlebot", 0, "Android", true)]
    [InlineData("Mozilla/5.0 (compatible; SomeNewCrawler/1.0)", "Bot", 0, "Other", false)]
    [InlineData("Twitterbot/1.0", "Twitterbot", 0, "Other", false)]
    [InlineData("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)", "Facebook", 0, "Other", false)]
    [InlineData("curl/8.9.1", "curl", 0, "Other", false)]
    [InlineData("", "Other", 0, "Other", false)]
    public void A_header_reads_as_its_family_version_and_system(string header, string browser, int major, string os, bool mobile)
    {
        Assert.Equal(new Agent(browser, major, os, mobile), UserAgents.Classify(header));
    }
}

public class LatencyTests
{
    [Fact]
    public void Each_duration_is_in_the_first_bucket_bounding_it()
    {
        Assert.Equal(0, Latency.Bucket(Latency.Bounds[0]));
        Assert.Equal(1, Latency.Bucket(Latency.Bounds[0] + 1));
        Assert.Equal(Latency.Bounds.Length, Latency.Bucket(Latency.Bounds[^1] + 1));
    }

    [Fact]
    public void The_95th_percentile_is_the_bucket_reaching_95_percent()
    {
        long[] buckets = [90, 4, 1, 5, 0];

        Assert.Equal(2, Latency.Percentile(buckets, 0.95));
        Assert.Equal(0, Latency.Percentile(buckets, 0.5));
        Assert.Null(Latency.Percentile([0, 0, 0, 0, 0], 0.95));
    }
}

public sealed class UsageStoreTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("usage-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void Two_flushes_of_one_route_add_into_one_row()
    {
        var store = new UsageStore(Path.Combine(folder, "usage.db"));
        var time = new FixedTime(DateTimeOffset.UtcNow);
        var counter = new UsageCounter(time);
        var route = new UsageRoute(UsageArea.Card, "/og/{slug}/{id:int}/{named}.png", "item");
        var agent = new Agent("Discordbot", 0, Agent.Other, false);

        counter.Request(route, "GET", 200, false, agent, 120);
        store.Write(counter.Drain());
        counter.Request(route, "GET", 200, false, agent, 4);
        counter.Request(route, "GET", 503, false, agent, 2000);
        store.Write(counter.Drain());

        var since = time.GetUtcNow() - TimeSpan.FromDays(1);
        var row = Assert.Single(store.Routes(since));

        Assert.Equal(3, row.Requests);
        Assert.Equal(1, row.ServerErrors);
        Assert.Equal(2000, row.MaxMilliseconds);
        Assert.Equal(Latency.Bounds.Length, row.P95Bucket);

        var browser = Assert.Single(store.Clients(since));

        Assert.Equal(3, browser.Requests);
        Assert.Equal(1, browser.ServerErrors);
    }

    [Fact]
    public void A_rollup_merges_the_hours_of_one_day_and_keeps_the_totals()
    {
        var store = new UsageStore(Path.Combine(folder, "usage.db"));
        var day = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var route = new UsageRoute(UsageArea.Api, "/api/catalog", UsageRoute.None);

        foreach (var hour in new[] { 1, 5, 23 })
        {
            var counter = new UsageCounter(new FixedTime(day.AddHours(hour)));
            counter.Request(route, "GET", 200, false, Agent.Unknown, hour);
            counter.Error("Category", "System.Exception");
            store.Write(counter.Drain());
        }

        store.Prune(day.AddDays(-1), day.AddDays(2), day.AddDays(-1));
        store.Prune(day.AddDays(-1), day.AddDays(2), day.AddDays(-1));

        var row = Assert.Single(store.Routes(day.AddDays(-1)));
        Assert.Equal(3, row.Requests);
        Assert.Equal(23, row.MaxMilliseconds);
        Assert.Equal(3, Assert.Single(store.Errors(day.AddDays(-1))).Count);

        store.Prune(day.AddDays(1), day.AddDays(2), day.AddDays(1));
        Assert.Empty(store.Routes(day.AddDays(-1)));
    }

    [Fact]
    public void Clearing_deletes_the_counts_older_than_the_cutoff_and_keeps_the_rest()
    {
        var store = new UsageStore(Path.Combine(folder, "usage.db"));
        var day = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var route = new UsageRoute(UsageArea.Api, "/api/catalog", UsageRoute.None);

        foreach (var at in new[] { day, day.AddDays(5) })
        {
            var counter = new UsageCounter(new FixedTime(at));
            counter.Request(route, "GET", 200, false, Agent.Unknown, 1);
            counter.Error("Category", "System.Exception");
            store.Write(counter.Drain());
        }

        store.Clear(day.AddDays(1));

        var since = day.AddDays(-1);

        Assert.Equal(1, Assert.Single(store.Routes(since)).Requests);
        Assert.Equal(1, Assert.Single(store.Clients(since)).Requests);
        Assert.Equal(1, Assert.Single(store.Errors(since)).Count);
    }

    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("get", "GET")]
    [InlineData("QUERY", "QUERY")]
    [InlineData("PROPFIND", UsageMiddleware.OtherMethod)]
    public void A_method_outside_the_known_set_is_one_row(string method, string counted)
    {
        Assert.Equal(counted, UsageMiddleware.Method(method));
    }

    [Fact]
    public void A_snapshot_is_a_readable_copy()
    {
        var store = new UsageStore(Path.Combine(folder, "usage.db"));
        var counter = new UsageCounter(TimeProvider.System);

        counter.Request(new UsageRoute(UsageArea.Api, "/api/catalog", UsageRoute.None), "GET", 200, true, Agent.Unknown, 3);
        store.Write(counter.Drain());

        var copy = Path.Combine(folder, "copy.db");
        store.Snapshot(copy);

        Assert.Single(new UsageStore(copy).Areas(DateTimeOffset.UtcNow - TimeSpan.FromDays(1)));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
