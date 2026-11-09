using System.Collections.Concurrent;
using System.Net;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Fellowship.Codex.Tests;

/// <summary>A codex with Turnstile configured and siteverify answered by <see cref="SiteVerify"/>.</summary>
public sealed class TurnstileHost : IAsyncLifetime
{
    public const string SiteKey = "1x00000000000000000000AA";

    public const string Hostname = "codex.test";

    public SiteVerify Verify { get; } = new();

    public WebApplicationFactory<global::Program> Factory { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(web => web
            .UseEnvironment("Development")
            .UseSetting(TurnstileSettings.SiteKeyKey, SiteKey)
            .UseSetting(TurnstileSettings.SecretKeyKey, "test-secret")
            .UseSetting(TurnstileSettings.HostnamesKey, Hostname)
            .ConfigureTestServices(services => services
                .AddHttpClient(TurnstileVerification.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Verify)));

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await Factory.DisposeAsync();

    public HttpClient Client() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    /// <summary>Answers siteverify by token: each token names the result it receives.</summary>
    public sealed class SiteVerify : HttpMessageHandler
    {
        public const string Good = "good";

        public const string WrongAction = "wrong-action";

        public const string WrongHost = "wrong-host";

        public const string Down = "down";

        public ConcurrentBag<string> Tokens { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var token = form.Split('&')
                .Select(pair => pair.Split('='))
                .Single(pair => pair[0] == "response")[1];

            Tokens.Add(token);

            var body = token switch
            {
                Good => Result(true, CatalogueFilterGate.Action, Hostname),
                WrongAction => Result(true, "other", Hostname),
                WrongHost => Result(true, CatalogueFilterGate.Action, "elsewhere.test"),
                _ when token.StartsWith(Down, StringComparison.Ordinal) => null,
                _ => Result(false, null, null),
            };

            return body is null
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        private static string Result(bool success, string? action, string? hostname) =>
            System.Text.Json.JsonSerializer.Serialize(new { success, action, hostname });
    }
}

public class TurnstileGateTests(TurnstileHost host) : IClassFixture<TurnstileHost>
{
    private const string Filtered = "/items?armorType=Plate";

    private const string Results = "cx-tabs";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private async Task<string> GetAsync(string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Filtered);

        if (cookie is not null) request.Headers.Add("Cookie", cookie);

        using var response = await host.Client().SendAsync(request, Cancellation);

        return await response.Content.ReadAsStringAsync(Cancellation);
    }

    private async Task<HttpResponseMessage> PassAsync(string token) =>
        await host.Client().PostAsync(
            CatalogueFilterGate.Pass,
            new FormUrlEncodedContent([new(TurnstileVerification.ResponseField, token)]),
            Cancellation);

    [Fact]
    public async Task The_fixed_page_is_a_challenge_for_the_site_key_and_action()
    {
        var body = await GetAsync();

        Assert.Contains($"data-sitekey=\"{TurnstileHost.SiteKey}\"", body);
        Assert.Contains($"data-action=\"{CatalogueFilterGate.Action}\"", body);
        Assert.DoesNotContain(Results, body);
    }

    [Fact]
    public async Task A_cookie_the_page_script_could_set_does_not_pass()
    {
        var body = await GetAsync($"{CatalogueFilterGate.Cookie}=1");

        Assert.DoesNotContain(Results, body);
    }

    [Fact]
    public async Task A_passed_token_sets_a_cookie_that_renders_a_filtered_page()
    {
        using var response = await PassAsync(TurnstileHost.SiteVerify.Good);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Results, await GetAsync(cookie.Split(';')[0]));
    }

    [Theory]
    [InlineData(TurnstileHost.SiteVerify.WrongAction)]
    [InlineData(TurnstileHost.SiteVerify.WrongHost)]
    [InlineData(TurnstileHost.SiteVerify.Down)]
    [InlineData("rejected")]
    public async Task A_token_siteverify_does_not_pass_is_forbidden(string token)
    {
        using var response = await PassAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task A_failed_siteverify_request_is_sent_once()
    {
        const string once = $"{TurnstileHost.SiteVerify.Down}-once";

        using var response = await PassAsync(once);

        Assert.Single(host.Verify.Tokens, token => token == once);
    }

    [Fact]
    public async Task An_oversize_token_is_forbidden_with_no_siteverify_request()
    {
        var token = new string('x', TurnstileVerification.MaxTokenLength + 1);

        using var response = await PassAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(token, host.Verify.Tokens);
    }

    [Fact]
    public async Task An_empty_token_is_forbidden()
    {
        using var response = await PassAsync(string.Empty);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
