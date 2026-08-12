using AngleSharp;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Tests;

public sealed class BrowseSortTests : IClassFixture<RouteTests.Factory>
{
    private const string Unreadable = "could not be read";

    private readonly HttpClient client;

    public BrowseSortTests(RouteTests.Factory factory) => client = factory.CreateClient();

    public static TheoryData<string> Listed
    {
        get
        {
            var slugs = new TheoryData<string>();

            foreach (var type in RouteTests.Factory.Store.Types.Where(PublicTypes.Listed))
            {
                slugs.Add(EntityTypes.Slug(type));
            }

            return slugs;
        }
    }

    private async Task<IDocument> Page(string address)
    {
        var body = await client.GetStringAsync(address, Xunit.TestContext.Current.CancellationToken);

        return await BrowsingContext.New(Configuration.Default).OpenAsync(
            request => request.Content(body), Xunit.TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Listed))]
    public async Task Every_column_a_list_offers_to_sort_by_is_a_sorting_the_codex_declares(string slug)
    {
        var listing = $"/browse?type={slug}";

        var page = await Page(listing);

        Assert.DoesNotContain(Unreadable, page.Body!.TextContent, StringComparison.OrdinalIgnoreCase);

        var links = page.QuerySelectorAll("table.cx-rows thead th a[href]")
            .Select(link => link.GetAttribute("href")!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(links);

        foreach (var href in links)
        {
            var sorted = await Page(href);

            Assert.DoesNotContain(
                Unreadable,
                sorted.Body!.TextContent,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
