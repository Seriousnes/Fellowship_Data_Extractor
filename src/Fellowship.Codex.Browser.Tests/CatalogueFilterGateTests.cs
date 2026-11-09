namespace Fellowship.Codex.Browser.Tests;

[Collection(Browsing.Name)]
public sealed class CatalogueFilterGateTests(Site site)
{
    private const string Filtered = "/items?armorType=Plate";

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
    public async Task An_applied_filter_loads_without_the_fixed_page()
    {
        await using var context = await site.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/items");
        await page.Locator("#pick-kind").SelectOptionAsync("Plate");
        await page.Locator(".cx-apply").ClickAsync();
        await page.WaitForURLAsync("**/items?kind=Plate");
        await page.Locator(Results).WaitForAsync();

        Assert.Empty(await context.CookiesAsync());
    }
}
