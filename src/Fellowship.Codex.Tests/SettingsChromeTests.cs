namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class SettingsChromeTests(Export export)
{
    private static void Under(string url)
    {
        var name = url[(url.LastIndexOf('/') + 1)..];

        Assert.EndsWith(".png", name, StringComparison.Ordinal);
        Assert.EndsWith($"/{ChromeAssetService.Prefix}/{name}", url, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_slot_url_resolves_under_the_prefix()
    {
        Under(export.Chrome.SlotPlate);
        Under(export.Chrome.SlotBacking(null));
        Under(export.Chrome.SlotFrame(null));
        Under(export.Chrome.SlotGlyph(null));

        Assert.Null(export.Chrome.SlotBevel(null));

        Assert.All(export.Store.Settings.Rarities, rung =>
        {
            Under(export.Chrome.SlotBacking(rung.Name));
            Under(export.Chrome.SlotFrame(rung.Name));
            Under(export.Chrome.SlotBevel(rung.Name)!);
        });

        Assert.NotEmpty(export.Store.Settings.SlotIcons);

        Assert.All(export.Store.Settings.SlotIcons.Keys, slot => Under(export.Chrome.SlotGlyph(slot)));
    }

    [Fact]
    public void Entity_icon_art_resolves_under_the_prefix_the_chrome_composes()
    {
        var worn = export.Store.Entities.OfType<ItemDocument>().First(item => item.Icon is not null);

        var art = export.Tooltip.Art(worn, export.Tooltip.Rarity(worn, new TooltipQuery()));

        Assert.NotNull(art);
        Under(art);

        Assert.Equal(export.Chrome.Art(worn.Icon), export.View.Icon(worn.Icon));
        Under(export.View.Icon(worn.Icon)!);

        Assert.Null(export.Chrome.Art(null));
        Assert.Null(export.View.Icon(null));
    }
}
