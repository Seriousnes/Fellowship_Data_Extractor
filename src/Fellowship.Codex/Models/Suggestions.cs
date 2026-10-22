using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class SearchResults
{
    public static SearchResult From(EntityDocument document, ICodexViewService view, string? text = null)
    {
        var type = EntityTypes.TypeOf(document);
        var item = document as ItemDocument;
        var tip = view.Tip(document);

        return new SearchResult(
            EntityTypes.Slug(type),
            PublicTypes.Label(type),
            document.Id,
            Matched(document, text) ?? PublicTypes.NameOf(document),
            view.Icon(DocumentFields.Text(document, nameof(ItemDocument.Icon))),
            view.RarityName(document),
            view.Href(document),
            tip.Address,
            tip.Body,
            item?.Slot,
            item?.ArmorType?.ToString(),
            item?.Heroes?.Select(hero => hero.ToString()).ToArray());
    }

    private static string? Matched(EntityDocument document, string? text)
    {
        if (text is null || TooltipView.NameByHero(document) is not { } names) return null;

        var typed = SearchIndexService.Words(text).ToArray();

        return names.Values
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault(name =>
            {
                var words = SearchIndexService.Words(name).ToArray();

                return typed.All(word => words.Any(part => part.StartsWith(word, StringComparison.Ordinal)));
            });
    }
}
