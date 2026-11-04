using System.Globalization;
using Fellowship.Codex.Browser.Tests.Pages;

namespace Fellowship.Codex.Browser.Tests;

[Collection(Browsing.Name)]
public sealed class ItemLevelInputTests(Site site)
{
    private ItemDocument Wearable => site.Store.Entities.OfType<ItemDocument>().First(item => item.Stats is { Count: > 0 });

    private EnchantDocument Ranked =>
        site.Store.Entities.OfType<EnchantDocument>().First(enchant => AugmentationRanks.Ranked(site.Store, enchant));

    private int MaxItemLevel => site.Store.Settings.Items.MaxItemLevel;

    /// <summary>A level other than <paramref name="level"/>, so committing it changes what the page shows.</summary>
    private static int Other(int level) => level > 1 ? level - 1 : level + 1;

    private static string Text(int level) => level.ToString(CultureInfo.InvariantCulture);

    private async Task<EntityPage> OpenAsync(IBrowserContext context, EntityDocument entity)
    {
        var page = new EntityPage(await context.NewPageAsync());
        await page.OpenAsync(entity, site.Ui);

        return page;
    }

    [Fact]
    public async Task An_item_level_typed_with_a_leading_zero_stays_in_the_knob_after_the_render()
    {
        await using var context = await site.NewContextAsync();
        var page = await OpenAsync(context, Wearable);
        var level = Other(site.Store.DropItemLevel(Wearable));

        await page.ItemLevel.CommitAsync("0" + Text(level));
        await Assertions.Expect(page.ItemPower).ToHaveTextAsync(Text(level));

        Assert.Equal("0" + Text(level), await page.ItemLevel.ValueAsync());
    }

    [Fact]
    public async Task An_emptied_item_level_knob_stays_empty_and_the_tooltip_reads_the_drop_item_level()
    {
        await using var context = await site.NewContextAsync();
        var page = await OpenAsync(context, Wearable);
        var drop = site.Store.DropItemLevel(Wearable);

        await page.ItemLevel.CommitAsync(Text(Other(drop)));
        await Assertions.Expect(page.ItemPower).ToHaveTextAsync(Text(Other(drop)));

        await page.ItemLevel.ClearAndCommitAsync();
        await Assertions.Expect(page.ItemPower).ToHaveTextAsync(Text(drop));

        Assert.Equal(string.Empty, await page.ItemLevel.ValueAsync());
    }

    [Fact]
    public async Task An_enchant_item_level_typed_with_a_leading_zero_stays_in_the_slider_box_after_the_render()
    {
        await using var context = await site.NewContextAsync();
        var page = await OpenAsync(context, Ranked);
        var level = Other(MaxItemLevel);

        await page.ItemLevel.CommitAsync("0" + Text(level));
        await Assertions.Expect(page.ItemLevelBar).ToHaveValueAsync(Text(level));

        Assert.Equal("0" + Text(level), await page.ItemLevel.ValueAsync());
    }

    [Fact]
    public async Task An_emptied_enchant_item_level_box_stays_empty_and_the_slider_reads_the_max_item_level()
    {
        await using var context = await site.NewContextAsync();
        var page = await OpenAsync(context, Ranked);

        await page.ItemLevel.CommitAsync(Text(Other(MaxItemLevel)));
        await Assertions.Expect(page.ItemLevelBar).ToHaveValueAsync(Text(Other(MaxItemLevel)));

        await page.ItemLevel.ClearAndCommitAsync();
        await Assertions.Expect(page.ItemLevelBar).ToHaveValueAsync(Text(MaxItemLevel));

        Assert.Equal(string.Empty, await page.ItemLevel.ValueAsync());
    }
}
