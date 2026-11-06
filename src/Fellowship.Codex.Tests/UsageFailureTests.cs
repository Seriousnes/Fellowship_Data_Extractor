using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Fellowship.Codex.Usage;

namespace Fellowship.Codex.Tests;

public sealed class FailureStoreTests : IDisposable
{
    private static readonly UsageRoute Route = new(UsageArea.Page, "/{slug}", "item");

    private static readonly Agent Crawler = new("GPTBot", 0, Agent.Other, false);

    private readonly string folder = Directory.CreateTempSubdirectory("failures-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    private UsageStore Store() => new(Path.Combine(folder, "usage.db"));

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void Failures_are_counted_by_reason_route_and_client()
    {
        var store = Store();
        var now = DateTimeOffset.UtcNow;
        var counter = new UsageCounter(new FixedTime(now));

        counter.Failure(Route, "GET", 503, FailureReason.Rejected, Crawler);
        counter.Failure(Route, "GET", 503, FailureReason.Rejected, Crawler);
        counter.Failure(Route, "GET", 503, FailureReason.TimedOut, Crawler);
        store.Write(counter.Drain());

        var rows = store.Failures(now.AddHours(-2));

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Single(row => row.Reason == FailureReason.Rejected).Count);
        Assert.Equal(ClientKind.Crawler, rows[0].Kind);
        Assert.Equal(3, store.FailureMinutes(now.AddHours(-2)).Sum(point => point.Requests));
    }

    [Fact]
    public void A_rollup_merges_failures_by_day_and_retention_deletes_them()
    {
        var store = Store();
        var day = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);

        foreach (var hour in new[] { 1, 5, 23 })
        {
            var counter = new UsageCounter(new FixedTime(day.AddHours(hour)));

            counter.Failure(Route, "GET", 503, FailureReason.Rejected, Crawler);
            store.Write(counter.Drain());
        }

        store.Prune(day.AddDays(-1), day.AddDays(2), day.AddDays(2));

        Assert.Equal(3, Assert.Single(store.Failures(day.AddDays(-1))).Count);
        Assert.Empty(store.FailureMinutes(day.AddDays(-1)));

        store.Prune(day.AddDays(1), day.AddDays(2), day.AddDays(2));

        Assert.Empty(store.Failures(day.AddDays(-1)));
    }

    [Fact]
    public void Clearing_deletes_failures_and_samples_older_than_the_cutoff()
    {
        var store = Store();
        var day = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);

        foreach (var at in new[] { day, day.AddDays(5) })
        {
            var counter = new UsageCounter(new FixedTime(at));

            counter.Failure(Route, "GET", 503, FailureReason.Exception, Crawler);
            store.Write(counter.Drain());
            store.Write(new ServerSample(at.ToUnixTimeSeconds(), 1, 2, 3, 4, 5, 6, 7));
        }

        store.Clear(day.AddDays(1));

        var since = day.AddDays(-1);

        Assert.Equal(1, Assert.Single(store.Failures(since)).Count);
        Assert.Equal(1, store.FailureMinutes(since).Sum(point => point.Requests));
        Assert.Single(store.Samples(since));
    }

    [Fact]
    public void Errors_are_charted_per_bucket_by_status_class()
    {
        var store = Store();
        var now = DateTimeOffset.UtcNow;
        var counter = new UsageCounter(new FixedTime(now));

        counter.Request(Route, "GET", 200, false, Crawler, 1);
        counter.Request(Route, "GET", 404, false, Crawler, 1);
        counter.Request(Route, "GET", 503, false, Crawler, 1);
        counter.Request(Route, "GET", 503, false, Crawler, 1);
        store.Write(counter.Drain());

        var points = store.ErrorSeries(now.AddHours(-2), 3600);

        Assert.Equal(1, points.Single(point => point.Series == UsageRanges.ClientErrors).Requests);
        Assert.Equal(2, points.Single(point => point.Series == UsageRanges.ServerErrors).Requests);
    }
}

public class RingTests
{
    [Fact]
    public void A_ring_keeps_the_newest_items_newest_first()
    {
        var ring = new Ring<int>(3);

        foreach (var item in Enumerable.Range(1, 5)) ring.Add(item);

        Assert.Equal([5, 4, 3], ring.Newest());
    }
}

public class ClientTagTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void One_address_has_one_tag_for_a_day_and_a_new_tag_the_next_day()
    {
        var address = IPAddress.Parse("203.0.113.7");
        var clock = new Clock(new DateTimeOffset(2026, 1, 10, 1, 0, 0, TimeSpan.Zero));
        var tags = new ClientTags(clock);
        var first = tags.Tag(address);

        clock.Now = clock.Now.AddHours(20);

        Assert.Equal(first, tags.Tag(address));
        Assert.NotEqual(first, tags.Tag(IPAddress.Parse("203.0.113.8")));

        clock.Now = clock.Now.AddHours(10);

        Assert.NotEqual(first, tags.Tag(address));
    }
}

