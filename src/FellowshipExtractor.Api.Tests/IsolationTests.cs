using System.Text.RegularExpressions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// Reads the stylesheets the build generated from the .scss sources, which is the only place a
/// dropped ::deep is visible: the rule compiles, bundles and matches nothing, and no rendered
/// markup differs.
/// </summary>
public partial class IsolationTests
{
    /// <summary>
    /// A code element carrying the scope attribute, which is what a rule reaching a caller's markup
    /// compiles to when its ::deep is dropped.
    ///
    /// The lookbehind is what keeps it to the element: .cx-code is a class this design system
    /// genuinely has, on the hero codename line, and a plain substring test reports it as a failure.
    /// </summary>
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

    /// <summary>
    /// The three rules that reach a caller's markup, read out of the bundle the browser is served
    /// rather than out of the per-component sheet. The rewrite from ::deep to an attribute on the
    /// first element of the selector happens in the bundle, so this is where a dropped ::deep would
    /// show as a scoped code element and where a kept one shows as the attribute on the ancestor.
    /// </summary>
    [Fact]
    public void The_bundle_puts_the_attribute_on_the_ancestor_of_a_caller_written_code_element()
    {
        var bundle = Path.Combine(
            Project(), "obj", "Debug", "net10.0", "scopedcss", "bundle",
            "FellowshipExtractor.Api.styles.css");

        Assert.True(File.Exists(bundle), $"{bundle} was not bundled. The Razor SDK writes it on build.");

        var css = File.ReadAllText(bundle);

        Assert.DoesNotMatch(ScopedCode(), css);

        // The three ancestors the attribute does land on, one per ::deep rule in the library.
        Assert.Matches(@"\.cx-note\[b-[a-z0-9]{10}\]\s+code", css);
        Assert.Matches(@"td\[b-[a-z0-9]{10}\]\s+code", css);
        Assert.Matches(@"li\[b-[a-z0-9]{10}\]\s+code", css);
    }

    /// <summary>
    /// The Api project folder, found by walking up from the test binary, so the test reads the
    /// stylesheets the build just wrote rather than a copy.
    /// </summary>
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
