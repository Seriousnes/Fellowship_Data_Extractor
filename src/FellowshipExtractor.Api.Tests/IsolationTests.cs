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
    public void Nothing_the_tooltip_draws_with_reaches_the_page_bundle()
    {
        Assert.True(
            File.Exists(Project.Bundle),
            $"{Project.Bundle} was not bundled. The Razor SDK writes it on build.");

        Assert.DoesNotContain(
            TooltipView.Root, File.ReadAllText(Project.Bundle), StringComparison.Ordinal);
    }
}
