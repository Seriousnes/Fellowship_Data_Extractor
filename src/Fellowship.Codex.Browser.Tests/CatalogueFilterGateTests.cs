namespace Fellowship.Codex.Browser.Tests;

[Collection(Browsing.Name)]
public sealed class CatalogueFilterGateTests(Site site)
{
    private const string Filtered = "/items?armor=Plate";

    private const string Results = ".cx-tabs";

    [Fact]
    public async Task A_filtered_address_opened_directly_shows_its_results_at_the_same_address()
    {
        await using var context = await site.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Filtered);
        await page.Locator(Results).WaitForAsync();

        Assert.EndsWith(Filtered, page.Url);
    }

    [Fact]
    public async Task A_click_on_a_filter_loads_without_the_fixed_page()
    {
        await using var context = await site.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/items");
        await page.Locator($"a[href='{Filtered}']").First.DispatchEventAsync("click");
        await page.WaitForURLAsync($"**{Filtered}");
        await page.Locator(Results).WaitForAsync();

        Assert.Empty(await context.CookiesAsync());
    }
}
