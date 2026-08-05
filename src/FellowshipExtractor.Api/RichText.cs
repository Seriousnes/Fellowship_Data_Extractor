using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace FellowshipExtractor.Api;

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
    /// Renders one description as styled HTML. Substitution runs on the text between tags rather
    /// than on the whole string, so a token is never matched across a tag boundary, and the
    /// substituted value is escaped by the same path as the text around it.
    /// </summary>
    public string Render(string text, IReadOnlyDictionary<string, Substitution>? substitutions = null) =>
        Scan(text, substitutions, markup: true);

    /// <summary>
    /// The same description with the same numbers in it and none of the game's markup: the run tags
    /// dropped and each substitution written as its plain reading.
    ///
    /// This is what a caller reading the catalogue as data is given, and it exists because handing
    /// the shipped string over instead published two things the styled reading resolves away. Every
    /// {Token} in the corpus is a key of the kind <c>StoneShieldDmg</c>, which is a spelling and not
    /// a label; and every run tag is the name of a row of the shipped style table, <c>rt.mana</c>
    /// beside <c>rt.warning</c>. A page drew "862.4% of Agility" where the record said
    /// "{AgiCoefficient}" wrapped in tags, so the two surfaces said different things about one row.
    /// </summary>
    public string Plain(string text, IReadOnlyDictionary<string, Substitution>? substitutions = null) =>
        Scan(text, substitutions, markup: false);

    /// <summary>
    /// The one walk of a description, in either reading. The scanner is shared rather than written
    /// twice because the two readings have to spend the same tokens at the same points: a second
    /// scanner is a second answer to where a run begins.
    /// </summary>
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
                // An unterminated angle bracket is literal text, not a tag.
                Write(html, text[next..], substitutions, markup);
                break;
            }

            var name = text[(next + 1)..end];

            // The game does not nest runs, so an opening tag inside one closes it.
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

    /// <summary>
    /// Writes a run of plain text, replacing any {Token} in it with the reading the caller resolved.
    ///
    /// A token nobody resolved is written as the sentence saying so rather than as its own key.
    /// The key is the build's spelling of the number and says nothing a reader can use - 221 of the
    /// 237 distinct keys in the export read like StrCoeff50 and HemoStrikeTickDmg - so a sentence
    /// missing a number now says that it is missing rather than naming the row it would have come
    /// from. Measured over the corpus this is a guard rather than a path: every one of the {Token}s
    /// the export's descriptions, details, gem overflows and nested rows spend has an entry in the
    /// row carrying it, so a caller passing that row's tokens resolves each of them to something.
    /// </summary>
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
                // The marked style a token used to be drawn in, kept for the reading that replaces
                // it: an unresolved substitution is an editorial insertion rather than prose, and
                // the sentence it stands in reads as one only if it is drawn as one.
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
///
/// Resolved says whether a number was found at all. A token the build settles nowhere still gets one
/// of these, carrying the sentence that says so, because the alternative is the sentence keeping the
/// token's own key: 47 of the 373 substitutions in the export are in that state, on 41 rows, and 40
/// of them are worked out by native code the extractor cannot read. The bracket is part of the
/// reading rather than styling on it, so the marker survives into the plain text a caller reading
/// the catalogue as data is given.
/// </summary>
public readonly record struct Substitution(
    string Text, string? Title = null, string? Html = null, bool Resolved = true)
{
    /// <summary>
    /// What stands in for a token the caller resolved nothing for, which in the shipped corpus is
    /// only a token whose row was not passed alongside its text.
    /// </summary>
    public static Substitution Missing { get; } = new(
        "[not in the build]", "nothing in the build resolves this substitution", Resolved: false);
}
