using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace FellowshipExtractor.Api;

public sealed record TextStyle(string Color, string Weight, double Size)
{
    public bool Italic => Weight.EndsWith("Italic", StringComparison.OrdinalIgnoreCase);

    public string CssWeight => Weight.Split('-')[0];

    public static string CssClass(string name) =>
        "rt-" + name.Replace("rt.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('.', '-')
            .ToLowerInvariant();
}

public sealed class RichTextRenderer(IReadOnlyDictionary<string, TextStyle> styles)
{
    private const string CloseTag = "</>";

    public IReadOnlyDictionary<string, TextStyle> Styles => styles;

    public ICollection<string> Unknown => unknown.Keys;

    private readonly ConcurrentDictionary<string, byte> unknown = new(StringComparer.OrdinalIgnoreCase);

    public string Render(string text, IReadOnlyDictionary<string, Substitution>? substitutions = null) =>
        Scan(text, substitutions, markup: true);

    public string Plain(string text, IReadOnlyDictionary<string, Substitution>? substitutions = null) =>
        Scan(text, substitutions, markup: false);

    private string Scan(string text, IReadOnlyDictionary<string, Substitution>? substitutions, bool markup)
    {
        var html = new StringBuilder(text.Length + 64);
        var at = 0;
        string? open = null;

        void Close()
        {
            if (open is null) return;

            if (markup) html.Append("</span>");
            open = null;
        }

        while (at < text.Length)
        {
            var next = text.IndexOf('<', at);
            if (next < 0)
            {
                Write(html, text[at..], substitutions, markup);
                break;
            }

            Write(html, text[at..next], substitutions, markup);

            if (string.CompareOrdinal(text, next, CloseTag, 0, CloseTag.Length) == 0)
            {
                Close();
                at = next + CloseTag.Length;
                continue;
            }

            var end = text.IndexOf('>', next);
            if (end < 0)
            {
                Write(html, text[next..], substitutions, markup);
                break;
            }

            var name = text[(next + 1)..end];

            Close();

            if (styles.ContainsKey(name))
            {
                if (markup) html.Append("<span class=\"rt ").Append(TextStyle.CssClass(name)).Append("\">");
            }
            else
            {
                unknown.TryAdd(name, 0);
                if (markup) html.Append("<span class=\"rt rt-unstyled\">");
            }

            open = name;
            at = end + 1;
        }

        Close();

        return html.ToString();
    }

    private static void Write(
        StringBuilder html, string text, IReadOnlyDictionary<string, Substitution>? substitutions, bool markup)
    {
        if (text.Length == 0) return;

        var at = 0;
        while (at < text.Length)
        {
            var open = text.IndexOf('{', at);
            var close = open < 0 ? -1 : text.IndexOf('}', open);

            if (open < 0 || close < 0)
            {
                Text(html, text[at..], markup);
                return;
            }

            Text(html, text[at..open], markup);

            var value = substitutions is not null && substitutions.TryGetValue(text[(open + 1)..close], out var found)
                ? found
                : Substitution.Missing;

            if (!markup)
            {
                html.Append(value.Text);
            }
            else if (value.Resolved)
            {
                html.Append("<span class=\"sub\"");
                Titled(html, value.Title);
                html.Append('>').Append(value.Html ?? WebUtility.HtmlEncode(value.Text)).Append("</span>");
            }
            else
            {
                html.Append("<span class=\"tok\"");
                Titled(html, value.Title);
                html.Append('>').Append(WebUtility.HtmlEncode(value.Text)).Append("</span>");
            }

            at = close + 1;
        }
    }

    private static void Text(StringBuilder html, string text, bool markup) =>
        html.Append(markup ? WebUtility.HtmlEncode(text) : text);

    private static void Titled(StringBuilder html, string? title)
    {
        if (title is not { Length: > 0 }) return;

        html.Append(" title=\"").Append(WebUtility.HtmlEncode(title)).Append('"');
    }

    public string Css(string scope)
    {
        var css = new StringBuilder();

        var byClass = styles
            .GroupBy(s => TextStyle.CssClass(s.Key), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byClass)
        {
            var style = group.First().Value;

            css.Append('.').Append(scope).Append(" .").Append(group.Key)
                .Append("{color:#").Append(style.Color)
                .Append(";font-weight:").Append(style.CssWeight);

            if (style.Italic) css.Append(";font-style:italic");

            css.Append('}');
        }

        return css.ToString();
    }
}

public readonly record struct Substitution(
    string Text, string? Title = null, string? Html = null, bool Resolved = true)
{
    public static Substitution Missing { get; } = new(
        "[not in the build]", "nothing in the build resolves this substitution", Resolved: false);
}
