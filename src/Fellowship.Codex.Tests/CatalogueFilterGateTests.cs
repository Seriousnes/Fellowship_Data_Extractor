using System.Net;
using System.Net.Http.Headers;

namespace Fellowship.Codex.Tests;

[Collection(Admin.Name)]
public class CatalogueFilterGateTests(AdminHost host)
{
    private const string Filtered = "/items?armor=Plate";

    private const string Results = "cx-tabs";

    private async Task<(HttpResponseMessage Response, string Body)> GetAsync(string path, Action<HttpRequestHeaders>? headers = null)
    {
        var cancellation = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        headers?.Invoke(request.Headers);

        var response = await host.Client().SendAsync(request, cancellation);

        return (response, await response.Content.ReadAsStringAsync(cancellation));
    }

    [Fact]
    public async Task A_filtered_request_from_a_client_without_scripts_receives_the_fixed_page()
    {
        var (response, body) = await GetAsync(Filtered);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains(CatalogueFilterGate.Cookie, body);
        Assert.DoesNotContain(Results, body);
    }

    [Fact]
    public async Task A_filtered_search_receives_the_fixed_page()
    {
        var (_, body) = await GetAsync("/search?hero=Ardeos&hero=Helena");

        Assert.DoesNotContain(Results, body);
    }

    [Fact]
    public async Task An_unfiltered_catalogue_page_renders()
    {
        var (_, body) = await GetAsync("/items");

        Assert.Contains(Results, body);
    }

    [Fact]
    public async Task Enhanced_navigation_renders_a_filtered_page()
    {
        var (_, body) = await GetAsync(Filtered, headers =>
            headers.TryAddWithoutValidation("Accept", $"text/html; {CatalogueFilterGate.EnhancedNavigation}"));

        Assert.Contains(Results, body);
    }

    [Fact]
    public async Task The_cookie_renders_a_filtered_page()
    {
        var (_, body) = await GetAsync(Filtered, headers =>
            headers.Add("Cookie", $"{CatalogueFilterGate.Cookie}=1"));

        Assert.Contains(Results, body);
    }

    [Fact]
    public async Task A_query_on_any_other_page_renders()
    {
        var (_, body) = await GetAsync("/talent/209/sword-board?hero=Ardeos");

        Assert.DoesNotContain(CatalogueFilterGate.Cookie, body);
    }

    [Fact]
    public async Task A_filtered_dungeon_list_receives_the_fixed_page()
    {
        var (_, body) = await GetAsync("/dungeon/13/wraithtide-vault?slot=Weapon");

        Assert.Contains(CatalogueFilterGate.Cookie, body);
        Assert.DoesNotContain(Results, body);
    }

    [Fact]
    public async Task A_dungeon_page_with_no_filter_renders_its_lists()
    {
        var (_, body) = await GetAsync("/dungeon/13/wraithtide-vault");

        Assert.Contains(Results, body);
    }
}
