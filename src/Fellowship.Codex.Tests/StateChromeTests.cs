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
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

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
