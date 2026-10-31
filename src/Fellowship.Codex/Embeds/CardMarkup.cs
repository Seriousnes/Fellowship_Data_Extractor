using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed partial class CardMarkup(IEntityStoreService store, IDesignTokenService tokens)
{
    private const int BoldWeight = 600;

    private readonly IReadOnlyDictionary<string, TextStyle> byClass = store.Settings.TextStyles
        .GroupBy(style => TextStyle.CssClass(style.Key), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);

    // Each line break in the text starts a line, and a blank line between two lines is a paragraph gap.
    public IReadOnlyList<CardLine> Lines(string html, string? lead = null)
    {
        var lines = new List<CardLine>();
        var runs = new List<CardRun>();
        var outer = new Stack<CardRun>();
        var current = new CardRun(string.Empty);
        var breaks = 0;
        var continues = false;

        void Close()
        {
            if (Trimmed(runs) is { Count: > 0 } trimmed)
            {
                lines.Add(new CardLine(lines.Count == 0 ? lead : null, trimmed, continues));
            }

            runs.Clear();
        }

        void Append(string text)
        {
            var parts = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

            for (var index = 0; index < parts.Length; index++)
            {
                if (index > 0) breaks++;
                if (parts[index].Length == 0) continue;

                if (breaks > 0 && !string.IsNullOrWhiteSpace(parts[index]))
                {
                    if (runs.Any(run => !string.IsNullOrWhiteSpace(run.Text)))
                    {
                        Close();
                        continues = breaks == 1;
                    }

                    breaks = 0;
                }

                runs.Add(current with { Text = parts[index] });
            }
        }

        foreach (Match match in Markup().Matches(html))
        {
            if (match.Groups["text"].Success)
            {
                Append(WebUtility.HtmlDecode(match.Groups["text"].Value));
                continue;
            }

            if (match.Groups["close"].Success)
            {
                if (outer.Count > 0) current = outer.Pop();
                continue;
            }

            if (match.Groups["name"].Value.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                Append("\n");
                continue;
            }

            if (match.Groups["self"].Success) continue;

            outer.Push(current);
            current = Styled(current, match.Groups["class"].Value);
        }

        Close();

        if (lines.Count == 0 && lead is not null) lines.Add(new CardLine(lead, []));

        return lines;
    }

    private CardRun Styled(CardRun outer, string classes)
    {
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (byClass.TryGetValue(name, out var style))
            {
                return outer with
                {
                    Colour = "#" + style.Color,
                    Bold = int.TryParse(style.CssWeight, CultureInfo.InvariantCulture, out var weight) && weight >= BoldWeight,
                    Italic = style.Italic,
                };
            }

            if (name == RichTextService.SubstitutionClass) return outer with { Bold = true };

            if (name == RichTextService.UnresolvedClass)
            {
                return outer with { Colour = tokens.Color("core.text-disabled"), Bold = false, Italic = true };
            }
        }

        return outer;
    }

    private static List<CardRun> Trimmed(List<CardRun> runs)
    {
        var trimmed = runs.ToList();

        while (trimmed.Count > 0 && trimmed[0].Text.TrimStart() is var start && start.Length == 0) trimmed.RemoveAt(0);
        while (trimmed.Count > 0 && trimmed[^1].Text.TrimEnd() is var end && end.Length == 0) trimmed.RemoveAt(trimmed.Count - 1);

        if (trimmed.Count == 0) return trimmed;

        trimmed[0] = trimmed[0] with { Text = trimmed[0].Text.TrimStart() };
        trimmed[^1] = trimmed[^1] with { Text = trimmed[^1].Text.TrimEnd() };

        return trimmed;
    }

    [GeneratedRegex(
        @"<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9]*)(?:[^>]*?\bclass=""(?<class>[^""]*)"")?[^>]*?(?<self>/)?>|(?<text>[^<]+)")]
    private static partial Regex Markup();
}
