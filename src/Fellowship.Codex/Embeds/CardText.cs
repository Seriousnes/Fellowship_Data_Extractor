using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Fellowship.Codex.Embeds;

public static partial class CardText
{
    public const string Ellipsis = "…";

    public const char NoBreak = ' ';

    public static string Plain(string html, string between) =>
        string.Join(' ', WebUtility.HtmlDecode(Tags().Replace(html, between))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Unbroken(string text) => text.Replace(' ', NoBreak);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Capitalised(string noun) =>
        noun.Length == 0 ? noun : char.ToUpper(noun[0], CultureInfo.InvariantCulture) + noun[1..];

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();
}
