namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class WindowChromeTests(Export export)
{
    private static readonly string[] Windowed =
    [
        "T_Borders_Rounded_16", "T_Borders_Thin", "T_Borders_WindowBackground_Windowed",
    ];

    [Fact]
    public void The_window_answers_every_texture_the_widget_names()
    {
        Assert.EndsWith(
            "T_Borders_Rounded_16.png", export.Chrome.RoundedWindow, StringComparison.Ordinal);

        Assert.EndsWith("T_Borders_Thin.png", export.Chrome.ThinWindow, StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Borders_WindowBackground_Windowed.png", export.Chrome.WindowFill,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_texture_the_window_names_is_on_disk_to_be_uploaded()
    {
        Assert.All(Windowed, texture =>
            Assert.True(
                File.Exists(Path.Combine(export.Paths.Ui, texture + ".png")),
                $"{texture}.png is not in {export.Paths.Ui}, so the window would draw nothing."));
    }

    [Fact]
    public void The_prelude_hands_the_window_its_thin_frame_and_its_fill()
    {
        var sheet = export.Tooltip.Style();

        Assert.Contains($"--fx-thin:url({export.Chrome.ThinWindow})", sheet, StringComparison.Ordinal);
        Assert.Contains($"--fx-fill:url({export.Chrome.WindowFill})", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_with_no_rung_wears_the_thin_frame_at_the_slices_the_brush_margin_gives_it()
    {
        var sheet = export.Tooltip.Style();

        Assert.Contains(
            ".fx-tip.plain .card::before {", sheet, StringComparison.Ordinal);

        Assert.Contains("border-width: 2px 8px;", sheet, StringComparison.Ordinal);
        Assert.Contains("border-image: var(--fx-thin) 4 16 stretch;", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_with_no_rung_is_filled_by_the_brush_the_window_stretches_over_it()
    {
        var sheet = export.Tooltip.Style();

        Assert.Contains(
            "background: var(--fx-fill) 0 0/100% 100% no-repeat, var(--core-secondary-900);",
            sheet,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_that_states_a_rung_keeps_the_window_it_already_had()
    {
        var sheet = export.Tooltip.Style();

        Assert.Contains("border: 7px solid transparent;", sheet, StringComparison.Ordinal);
        Assert.Contains("border-image: var(--fx-window) 7 stretch;", sheet, StringComparison.Ordinal);

        Assert.Contains("border-width: 16px;", sheet, StringComparison.Ordinal);
        Assert.Contains("border-image: var(--fx-window) 32 stretch;", sheet, StringComparison.Ordinal);
    }
}
