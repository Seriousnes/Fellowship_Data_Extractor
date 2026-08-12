using System.Text.RegularExpressions;

namespace Fellowship.Codex.Tests;

internal static partial class Scoped
{
    [GeneratedRegex(@"\sb-[a-z0-9]{10}(?:="""")?(?=[\s>])")]
    private static partial Regex Attribute();

    public static string Bare(this string markup) => Attribute().Replace(markup, string.Empty);
}
