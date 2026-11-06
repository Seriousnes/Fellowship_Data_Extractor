using System.Net;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Fellowship.Codex.Usage;

namespace Fellowship.Codex.Tests;

public class ClientKindTests
{
    [Theory]
    [InlineData("Chrome", ClientKind.Browser)]
    [InlineData("Safari", ClientKind.Browser)]
    [InlineData("Googlebot", ClientKind.Crawler)]
    [InlineData(UserAgents.Bot, ClientKind.Crawler)]
    [InlineData("Discordbot", ClientKind.Preview)]
    [InlineData("curl", ClientKind.Tool)]
    [InlineData(Agent.Other, ClientKind.Unknown)]
    public void Each_family_has_the_kind_its_list_names(string browser, ClientKind kind)
    {
        Assert.Equal(kind, UserAgents.Kind(browser));
    }
}

public sealed class UsageChartTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("charts-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void A_client_row_splits_its_requests_by_area()
    {
        var store = new UsageStore(Path.Combine(folder, "usage.db"));
        var counter = new UsageCounter(TimeProvider.System);
        var chrome = new Agent("Chrome", 153, "Android", true);
        var page = new UsageRoute(UsageArea.Page, "/", UsageRoute.None);

        counter.Request(page, "GET", 200, false, chrome, 5);
        counter.Request(page, "GET", 200, false, chrome, 5);
        counter.Request(new UsageRoute(UsageArea.Circuit, "/_blazor", UsageRoute.None), "GET", 101, false, chrome, null);
        store.Write(counter.Drain());

        var row = Assert.Single(store.Clients(DateTimeOffset.UtcNow - TimeSpan.FromDays(1)));

        Assert.Equal((2L, 1L, 3L), (row.Pages, row.Circuits, row.Requests));
        Assert.Equal(ClientKind.Browser, row.Kind);
    }

    [Fact]
    public void The_live_chart_has_one_point_per_minute_and_a_slot_per_kind()
    {
        var store = new UsageStore(Path.Combine(folder, "usage.db"));
        var counter = new UsageCounter(TimeProvider.System);

        counter.Request(
            new UsageRoute(UsageArea.Page, "/", UsageRoute.None), "GET", 200, false, new Agent("Googlebot", 0, "Android", true), 5);
        store.Write(counter.Drain());

        using var json = JsonDocument.Parse(UsageRanges.Charts(store, 1, DateTimeOffset.UtcNow));
        var live = json.RootElement.GetProperty("live");
        var series = Assert.Single(live.GetProperty("series").EnumerateArray());

        Assert.Equal(60, live.GetProperty("labels").GetArrayLength());
        Assert.Equal(nameof(ClientKind.Crawler), series.GetProperty("name").GetString());
        Assert.Equal((int)ClientKind.Crawler + 1, series.GetProperty("slot").GetInt32());
        Assert.Equal(1, series.GetProperty("values").EnumerateArray().Last().GetInt64());
        Assert.Equal("hour", json.RootElement.GetProperty("range").GetProperty("unit").GetString());
    }
}

[Collection(Admin.Name)]
public class AdminReportTests(AdminHost host)
{
    [Fact]
    public async Task An_owner_receives_the_dashboard_with_its_chart_data()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var client = host.Client(AdminHost.Owner);

        (await client.GetAsync("/api/catalog", cancellation)).Dispose();
        host.Factory.Services.GetRequiredService<UsageStore>()
            .Write(host.Factory.Services.GetRequiredService<UsageCounter>().Drain());

        using var response = await client.GetAsync($"{UsageRoutes.Admin}?days=30", cancellation);
        var body = await response.Content.ReadAsStringAsync(cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("id=\"cx-usage-data\"", body);
        Assert.Contains("\"unit\":\"day\"", body);
    }
}
