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

    private static Rectangle Well(string file)
    {
        using var plate = Image.Load<Rgba32>(file);

        var fill = plate[plate.Width / 2, plate.Height / 2];

        var reached = new bool[plate.Width, plate.Height];
        var walking = new Stack<Point>();
        walking.Push(new Point(plate.Width / 2, plate.Height / 2));
        reached[plate.Width / 2, plate.Height / 2] = true;

        int left = plate.Width, right = -1, top = plate.Height, bottom = -1;

        while (walking.Count > 0)
        {
            var at = walking.Pop();

            left = Math.Min(left, at.X);
            right = Math.Max(right, at.X);
            top = Math.Min(top, at.Y);
            bottom = Math.Max(bottom, at.Y);

            foreach (var step in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
            {
                var next = new Point(at.X + step.X, at.Y + step.Y);

                if (next.X < 0 || next.Y < 0 || next.X >= plate.Width || next.Y >= plate.Height) continue;
                if (reached[next.X, next.Y] || !plate[next.X, next.Y].Equals(fill)) continue;

                reached[next.X, next.Y] = true;
                walking.Push(next);
            }
        }

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    [Fact]
    public void The_well_the_stylesheet_cuts_the_art_to_is_the_hole_in_every_plate()
    {
        var wells = ChromeAssetService.NodeStates
            .Select(state => Well(Texture(export.Chrome.TraitNode(state))))
            .ToArray();

        Assert.All(wells, well =>
        {
            Assert.Equal(new Size(154, 180), well.Size);
            Assert.Equal(208 - well.Right, well.Left);
            Assert.Equal(240 - well.Bottom, well.Top);
        });

        Assert.Single(wells.Distinct());
    }

    [Fact]
    public void Every_state_frame_is_the_shape_the_stylesheet_derives_its_height_from()
    {
        Assert.All(
            ChromeAssetService.NodeStates,
            state => Assert.Equal((208, 240), Shape(Texture(export.Chrome.TraitNode(state)))));

        Assert.All(
            ChromeAssetService.BlessingStates,
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
        Assert.Equal(["selected", "available", "unavailable"], ChromeAssetService.NodeStates);
        Assert.EndsWith("T_Icons_Locked.png", export.Chrome.LockGlyph, StringComparison.Ordinal);
        Assert.EndsWith("T_GoldDivider.png", export.Chrome.GoldDivider, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blessing_answers_the_plate_its_state_names()
    {
        Assert.Equal(["allocated", "unallocated"], ChromeAssetService.BlessingStates);

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
