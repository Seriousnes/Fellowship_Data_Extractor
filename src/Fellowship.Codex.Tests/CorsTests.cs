using System.Net;

namespace Fellowship.Codex.Tests;

public sealed class CorsTests : IClassFixture<RouteTests.Factory>
{
    private const string Origin = "https://fellowshipanalyzer.com";

    private readonly HttpClient client;

    public CorsTests(RouteTests.Factory factory) => client = factory.CreateClient();

    [Fact]
    public async Task An_api_read_from_another_origin_carries_the_header_that_allows_it()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
        request.Headers.Add("Origin", Origin);

        var response = await client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task A_preflight_for_an_api_read_names_the_method_it_allows()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/entities");
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var allowed = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods"))
            .Split(',', StringSplitOptions.TrimEntries);

        Assert.Contains("GET", allowed);
    }

    [Fact]
    public async Task A_tooltip_read_from_another_origin_carries_the_header_that_allows_it()
    {
        var id = RouteTests.Factory.Store.Entities.OfType<AbilityDocument>().First().Id;

        using var request = new HttpRequestMessage(HttpMethod.Query, $"/api/ability/{id}/tooltip");
        request.Headers.Add("Origin", Origin);

        var response = await client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task A_preflight_for_a_tooltip_names_the_query_method_it_allows()
    {
        var id = RouteTests.Factory.Store.Entities.OfType<AbilityDocument>().First().Id;

        using var request = new HttpRequestMessage(HttpMethod.Options, $"/api/ability/{id}/tooltip");
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "QUERY");

        var response = await client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var allowed = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods"))
            .Split(',', StringSplitOptions.TrimEntries);

        Assert.Contains("QUERY", allowed);
    }

    [Fact]
    public async Task A_page_is_not_offered_to_another_origin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", Origin);

        var response = await client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
