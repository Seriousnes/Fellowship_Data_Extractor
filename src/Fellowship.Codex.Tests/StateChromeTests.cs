namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class StateChromeTests(Export export)
{
    [Fact]
    public void A_node_nothing_asks_for_is_the_one_the_reader_is_looking_at()
    {
        Assert.Equal(export.Chrome.AttunementNode("selected"), export.Chrome.AttunementNode(null));
        Assert.Equal(export.Chrome.AttunementNode("selected"), export.Chrome.AttunementNode("no such state"));
        Assert.Equal(export.Chrome.AttunementNode("selected"), export.Chrome.AttunementNode("SELECTED"));
    }

    [Fact]
    public void Every_glyph_draws_its_hovered_rune_allocated_and_its_own_rune_unallocated()
    {
        var glyphs = export.Store.Entities.OfType<GlyphDocument>().ToArray();

        Assert.NotEmpty(glyphs);

        Assert.All(glyphs, glyph =>
        {
            Assert.NotNull(glyph.IconHovered);
            Assert.NotEqual(glyph.Icon, glyph.IconHovered);

            Assert.Equal(
                export.Chrome.Texture(glyph.IconHovered),
                export.Chrome.GlyphRune(glyph, ChromeAssetService.GlyphState(1)));

            Assert.Equal(
                export.Chrome.Texture(glyph.Icon),
                export.Chrome.GlyphRune(glyph, ChromeAssetService.GlyphState(0)));
        });
    }

    [Fact]
    public void Every_modifier_slot_definition_draws_a_glyph_and_an_empty_slot_draws_none()
    {
        var slots = ChromeAssetService.ModifierSlots(export.Store.Settings);

        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.NotNull(export.Chrome.ModifierGlyph(slot)));
        Assert.Null(export.Chrome.ModifierGlyph(null));
    }

    [Fact]
    public void A_augmentation_modifier_draws_the_glyph_of_the_slot_whose_pool_holds_its_category()
    {
        var settings = export.Store.Settings;

        foreach (var augmentation in export.Store.Entities.OfType<AugmentationDocument>())
        {
            var slot = ItemCraft.KindOfAugmentation(augmentation, settings) is { } kind ? ItemCraft.Defining(kind, augmentation, settings) : null;

            if (slot is null) continue;

            Assert.Contains(augmentation.CategoryTag!, settings.StatCalculation.SlotDefinitions[slot].AugmentationPool);
        }
    }

    [Fact]
    public void Only_the_selected_node_draws_its_icon_in_colour()
    {
        Assert.True(ChromeAssetService.Allocated("selected"));
        Assert.True(ChromeAssetService.Allocated(null));
        Assert.False(ChromeAssetService.Allocated("available"));
        Assert.False(ChromeAssetService.Allocated("unavailable"));
    }

    [Fact]
    public void Only_the_unavailable_node_is_padlocked()
    {
        Assert.True(ChromeAssetService.Padlocked("unavailable"));
        Assert.True(ChromeAssetService.Padlocked("UNAVAILABLE"));
        Assert.False(ChromeAssetService.Padlocked("available"));
        Assert.False(ChromeAssetService.Padlocked("selected"));
        Assert.False(ChromeAssetService.Padlocked(null));
        Assert.False(ChromeAssetService.Padlocked("allocated"));
    }
}
