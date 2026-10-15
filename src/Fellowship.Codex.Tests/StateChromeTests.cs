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
    public void Every_blessing_answers_the_allocated_art_beside_its_unallocated_twin()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.NotEmpty(blessings);

        Assert.All(blessings, blessing =>
        {
            var twin = blessing.Icon!.Replace("Unallocated", "Allocated", StringComparison.Ordinal);

            Assert.Equal(export.Chrome.Texture(twin), export.Chrome.BlessingTexture(blessing.Icon, null));
        });

        Assert.Null(export.Chrome.BlessingTexture("Berserker16", null));
        Assert.Null(export.Chrome.BlessingTexture(null, null));
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
