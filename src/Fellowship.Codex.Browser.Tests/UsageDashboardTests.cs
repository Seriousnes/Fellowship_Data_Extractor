using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Fellowship.Codex.Usage;

namespace Fellowship.Codex.Browser.Tests;

/// <summary>The Codex with a usage database and an owner signed in by a request header.</summary>
public sealed class UsageSite : Site
{
    public const string Owner = "4242";

    public const string UserHeader = "X-Test-User";

    private readonly string folder = Directory.CreateTempSubdirectory("usage-site-").FullName;

    public static readonly KeyValuePair<string, string>[] SignedIn = [new(UserHeader, Owner)];

    protected override void Configure(IWebHostBuilder web) => web
        .UseSetting(UsageSettings.DatabaseKey, Path.Combine(folder, "usage.db"))
        .UseSetting(UsageSettings.OwnersKey, Owner)
        .ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderUser>(HeaderUser.Scheme, null);
            services.PostConfigure<AuthenticationOptions>(options => options.DefaultAuthenticateScheme = HeaderUser.Scheme);
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(folder, true);
    }

    private sealed class HeaderUser(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "header";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers[UserHeader].FirstOrDefault() is not { } id) return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, "owner")], Scheme);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}

// Two hosts started at once intercept each other's builder, so this collection runs alone.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Usage : ICollectionFixture<UsageSite>
{
    public const string Name = "usage";
}

[Collection(Usage.Name)]
public sealed class UsageDashboardTests(UsageSite site)
{
    private static Task<bool> DrawnAsync(IPage page, string canvas) =>
        page.EvaluateAsync<bool>("id => !!window.Chart?.getChart(id)", canvas);

    [Fact]
    public async Task The_owner_downloads_the_database_and_clears_old_counts_after_confirming()
    {
        const string Old = "/api/old-counts";

        var store = site.Services.GetRequiredService<UsageStore>();
        var since = DateTimeOffset.UtcNow - TimeSpan.FromDays(UsageRanges.Days.Max() + 30);
        var counter = new UsageCounter(new Earlier(TimeSpan.FromDays(UsageRanges.Days.Max() + 10)));

        counter.Request(new UsageRoute(UsageArea.Api, Old, UsageRoute.None), "GET", 200, false, Agent.Unknown, 1);
        store.Write(counter.Drain());

        await using var context = await site.NewContextAsync(UsageSite.SignedIn);
        var page = await context.NewPageAsync();

        await page.GotoAsync(UsageRoutes.Admin);

        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByText("Download database").ClickAsync());

        Assert.EndsWith(".db", download.SuggestedFilename);

        var button = page.Locator("#cx-usage-clear button");

        void Dismiss(object? sender, IDialog dialog) => _ = dialog.DismissAsync();

        page.Dialog += Dismiss;
        await button.ClickAsync();
        page.Dialog -= Dismiss;

        Assert.Contains(store.Routes(since), route => route.Route == Old);

        void Accept(object? sender, IDialog dialog) => _ = dialog.AcceptAsync();

        page.Dialog += Accept;
        await page.RunAndWaitForResponseAsync(() => button.ClickAsync(), $"**{UsageExtensions.Clear}");

