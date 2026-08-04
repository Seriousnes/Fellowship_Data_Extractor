using System.Text.RegularExpressions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The rendered markup without the scope attributes Blazor's CSS isolation puts on every element a
/// component with a stylesheet emits. Only the assertions that compare an exact string need it:
/// bUnit's semantic comparison already ignores the attribute, and an assertion that reads one
/// deliberately does it through Find and GetAttribute, which are unaffected.
/// </summary>
internal static partial class Scoped
{
    /// <summary>
    /// b- and ten base-36 characters, followed by a space or the tag's end.
    ///
    /// The optional ="" is what an assertion reading through Find(...).OuterHtml needs: the
    /// renderer writes the attribute valueless, and AngleSharp serialises a valueless attribute
    /// with an empty value. cut.Markup is the renderer's own string and carries neither.
    /// </summary>
    [GeneratedRegex(@"\sb-[a-z0-9]{10}(?:="""")?(?=[\s>])")]
    private static partial Regex Attribute();

    public static string Bare(this string markup) => Attribute().Replace(markup, string.Empty);
}
