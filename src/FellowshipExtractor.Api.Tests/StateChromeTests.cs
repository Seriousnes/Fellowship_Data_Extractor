using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class StateChromeTests(Export export)
{
    private string Texture(string url) =>
        Path.Combine(export.Paths.Ui, url[(url.LastIndexOf('/') + 1)..]);

    private static (int Width, int Height) Shape(string file)
    {
        using var image = Image.Load(file);
        return (image.Width, image.Height);
    }

    private static bool Painted(Image<Rgba32> image, int row)
    {
        for (var x = 0; x < image.Width; x++)
        {
            if (image[x, row].A > 0) return true;
        }

        return false;
    }

    [Fact]
    public void Every_state_frame_is_the_shape_the_stylesheet_derives_its_height_from()
    {
        Assert.All(
            ChromeAssets.NodeStates,
            state => Assert.Equal((208, 240), Shape(Texture(export.Chrome.TraitNode(state)))));

        Assert.All(
            ChromeAssets.BlessingStates,
            state => Assert.Equal((148, 188), Shape(Texture(export.Chrome.BlessingPlate(state)))));
    }

    [Fact]
    public void The_gold_rule_uploaded_is_the_band_cropped_out_of_its_canvas()
    {
        using var canvas = Image.Load<Rgba32>(Texture(export.Chrome.GoldDivider));

        Assert.Equal(56, canvas.Height);

        Assert.All(
            Enumerable.Range(0, canvas.Height),
            row => Assert.Equal(row is >= 24 and <= 27, Painted(canvas, row)));

        var payload = export.Chrome.Payloads().Single(carried =>
            carried.Name.EndsWith("T_GoldDivider.png", StringComparison.Ordinal));

        using var band = Image.Load<Rgba32>(payload.Content);

        Assert.Equal(canvas.Width, band.Width);
        Assert.Equal(4, band.Height);
        Assert.All(Enumerable.Range(0, band.Height), row => Assert.True(Painted(band, row)));
    }

    [Fact]
    public void A_node_answers_the_frame_the_state_names()
    {
        Assert.EndsWith(
            "T_Buttons_TraitNode_SelectedDefault.png", export.Chrome.TraitNode("selected"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_TraitNode_AvailableDefault.png", export.Chrome.TraitNode("available"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_TraitNode_UnavailableDefault.png", export.Chrome.TraitNode("unavailable"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_nothing_asks_for_is_the_one_the_reader_is_looking_at()
    {
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode(null));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("no such state"));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("SELECTED"));
    }

    [Fact]
    public void The_states_are_the_ones_the_build_ships_and_the_padlock_is_there()
    {
        Assert.Equal(["selected", "available", "unavailable"], ChromeAssets.NodeStates);
        Assert.EndsWith("T_Icons_Locked.png", export.Chrome.LockGlyph, StringComparison.Ordinal);
        Assert.EndsWith("T_GoldDivider.png", export.Chrome.GoldDivider, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blessing_answers_the_plate_its_state_names()
    {
        Assert.Equal(["allocated", "unallocated"], ChromeAssets.BlessingStates);

        Assert.EndsWith(
            "T_Buttons_BlessingButton_Allocated.png", export.Chrome.BlessingPlate("allocated"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_BlessingButton_Unallocated.png", export.Chrome.BlessingPlate("unallocated"),
            StringComparison.Ordinal);

        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Chrome.BlessingPlate(null));
    }

    [Fact]
    public void Every_blessing_has_the_allocated_art_the_build_ships()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.NotEmpty(blessings);

        Assert.All(blessings, blessing =>
        {
            var allocated = export.Chrome.BlessingArt(blessing.Icon, null);

            Assert.NotNull(allocated);
            Assert.EndsWith(
                blessing.Icon!.Replace("Unallocated", "Allocated", StringComparison.Ordinal),
                allocated,
                StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(
                export.Paths.Ui,
                blessing.Icon.Replace("Unallocated", "Allocated", StringComparison.Ordinal))));
        });

        Assert.Null(export.Chrome.BlessingArt("Berserker16.png", null));
        Assert.Null(export.Chrome.BlessingArt(null, null));
    }
}
