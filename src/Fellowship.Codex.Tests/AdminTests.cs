using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Fellowship.Codex.Usage;

namespace Fellowship.Codex.Tests;

public sealed class AdminHost : IAsyncLifetime
{
    public const string Owner = "4242";

    public const string UserHeader = "X-Test-User";

    private readonly string folder = Directory.CreateTempSubdirectory("admin-").FullName;

    public WebApplicationFactory<global::Program> Factory { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(web => web
            .UseEnvironment("Development")
            .UseSetting(UsageSettings.DatabaseKey, Path.Combine(folder, "usage.db"))
            .UseSetting(UsageSettings.OwnersKey, Owner)
            .ConfigureTestServices(services => services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, HeaderUser>(HeaderUser.Scheme, null))
            .ConfigureTestServices(services => services.PostConfigure<AuthenticationOptions>(options =>
                options.DefaultAuthenticateScheme = HeaderUser.Scheme)));

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        Directory.Delete(folder, true);
    }

    public HttpClient Client(string? user = null)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        if (user is not null) client.DefaultRequestHeaders.Add(UserHeader, user);

        return client;
    }

    /// <summary>Signs in the GitHub user id named in the test header.</summary>
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

[CollectionDefinition(Name)]
public sealed class Admin : ICollectionFixture<AdminHost>
{
    public const string Name = "admin";
}

[Collection(Admin.Name)]
public class AdminTests(AdminHost host)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/talent/209/sword-board")]
    [InlineData("/heroes")]
    [InlineData("/api/catalog")]
    [InlineData("/og/talent/209/sword-board.png")]
    public async Task An_anonymous_request_receives_no_cookie(string path)
    {
        using var response = await host.Client().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"), string.Join("; ", response.Headers.Where(h => h.Key == "Set-Cookie").SelectMany(h => h.Value)));
    }

    [Fact]
    public async Task The_dashboard_issues_its_antiforgery_cookie_inside_the_admin_area()
    {
        using var response = await host.Client(AdminHost.Owner).GetAsync(UsageRoutes.Admin, TestContext.Current.CancellationToken);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains($"path={UsageRoutes.Admin}", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_owner_downloads_a_readable_copy_of_the_database()
    {
        var cancellation = TestContext.Current.CancellationToken;

        using var response = await host.Client(AdminHost.Owner).GetAsync(UsageExtensions.Database, cancellation);
        var content = await response.Content.ReadAsByteArrayAsync(cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.EndsWith(".db", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(content, 0, 16));
    }

    [Fact]
    public async Task A_user_other_than_an_owner_cannot_download_the_database()
    {
        using var response = await host.Client("1").GetAsync(UsageExtensions.Database, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clearing_without_an_antiforgery_token_is_refused()
    {
        using var form = new FormUrlEncodedContent([new(UsageExtensions.ClearDays, "1")]);
        using var response = await host.Client(AdminHost.Owner).PostAsync(UsageExtensions.Clear, form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_request_for_the_dashboard_is_sent_to_sign_in()
    {
        using var response = await host.Client().GetAsync(UsageRoutes.Admin, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(UsageExtensions.Login, response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task A_user_other_than_an_owner_is_refused()
    {
        using var response = await host.Client("1").GetAsync(UsageRoutes.Admin, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_owner_sees_the_counted_routes()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var client = host.Client(AdminHost.Owner);

        (await client.GetAsync("/og/talent/209/sword-board.png", cancellation)).Dispose();

        host.Factory.Services.GetRequiredService<UsageStore>()
            .Write(host.Factory.Services.GetRequiredService<UsageCounter>().Drain());

        using var response = await client.GetAsync($"{UsageRoutes.Admin}?days=1", cancellation);
        var page = await response.Content.ReadAsStringAsync(cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("/og/{slug}/{id:int}/{named}.png", page);
    }
}
