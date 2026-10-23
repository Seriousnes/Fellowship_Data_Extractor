using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class SearchResults
{
    public static SearchResult From(EntityDocument document, ICodexViewService view)
    {
        var type = EntityTypes.TypeOf(document);
        var item = document as ItemDocument;
        var tip = view.Tip(document);

        return new SearchResult(
            EntityTypes.Slug(type),
            PublicTypes.Label(type),
            document.Id,
            PublicTypes.NameOf(document),
            view.Icon(DocumentFields.Text(document, nameof(ItemDocument.Icon))),
            view.RarityName(document),
            view.Href(document),
            tip.Address,
            tip.Body,
            item?.Slot,
            item?.ArmorType?.ToString(),
            item?.Heroes?.Select(hero => hero.ToString()).ToArray());
    }
}