        Assert.DoesNotContain(store.Routes(since), route => route.Route == Old);
    }

    private sealed class Earlier(TimeSpan by) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() - by;
    }

    [Fact]
    public async Task A_grid_draws_the_rows_in_view_sorts_by_number_and_keeps_its_filter_across_a_refresh()
    {
        const int Rows = 5000;

        var failed = site.Services.GetRequiredService<Ring<FailedRequest>>();
        var counter = site.Services.GetRequiredService<UsageCounter>();
        var route = new UsageRoute(UsageArea.Page, "/{slug}", UsageRoute.None);

        foreach (var at in Enumerable.Range(1, Rows))
        {
            failed.Add(new FailedRequest(
                DateTimeOffset.UtcNow, FailureReason.Rejected, 503, "GET", UsageArea.Page, route.Route, $"/items?page={at}",
                Agent.Unknown, "abcd1234", at, null));
        }

        counter.Failure(route, "GET", 503, FailureReason.Rejected, Agent.Unknown);
        counter.Request(route, "GET", 503, false, Agent.Unknown, 1);
        site.Services.GetRequiredService<UsageStore>().Write(counter.Drain());

        await using var context = await site.NewContextAsync(UsageSite.SignedIn);
        var page = await context.NewPageAsync();

        await page.SetViewportSizeAsync(1400, 1000);
        await page.GotoAsync($"{UsageRoutes.Admin}?days=1");

        var grid = page.Locator("[data-grid='failed']");
        var rows = grid.Locator("tr.cx-grid-row");

        await Assertions.Expect(rows.First).ToBeVisibleAsync();
        Assert.InRange(await rows.CountAsync(), 1, 100);

        await grid.GetByRole(AriaRole.Button, new() { Name = "Duration" }).ClickAsync();
        await Assertions.Expect(rows.First.Locator("td").Nth(11)).ToContainTextAsync("5,000.0");

        await grid.Locator(".cx-grid-scroll").EvaluateAsync("node => { node.scrollTop = node.scrollHeight; }");
        await Assertions.Expect(rows.Last.Locator("td").Nth(11)).ToContainTextAsync("1.0");

        var filter = grid.GetByLabel("Filter Address");

        await filter.PressSequentiallyAsync("page=4999");
        await Assertions.Expect(rows).ToHaveCountAsync(1);
        await Assertions.Expect(grid.Locator(".cx-grid-foot")).ToContainTextAsync("1 of 5,000 rows");

        var before = await page.Locator("#cx-usage-status").TextContentAsync();

        await Assertions.Expect(page.Locator("#cx-usage-status")).Not.ToHaveTextAsync(before!, new() { Timeout = 20_000 });
        await Assertions.Expect(filter).ToHaveValueAsync("page=4999");
        await Assertions.Expect(filter).ToBeFocusedAsync();
        await Assertions.Expect(rows).ToHaveCountAsync(1);

        if (Environment.GetEnvironmentVariable("CODEX_SCREENSHOT") is { Length: > 0 } shot)
        {
            await filter.FillAsync(string.Empty);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = shot, FullPage = true });
        }
    }

    [Fact]
    public async Task The_dashboard_draws_both_charts_and_refreshes_the_report_in_place()
    {
        await using var context = await site.NewContextAsync(UsageSite.SignedIn);
        var page = await context.NewPageAsync();

        foreach (var path in new[] { "/", "/api/catalog", "/heroes" }) await page.GotoAsync(path);

        site.Services.GetRequiredService<UsageStore>().Write(site.Services.GetRequiredService<UsageCounter>().Drain());

        await page.SetViewportSizeAsync(1400, 1000);
        await page.GotoAsync($"{UsageRoutes.Admin}?days=1");

        var status = page.Locator("#cx-usage-status");
        await Assertions.Expect(status).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Updated "));

        Assert.True(await DrawnAsync(page, "cx-usage-live"));
        Assert.True(await DrawnAsync(page, "cx-usage-range"));

        if (Environment.GetEnvironmentVariable("CODEX_SCREENSHOT") is { Length: > 0 } shot)
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = shot, FullPage = true });
        }

        var before = await status.TextContentAsync();

        await page.Locator("#cx-usage-ranges a[data-days='30']").ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("days=30$"));
        await Assertions.Expect(page.Locator("#cx-usage-range").Locator("xpath=ancestor::div[contains(@class,'cx-panel')][1]/div[contains(@class,'cx-panel-head')]"))
            .ToHaveTextAsync(UsageRanges.Head(30));
        await Assertions.Expect(page.Locator("#cx-usage-ranges a[data-days='30']")).ToHaveAttributeAsync("aria-current", "page");
        Assert.True(await DrawnAsync(page, "cx-usage-range"));
        Assert.NotNull(before);
    }
}
