namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class StateChromeTests(Export export)
{
    [Fact]
    public void A_node_nothing_asks_for_is_the_one_the_reader_is_looking_at()
    {
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode(null));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("no such state"));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("SELECTED"));
    }

    [Fact]
    public void Every_blessing_draws_its_hovered_rune_allocated_and_its_own_rune_unallocated()
    {
        var blessings = export.Store.Entities.OfType<GlyphDocument>().ToArray();

        Assert.NotEmpty(blessings);

        Assert.All(blessings, blessing =>
        {
            Assert.NotNull(blessing.IconHovered);
            Assert.NotEqual(blessing.Icon, blessing.IconHovered);

            Assert.Equal(
                export.Chrome.Texture(blessing.IconHovered),
                export.Chrome.BlessingRune(blessing, ChromeAssetService.BlessingState(1)));

            Assert.Equal(
                export.Chrome.Texture(blessing.Icon),
                export.Chrome.BlessingRune(blessing, ChromeAssetService.BlessingState(0)));
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
    public void A_trait_modifier_draws_the_glyph_of_the_slot_whose_pool_holds_its_category()
    {
        var settings = export.Store.Settings;

        foreach (var trait in export.Store.Entities.OfType<ItemTraitDocument>())
        {
            var slot = ItemCraft.KindOfTrait(trait, settings) is { } kind ? ItemCraft.Defining(kind, trait, settings) : null;

            if (slot is null) continue;

            Assert.Contains(trait.CategoryTag!, settings.StatCalculation.SlotDefinitions[slot].ItemTraitPool);
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
