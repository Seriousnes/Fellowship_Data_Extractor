using System.Text.RegularExpressions;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public partial class WindowChromeTests(Export export)
{
    [GeneratedRegex(@"(?:^|[{}])\s*([^@{}/][^{}]*?)\s*\{")]
    private static partial Regex Selector();

    private static readonly string[] NoRungOnly = ["var(--fx-thin)", "var(--fx-fill)"];

    private static readonly string Rows = $".{TooltipView.Root}";

    private static readonly string NoRung = $".{TooltipView.Root}.plain";

    private static string Body(string sheet, string selector)
    {
        var opens = sheet.IndexOf(selector + " {", StringComparison.Ordinal);

        Assert.True(opens >= 0, $"The sheet carries no {selector} rule of its own.");

        return sheet[opens..sheet.IndexOf('}', opens)];
    }

    private static IEnumerable<(string Selector, string Body)> Rules(string sheet)
    {
        foreach (Match rule in Selector().Matches(sheet))
        {
            var opens = rule.Index + rule.Length - 1;
            var closes = sheet.IndexOf('}', opens);

            if (closes < 0) yield break;

            var body = sheet[(opens + 1)..closes];

            foreach (var selector in rule.Groups[1].Value.Split(','))
            {
                yield return (selector.Trim(), body);
            }
        }
    }

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
    public void The_prelude_hands_the_window_its_thin_frame_and_its_fill()
    {
        var declared = Rules(export.Tooltip.Style())
            .Where(rule => string.Equals(rule.Selector, Rows, StringComparison.Ordinal))
            .Select(rule => rule.Body)
            .ToArray();

        Assert.NotEmpty(declared);

        Assert.Contains(declared, body =>
            body.Contains($"--fx-thin:url({export.Chrome.ThinWindow})", StringComparison.Ordinal) &&
            body.Contains($"--fx-fill:url({export.Chrome.WindowFill})", StringComparison.Ordinal));
    }

    [Fact]
    public void A_row_with_no_rung_wears_both_frames_the_widget_stacks()
    {
        var sheet = export.Tooltip.Style();

        var thin = Body(sheet, $"{NoRung} .card::before");

        Assert.Contains("border-width: 2px 8px;", thin, StringComparison.Ordinal);
        Assert.Contains("border-image: var(--fx-thin) 4 16 stretch;", thin, StringComparison.Ordinal);

        var rounded = Body(sheet, $"{NoRung} .card::after");

        Assert.Contains("border-width: 16px;", rounded, StringComparison.Ordinal);
        Assert.Contains("border-image: var(--fx-window) 32 stretch;", rounded, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_with_no_rung_is_filled_by_the_brush_the_window_stretches_over_it()
    {
        Assert.Contains(
            "background: var(--fx-fill) 0 0/100% 100% no-repeat, var(--core-secondary-900);",
            Body(export.Tooltip.Style(), $"{NoRung} .card"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_prelude_hands_the_header_rule_the_gold_band_it_is_drawn_from()
    {
        var declared = Rules(export.Tooltip.Style())
            .Where(rule => string.Equals(rule.Selector, Rows, StringComparison.Ordinal))
            .Select(rule => rule.Body)
            .ToArray();

        Assert.NotEmpty(declared);

        Assert.Contains(declared, body =>
            body.Contains($"--fx-divider:url({export.Chrome.GoldDivider})", StringComparison.Ordinal));
    }

    [Fact]
    public void The_base_window_rules_are_the_ones_a_row_that_states_a_rung_inherits()
    {
        var sheet = export.Tooltip.Style();

        var window = Body(sheet, $"{Rows} .card::after");

        Assert.Contains("border: 7px solid transparent;", window, StringComparison.Ordinal);
        Assert.Contains("border-image: var(--fx-window) 7 stretch;", window, StringComparison.Ordinal);

        var card = Body(sheet, $"{Rows} .card");

        Assert.Contains("background: var(--core-secondary-900);", card, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--fx-fill)", card, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_but_a_row_with_no_rung_draws_the_thin_frame_or_the_fill()
    {
        var drawn = Rules(export.Tooltip.Style())
            .Where(rule => NoRungOnly.Any(brush =>
                rule.Body.Contains(brush, StringComparison.Ordinal)))
            .ToArray();

        Assert.All(NoRungOnly, brush =>
            Assert.Contains(drawn, rule => rule.Body.Contains(brush, StringComparison.Ordinal)));

        Assert.All(drawn, rule =>
            Assert.True(
                rule.Selector.StartsWith(NoRung, StringComparison.Ordinal),
                $"{rule.Selector} draws the window the game keeps for a row that states no rung, " +
                "so a row that states one would wear it too."));
    }
}
