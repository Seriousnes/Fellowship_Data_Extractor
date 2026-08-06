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

        var project = Project();

        foreach (var sheet in sheets)
        {
            var path = Path.Combine(project, sheet);

            Assert.True(File.Exists(path), $"{path} was not generated. Sass compiles it on build.");
            Assert.DoesNotMatch(ScopedCode(), File.ReadAllText(path));
        }
    }

    [Fact]
    public void The_bundle_puts_the_attribute_on_the_ancestor_of_a_caller_written_code_element()
    {
        var bundle = Path.Combine(
            Project(), "obj", "Debug", "net10.0", "scopedcss", "bundle",
            "FellowshipExtractor.Api.styles.css");

        Assert.True(File.Exists(bundle), $"{bundle} was not bundled. The Razor SDK writes it on build.");

        var css = File.ReadAllText(bundle);

        Assert.DoesNotMatch(ScopedCode(), css);

        Assert.Matches(@"\.cx-note\[b-[a-z0-9]{10}\]\s+code", css);
        Assert.Matches(@"td\[b-[a-z0-9]{10}\]\s+code", css);
        Assert.Matches(@"li\[b-[a-z0-9]{10}\]\s+code", css);
    }

    private static string Project()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var project = Path.Combine(here.FullName, "src", "FellowshipExtractor.Api");

            if (File.Exists(Path.Combine(project, "FellowshipExtractor.Api.csproj"))) return project;
        }

        throw new InvalidOperationException(
            "No src/FellowshipExtractor.Api above the test binary. The generated .razor.css files sit " +
            "beside their .razor.scss sources in that folder.");
    }
}
