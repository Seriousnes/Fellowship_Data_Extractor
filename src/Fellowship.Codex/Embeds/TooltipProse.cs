using System.Text.RegularExpressions;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed partial class TooltipProse(IFragmentService fragments, IEntityStoreService store, CardMarkup markup)
{
    public async Task<IReadOnlyList<CardLine>> Lines(EntityDocument document, TooltipQuery query)
    {
        var tooltip = await fragments.Render<Components.Tooltip.Tooltip>(new()
        {
            [nameof(Components.Tooltip.Tooltip.Entity)] = document,
            [nameof(Components.Tooltip.Tooltip.Query)] = query,
            [nameof(Components.Tooltip.Tooltip.Hero)] = store.Hero(query.Hero),
            [nameof(Components.Tooltip.Tooltip.Origin)] = string.Empty,
        });

        var lines = new List<CardLine>();
        var facts = new List<string>();

        foreach (Match match in Block().Matches(Styles().Replace(tooltip, string.Empty)))
        {
            if (match.Groups["key"].Success)
            {
                facts.Add(CardText.Unbroken(
                    $"{CardText.Plain(match.Groups["key"].Value, " ")} {CardText.Plain(match.Groups["value"].Value, " ")}"));
                continue;
            }

            lines.AddRange(markup.Lines(match.Groups["prose"].Value));
        }

        if (lines.Count == 0 && facts.Count > 0) lines.Add(new CardLine(null, string.Join(", ", facts)));

        return lines;
    }

    [GeneratedRegex(@"<style\b[^>]*>.*?</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Styles();

    [GeneratedRegex(
        @"<p class=""(?:desc|prose)\b[^""]*""[^>]*>(?<prose>.*?)</p>|<span class=""k""[^>]*>(?<key>.*?)</span>\s*<span class=""v""[^>]*>(?<value>.*?)</span>",
        RegexOptions.Singleline)]
    private static partial Regex Block();
}
