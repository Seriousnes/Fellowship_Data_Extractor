using System.Text.RegularExpressions;

namespace FellowshipExtractor.Api.Tests;

public partial class IsolationTests
{
    [GeneratedRegex(@"(?<![\w.-])code\[b-")]
    private static partial Regex ScopedCode();

    [Fact]
    public void No_generated_selector_reaches_a_code_element_through_the_scope_attribute()
    {
        string[] sheets =
        [
            "Components/Cx/Note.razor.css",
            "Components/Cx/Fact.razor.css",
            "Components/Cx/KeyValue.razor.css",
        ];

        foreach (var sheet in sheets)
        {
            var path = Path.Combine(Project.Folder, sheet);

            Assert.True(File.Exists(path), $"{path} was not generated. Sass compiles it on build.");
            Assert.DoesNotMatch(ScopedCode(), File.ReadAllText(path));
        }
    }

    [Fact]
    public void The_bundle_puts_the_attribute_on_the_ancestor_of_a_caller_written_code_element()
    {
        Assert.True(
            File.Exists(Project.Bundle),
            $"{Project.Bundle} was not bundled. The Razor SDK writes it on build.");

        var css = File.ReadAllText(Project.Bundle);

        Assert.DoesNotMatch(ScopedCode(), css);

        Assert.Matches(@"\.cx-note\[b-[a-z0-9]{10}\]\s+code", css);
        Assert.Matches(@"td\[b-[a-z0-9]{10}\]\s+code", css);
        Assert.Matches(@"li\[b-[a-z0-9]{10}\]\s+code", css);
    }

    [Fact]
    public void The_row_chrome_is_scoped_to_the_table_component_that_draws_it()
    {
        Assert.True(
            File.Exists(Project.Bundle),
            $"{Project.Bundle} was not bundled. The Razor SDK writes it on build.");

        var css = File.ReadAllText(Project.Bundle);
        var table = Scope(css, @"table\.cx-rows\[b-([a-z0-9]{10})\]");

        Assert.Equal(table, Scope(css, @"\.cx-table-scroll\[b-([a-z0-9]{10})\]"));
        Assert.Matches($@"table\.cx-rows thead th\[b-{table}\]", css);
        Assert.Matches($@"table\.cx-rows tbody td\[b-{table}\]", css);
        Assert.Matches($@"table\.cx-rows \.cx-group-head td\[b-{table}\]", css);
        Assert.Matches($@"table\.cx-rows \.cx-unit\[b-{table}\]", css);

        var browse = Scope(css, @"\.cx-tab\[b-([a-z0-9]{10})\]");

        Assert.NotEqual(table, browse);
        Assert.DoesNotMatch($@"table\.cx-rows (thead|tbody)[^{{]*\[b-{browse}\]", css);
    }

    [Fact]
    public void Owners_drawn_side_by_side_are_held_apart_by_a_rule_of_their_own()
    {
        var css = File.ReadAllText(Project.Bundle);
        var owner = Scope(css, @"table\.cx-rows \.cx-owner\[b-([a-z0-9]{10})\]");

        Assert.NotEqual(Scope(css, @"table\.cx-rows\[b-([a-z0-9]{10})\]"), owner);

        var rule = Regex.Match(css, $@"table\.cx-rows \.cx-owner\[b-{owner}\]\s*{{(?<body>[^}}]*)}}");

        Assert.True(rule.Success, "The bundle carries no body for the owner rule.");
        Assert.Matches(@"margin:[^;]*\s8px", rule.Groups["body"].Value);
    }

    private static string Scope(string css, string pattern)
    {
        var found = Regex.Match(css, pattern);

        Assert.True(found.Success, $"The bundle carries no selector matching {pattern}.");

        return found.Groups[1].Value;
    }

    [Fact]
    public void Nothing_the_tooltip_draws_with_reaches_the_page_bundle()
    {
        Assert.True(
            File.Exists(Project.Bundle),
            $"{Project.Bundle} was not bundled. The Razor SDK writes it on build.");

        Assert.DoesNotContain(
            TooltipView.Root, File.ReadAllText(Project.Bundle), StringComparison.Ordinal);
    }
}
