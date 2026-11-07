using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.DependencyInjection;

using Fellowship.Codex.Usage;

namespace Fellowship.Codex.Tests;

[Collection(Admin.Name)]
public class CrawlerValidatorTests(AdminHost host)
{
    private const string Page = "/talent/209/sword-board";

    private const string Googlebot =
        "Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    private const string Chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";

    private async Task<HttpResponseMessage> GetAsync(string path, string agent, Action<HttpRequestHeaders>? headers = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.UserAgent.ParseAdd(agent);
        headers?.Invoke(request.Headers);

        return await host.Client().SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_crawler_revisiting_an_unchanged_page_receives_a_304_by_either_validator()
    {
        using var first = await GetAsync(Page, Googlebot);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.CacheControl?.NoCache);

        var tag = first.Headers.ETag!;
        var modified = first.Content.Headers.LastModified!.Value;

        using var byTag = await GetAsync(Page, Googlebot, headers => headers.IfNoneMatch.Add(tag));
        using var byDate = await GetAsync(Page, Googlebot, headers => headers.IfModifiedSince = modified);

        Assert.Equal(HttpStatusCode.NotModified, byTag.StatusCode);
        Assert.Equal(HttpStatusCode.NotModified, byDate.StatusCode);
        Assert.Empty(await byTag.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_crawler_with_a_validator_from_another_image_or_export_receives_the_page()
    {
        using var response = await GetAsync(Page, Googlebot, headers => headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"0\"", true)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_browser_receives_no_validator_and_no_store()
    {
        using var response = await GetAsync(Page, Chrome);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task A_missing_page_gives_a_crawler_no_validator()
    {
        using var response = await GetAsync("/talent/999999/none", Googlebot);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.ETag);
    }
}

[Collection(Admin.Name)]
public class SitemapTests(AdminHost host)
{
    private IReadOnlyList<SitemapUrl> Urls => Sitemap.Urls(host.Factory.Services);

    [Fact]
    public void Each_url_is_unique_has_no_query_and_fits_one_file()
    {
        var urls = Urls;

        Assert.InRange(urls.Count, 1, Sitemap.Limit);
        Assert.Equal(urls.Count, urls.Select(url => url.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.All(urls, url => Assert.DoesNotContain('?', url.Path));
        Assert.DoesNotContain(urls, url => url.Path.StartsWith(UsageRoutes.Admin, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_first_url_under_each_top_segment_is_a_page()
    {
        var client = host.Client();

        var firsts = Urls
            .GroupBy(url => url.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty)
            .Select(group => group.First().Path);

        foreach (var path in firsts)
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} answered {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task A_patch_notes_page_has_each_notes_anchor_and_fields()
    {
        var notes = host.Factory.Services.GetRequiredService<IPatchNotesService>();

        var (build, note) = notes.Builds
            .SelectMany(named => notes.Of(named)!.Notes.Select(found => (named, found)))
            .First(pair => pair.found.Fields.Count > 0);

        var page = await host.Client().GetStringAsync($"/patch-notes/{build}", TestContext.Current.CancellationToken);

        Assert.Contains($"<li id=\"{note.Key.Slug}-{note.Key.Id}\"", page);
        Assert.Contains("<dl class=\"cx-pn-fields\">", page);
    }

    [Fact]
    public void The_xml_has_absolute_locations_under_the_origin()
    {
        var origin = new Uri("https://codex.example");
        var document = System.Xml.Linq.XDocument.Parse(Sitemap.Xml([new SitemapUrl("/heroes", null)], origin));
        var location = document.Descendants().Single(element => element.Name.LocalName == "loc").Value;

        Assert.Equal("https://codex.example/heroes", location);
    }
}
