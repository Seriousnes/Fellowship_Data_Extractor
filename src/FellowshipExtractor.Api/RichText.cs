using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace FellowshipExtractor.Api;

/// <summary>
/// One row of the game's rich text style set: the colour, weight and size the game draws a tagged
/// run in. Weight arrives as the typeface name the font asset declares, which is a number for the
/// upright faces and a number with an Italic suffix for the slanted ones.
/// </summary>
public sealed record TextStyle(string Color, string Weight, double Size)
{
    public bool Italic => Weight.EndsWith("Italic", StringComparison.OrdinalIgnoreCase);

    /// <summary>The numeric half of the typeface name, which is what CSS font-weight takes.</summary>
    public string CssWeight => Weight.Split('-')[0];

    /// <summary>
    /// The CSS class a run in a named style is drawn with. The name is the tag a description writes
    /// and is the key the style is filed under, so it is passed in rather than carried on the row.
    /// </summary>
    public static string CssClass(string name) =>
        "rt-" + name.Replace("rt.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('.', '-')
            .ToLowerInvariant();
}

/// <summary>
/// Turns the game's tagged description text into styled HTML.
///
/// The game writes a styled run as an opening tag naming a style set row and a bare closing tag:
/// "grants you &lt;rt.warning&gt;+3%&lt;/&gt; haste". The closing tag names nothing, so the scanner
/// tracks the run it is inside rather than matching names, and runs do not nest anywhere in the
/// shipped corpus.
///
/// Every tag the corpus uses is a row of the shipped style table, so a colour is read rather than
/// chosen. A tag naming a row the table does not define is drawn in the body colour and reported,
/// because a silently unstyled run reads as ordinary prose and hides the fact that a style went
/// missing from the build.
/// </summary>
public sealed class RichTextRenderer(IReadOnlyDictionary<string, TextStyle> styles)
{
    private const string CloseTag = "</>";

    public IReadOnlyDictionary<string, TextStyle> Styles => styles;

    /// <summary>
    /// The tags the corpus used that the style table does not define, filled as text is rendered.
    /// Concurrent because one renderer serves every request at once, and a plain set written from
    /// several threads corrupts rather than merely losing an entry.
    /// </summary>
    public ICollection<string> Unknown => unknown.Keys;

    private readonly ConcurrentDictionary<string, byte> unknown = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Renders one description. Substitution runs on the text between tags rather than on the whole
    /// string, so a token is never matched across a tag boundary, and the substituted value is
    /// escaped by the same path as the text around it.
    /// </summary>
    public string Render(string text, IReadOnlyDictionary<string, Substitution>? substitutions = null)
    {
        var html = new StringBuilder(text.Length + 64);
        var at = 0;
        string? open = null;

        while (at < text.Length)
        {
            var next = text.IndexOf('<', at);
            if (next < 0)
            {
                Write(html, text[at..], substitutions);
                break;
            }

            Write(html, text[at..next], substitutions);

            if (string.CompareOrdinal(text, next, CloseTag, 0, CloseTag.Length) == 0)
            {
                if (open is not null)
                {
                    html.Append("</span>");
                    open = null;
                }

                at = next + CloseTag.Length;
                continue;
            }

            var end = text.IndexOf('>', next);
            if (end < 0)
            {
                // An unterminated angle bracket is literal text, not a tag.
                Write(html, text[next..], substitutions);
                break;
            }

            var name = text[(next + 1)..end];

            // The game does not nest runs, so an opening tag inside one closes it.
            if (open is not null) html.Append("</span>");

            if (styles.ContainsKey(name))
            {
                html.Append("<span class=\"rt ").Append(TextStyle.CssClass(name)).Append("\">");
            }
            else
            {
                unknown.TryAdd(name, 0);
                html.Append("<span class=\"rt rt-unstyled\">");
            }

            open = name;
            at = end + 1;
        }

        if (open is not null) html.Append("</span>");

        return html.ToString();
    }

    /// <summary>
    /// Escapes a run of plain text, replacing any {Token} in it with the value the caller resolved.
    /// A token with no value is emitted as it stands so a reader sees which placeholder went
    /// unanswered rather than a gap in the sentence.
    /// </summary>
    private void Write(StringBuilder html, string text, IReadOnlyDictionary<string, Substitution>? substitutions)
    {
        if (text.Length == 0) return;

        var at = 0;
        while (at < text.Length)
        {
            var open = text.IndexOf('{', at);
            var close = open < 0 ? -1 : text.IndexOf('}', open);

            if (open < 0 || close < 0)
            {
                html.Append(WebUtility.HtmlEncode(text[at..]));
                return;
            }

            html.Append(WebUtility.HtmlEncode(text[at..open]));

            var name = text[(open + 1)..close];
            if (substitutions is not null && substitutions.TryGetValue(name, out var value))
            {
                html.Append("<span class=\"sub\"");
                if (value.Title is { Length: > 0 } title)
                {
                    html.Append(" title=\"").Append(WebUtility.HtmlEncode(title)).Append('"');
                }

                html.Append('>')
                    .Append(value.Html ?? WebUtility.HtmlEncode(value.Text))
                    .Append("</span>");
            }
            else
            {
                html.Append("<span class=\"tok\">")
                    .Append(WebUtility.HtmlEncode(text[open..(close + 1)]))
                    .Append("</span>");
            }

            at = close + 1;
        }
    }

    /// <summary>
    /// The style rules for every row of the shipped table, for the payload's style block. The scope
    /// is the class the fragment's root carries, and every selector is written under it: a payload
    /// is dropped into somebody else's page, and a bare .rt-warning would repaint any run of that
    /// class the host already had.
    /// </summary>
    public string Css(string scope)
    {
        var css = new StringBuilder();

        // Several tags fold to one class, because the build files the same style under more than
        // one spelling: Absorb beside absorb, MeikoAbility3 beside meikoability3.
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

/// <summary>
/// What a placeholder printed as, and the workings behind it. The title is what the game cannot
/// show and a reader of a web tooltip can: the coefficient and attribute the number came from, so a
/// printed 526 can be read back as 584.82% of Intellect without leaving the tooltip.
///
/// Html is set where the value is already marked up, which is the case whenever a number went
/// through one of the shipped print formats: those carry the game's own tags, so the format has to
/// be rendered rather than escaped. Text stays the plain reading, for the title and for anywhere a
/// tooltip is read without markup.
/// </summary>
public readonly record struct Substitution(string Text, string? Title = null, string? Html = null);
