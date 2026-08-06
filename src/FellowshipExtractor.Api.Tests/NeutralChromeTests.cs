using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class NeutralChromeTests(Export export)
{
    private static readonly string[] Chrome = ["--fx-header:url(", "--fx-frame:url(", "--fx-slot:url("];

    [Fact]
    public void A_row_that_carries_no_rarity_is_still_handed_every_piece_of_chrome()
    {
        var talent = export.Store.Entities.OfType<TalentDocument>().First();

        Assert.Null(export.Tooltip.Rarity(talent, new TooltipQuery()));

        var variables = export.Tooltip.Variables(null);

        Assert.All(Chrome, piece => Assert.Contains(piece, variables, StringComparison.Ordinal));
        Assert.DoesNotContain("none", variables, StringComparison.Ordinal);
    }

    [Fact]
    public void The_chrome_a_rarity_less_row_wears_is_its_own_and_not_a_rungs()
    {
        var neutral = export.Tooltip.Variables(null);
        var legendary = export.Tooltip.Variables(
            export.Store.Vocabulary.Rarities.Single(rung => rung.Tier == 6));

        Assert.NotEqual(legendary, neutral);
        Assert.Contains("-neutral.png", neutral, StringComparison.Ordinal);
        Assert.DoesNotContain("-legendary.png", neutral, StringComparison.Ordinal);
    }

    [Fact]
    public void The_neutral_plate_is_painted_flat_in_the_colour_the_border_is_drawn_in()
    {
        var painted = export.Chrome.Payloads()
            .Single(payload => payload.Name.EndsWith("TooltipHeader_Subheader-neutral.png", StringComparison.Ordinal));

        using var plate = Image.Load<Rgba32>(painted.Content);

        Assert.Equal([Drawn(Border())], Shades(plate));
    }

    private static string Drawn(Image<Rgba32> texture)
    {
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);

        texture.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A < 128) continue;

                    var shade = $"#{row[x].R:X2}{row[x].G:X2}{row[x].B:X2}";
                    counted[shade] = 1 + counted.GetValueOrDefault(shade);
                }
            }
        });

        return counted.MaxBy(shade => shade.Value).Key;
    }

    private static HashSet<string> Shades(Image<Rgba32> texture)
    {
        var shades = new HashSet<string>(StringComparer.Ordinal);

        texture.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    shades.Add($"#{row[x].R:X2}{row[x].G:X2}{row[x].B:X2}");
                }
            }
        });

        return shades;
    }

    private Image<Rgba32> Border() =>
        Image.Load<Rgba32>(Path.Combine(export.Paths.Ui, "T_Borders_BGPatterns_SmallIndentAlt.png"));

    [Fact]
    public void The_pages_own_panel_heading_keeps_the_plate_the_build_ships()
    {
        Assert.EndsWith("TooltipHeader_Subheader.png", export.Chrome.PanelHeader, StringComparison.Ordinal);
        Assert.NotEqual(export.Chrome.PanelHeader, export.Chrome.Header(null));
    }
}
