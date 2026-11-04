using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Fellowship.Codex.Browser.Tests;

/// <summary>
/// The Codex on Kestrel at a free port, reading the newest export in the checkout, and one Chromium.
/// Each test opens its own browser context.
/// </summary>
public class Site : IAsyncLifetime
{
    private readonly WebApplicationFactory<global::Program> host;

    public Site() =>
        host = new WebApplicationFactory<global::Program>().WithWebHostBuilder(web => Configure(web.UseEnvironment("Development")));

    public IServiceProvider Services => host.Services;

    /// <summary>Settings and services a derived site adds to the host.</summary>
    protected virtual void Configure(IWebHostBuilder web)
    {
    }

    private IPlaywright? playwright;

    private IBrowser? browser;

    public Uri Root { get; private set; } = null!;

    public IEntityStoreService Store => host.Services.GetRequiredService<IEntityStoreService>();

    public ICodexViewService Ui => host.Services.GetRequiredService<ICodexViewService>();

    public async ValueTask InitializeAsync()
    {
        host.UseKestrel(0);
        host.StartServer();

        Root = new Uri(host.Services.GetRequiredService<IServer>().Features
            .GetRequiredFeature<IServerAddressesFeature>().Addresses.First());

        // Installing a browser already present is a no-op.
        var installed = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (installed != 0) throw new InvalidOperationException($"Playwright's Chromium install exited with {installed}.");

        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync();
    }

    public Task<IBrowserContext> NewContextAsync(IEnumerable<KeyValuePair<string, string>>? headers = null) =>
        browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = Root.ToString(), ExtraHTTPHeaders = headers });

    public virtual async ValueTask DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();

        playwright?.Dispose();

        await host.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class Browsing : ICollectionFixture<Site>
{
    public const string Name = "browsing";
}
