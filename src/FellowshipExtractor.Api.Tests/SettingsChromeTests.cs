namespace FellowshipExtractor.Api.Tests;

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
    public void Every_rarity_wears_the_ornament_the_settings_name()
    {
        var ornaments = export.Store.Settings.RarityOrnaments;

        Assert.NotEmpty(ornaments);

        Assert.All(export.Store.Settings.Rarities, rung =>
        {
            var drawn = export.Chrome.Ornament(rung.Name);

            if (!ornaments.TryGetValue(rung.Name, out var texture))
            {
                Assert.Null(drawn);
                return;
            }

            Assert.NotNull(drawn);
            Under(drawn);
            Assert.EndsWith($"/{texture}.png", drawn, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Every_rarity_is_drawn_in_the_colour_row_the_settings_name()
    {
        var rows = export.Store.Settings.RarityColorRows;
        var colors = export.Store.Settings.Colors;

        Assert.All(export.Store.Settings.Rarities, rung =>
        {
            Assert.True(
                rows.TryGetValue(rung.Name, out var row),
                $"The settings name no colour row for {rung.Name}, so the rung is drawn in no colour.");

            Assert.True(colors.ContainsKey(row!), $"{row} is not a row the colours table carries.");
            Assert.Equal('#' + colors[row!], export.View.Tokens.Rarity(rung.Name));
        });
    }

    [Fact]
    public void Every_section_heading_is_drawn_in_the_colour_row_the_settings_name()
    {
        var rows = export.Store.Settings.SectionHeadingRows;

        Assert.Equal(export.View.Tokens.Color(rows["Ability"]), export.Chrome.AbilityBadge.Colour);
        Assert.Equal(export.View.Tokens.Color(rows["Legendary"]), export.Chrome.LegendaryBadge.Colour);
        Assert.Equal(export.View.Tokens.Color(rows["SetBonus"]), export.Chrome.SetBonusBadge.Colour);

        Assert.Equal("#EA4F84", export.Chrome.AbilityBadge.Colour);
        Assert.Equal("#FF8000", export.Chrome.LegendaryBadge.Colour);
        Assert.Equal("#00BB1D", export.Chrome.SetBonusBadge.Colour);
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