public class RecentLogTests
{
    [Fact]
    public void A_repeated_event_adds_to_its_count_and_keeps_the_latest_text()
    {
        var logs = new RecentLogs(TimeProvider.System);

        logs.Add(LogLevel.Warning, "Category", "1", "Failed {Path}", "Failed /a", null);
        logs.Add(LogLevel.Warning, "Category", "1", "Failed {Path}", "Failed /b", null);

        var row = Assert.Single(logs.Newest());

        Assert.Equal(2, row.Count);
        Assert.Equal("Failed /b", row.Message);
    }

    [Fact]
    public void A_flood_of_distinct_events_stays_within_the_capacity()
    {
        var logs = new RecentLogs(TimeProvider.System);

        foreach (var at in Enumerable.Range(0, RecentLogs.Capacity + 50))
        {
            logs.Add(LogLevel.Warning, "Category", "1", $"Template {at}", "Text", null);
        }

        Assert.Equal(RecentLogs.Capacity, logs.Newest().Count);
    }
}

public sealed class FailureReasonTests : IAsyncDisposable
{
    private const string Slow = "/slow";

    private const string Broken = "/broken";

    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly WebApplication app;

    public FailureReasonTests()
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddServing();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<UsageCounter>();
        builder.Services.AddSingleton(new Ring<FailedRequest>(UsageExtensions.FailedRequestCapacity));
        builder.Services.AddSingleton<ClientTags>();
        builder.Services.AddSingleton<ServerWatch>();

        app = builder.Build();
        app.UseMiddleware<UsageMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseRequestTimeouts();

        app.MapGet(Slow, async (HttpContext http) =>
            {
                await release.Task.WaitAsync(http.RequestAborted);

                return Results.Ok();
            })
            .RequireRateLimiting(Serving.Policy)
            .WithRequestTimeout(Serving.Policy);

        app.MapGet(Broken, IResult () => throw new InvalidOperationException("Broken."));
    }

    public async ValueTask DisposeAsync()
    {
        release.TrySetResult();
        await app.DisposeAsync();
    }

    private IReadOnlyList<FailedRequest> Failed => app.Services.GetRequiredService<Ring<FailedRequest>>().Newest();

    [Fact]
    public async Task A_request_past_the_queue_is_rejected_and_the_limiter_reports_its_load()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await app.StartAsync(cancellation);

        var client = app.GetTestClient();
        var held = RequestLoad.PermitLimit + RequestLoad.QueueLimit;
        var requests = Enumerable.Range(0, held).Select(_ => client.GetAsync(Slow, cancellation)).ToArray();
        var load = app.Services.GetRequiredService<RequestLoad>();

        while (load.Now() != (RequestLoad.PermitLimit, RequestLoad.QueueLimit)) await Task.Delay(10, cancellation);

        using var rejected = await client.GetAsync($"{Slow}?page=2", cancellation);

        release.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);

        var failure = Assert.Single(Failed);

        Assert.Equal(FailureReason.Rejected, failure.Reason);
        Assert.Equal($"{Slow}?page=2", failure.Address);
        Assert.Equal(1, Assert.Single(app.Services.GetRequiredService<UsageCounter>().Drain().Failures).Value);
    }

    [Fact]
    public async Task A_request_past_the_timeout_is_tagged_timed_out()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await app.StartAsync(cancellation);

        using var response = await app.GetTestClient().GetAsync(Slow, cancellation);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(FailureReason.TimedOut, Assert.Single(Failed).Reason);
    }

    [Fact]
    public async Task A_thrown_exception_is_tagged_with_its_type()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await app.StartAsync(cancellation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.GetTestClient().GetAsync(Broken, cancellation));

        var failure = Assert.Single(Failed);

        Assert.Equal(FailureReason.Exception, failure.Reason);
        Assert.Equal(typeof(InvalidOperationException).FullName, failure.Exception);
    }
}

[Collection(Admin.Name)]
public class DashboardRoutingTests(AdminHost host)
{
    [Fact]
    public void The_dashboard_takes_no_limiter_permit_and_public_pages_do()
    {
        var endpoints = host.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        bool Limited(string pattern) =>
            endpoints.Single(endpoint => endpoint.RoutePattern.RawText == pattern)
                .Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null;

        Assert.False(Limited(UsageRoutes.Admin));
        Assert.True(Limited("/heroes"));
    }
}
