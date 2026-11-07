namespace Fellowship.Codex.Browser.Tests;

[Collection(Browsing.Name)]
public sealed class ItemTraitPageTests(Site site)
{
    [Theory]
    [InlineData("/item-trait/3", "Inspired Arms")]
    [InlineData("/item-trait/30", "Topaz Winds")]
    public async Task A_trait_page_states_it_does_not_roll_on_items(string address, string name)
    {
        await using var context = await site.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(address);
        await page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true }).First.WaitForAsync();

        Assert.True(await page.GetByText("Does not roll on items").IsVisibleAsync());
    }
}
