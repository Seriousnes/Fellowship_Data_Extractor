using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public partial class SheetTests(Export export)
{
    private static readonly string[] Inherited =
    [
        "color:", "font:", "font-variant-numeric:", "letter-spacing:", "word-spacing:",
        "text-transform:", "text-indent:", "text-align:", "white-space:", "text-shadow:",
    ];

    [GeneratedRegex(@"(?:^|[{}])\s*([^@{}/][^{}]*?)\s*\{")]
    private static partial Regex Selector();

    [GeneratedRegex(@"<style>(.*?)</style>", RegexOptions.Singleline)]
    private static partial Regex Embedded();

    private async Task<string> Served()
    {
        var services = new ServiceCollection()
            .AddSingleton(export.Tooltip).AddSingleton(export.View).AddSingleton(export.Store)
            .BuildServiceProvider();

        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<Components.Tooltip.Tooltip>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    ["Entity"] = new AbilityDocument { Id = 1, Description = "burns" },
                }));

            return rendered.ToHtmlString();
        });
    }

    [Fact]
    public async Task A_caller_is_handed_the_whole_composed_sheet()
    {
        var embedded = Embedded().Match(await Served());

        Assert.True(embedded.Success, "The served fragment carries no style element.");
        Assert.Equal(export.Tooltip.Style(), embedded.Groups[1].Value);
    }

    [Fact]
    public void The_sheet_carries_the_rules_the_file_holds_and_the_rules_the_build_generates()
    {
        var sheet = export.Tooltip.Style();

        Assert.Contains($".{TooltipView.Root} .card {{", sheet, StringComparison.Ordinal);
        Assert.Contains($".{TooltipView.Root} .stats .random {{", sheet, StringComparison.Ordinal);
        Assert.Contains($".{TooltipView.Root} .fx-slot img {{", sheet, StringComparison.Ordinal);
        Assert.Contains($".{TooltipView.Root} .rt-", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public void The_root_declares_every_property_a_host_page_could_otherwise_hand_it()
    {
        var sheet = export.Tooltip.Style();

        var opens = sheet.IndexOf($".{TooltipView.Root} {{", StringComparison.Ordinal);

        Assert.True(opens >= 0, $"The sheet carries no .{TooltipView.Root} rule of its own.");

        var root = sheet[opens..sheet.IndexOf('}', opens)].Replace(" ", string.Empty);

        foreach (var property in Inherited)
        {
            Assert.Contains(property, root, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_selector_the_sheet_carries_sits_under_the_root_class()
    {
        var scope = $".{TooltipView.Root}";

        var rules = Selector().Matches(export.Tooltip.Style());

        Assert.True(
            rules.Count > 40,
            $"Only {rules.Count} rules were read out of the sheet, so this asserts almost nothing. " +
            "The sheet holds a rule per element the tooltip draws plus one per shipped text style.");

        foreach (Match rule in rules)
        {
            foreach (var selector in rule.Groups[1].Value.Split(','))
            {
                var trimmed = selector.Trim();

                Assert.True(
                    trimmed.StartsWith(scope, StringComparison.Ordinal),
                    $"{trimmed} is not scoped to {scope}, so it would style a page that embeds the fragment.");
            }
        }
    }
}
