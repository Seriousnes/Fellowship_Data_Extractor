using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Models;

public sealed record SearchResult(
    string Type,
    string TypeLabel,
    int Id,
    string Name,
    string? IconUrl,
    string? Rarity,
    string Href,
    string Tooltip,
    string? Slot,
    string? ArmorType,
    IReadOnlyList<string>? Heroes)
{
    public static SearchResult From(EntityDocument document, ICodexViewService view)
    {
        var type = EntityTypes.TypeOf(document);
        var item = document as ItemDocument;

        return new(
            EntityTypes.Slug(type),
            PublicTypes.Label(type),
            document.Id,
            PublicTypes.NameOf(document),
            view.Icon(DocumentFields.Text(document, nameof(ItemDocument.Icon))),
            view.RarityName(document),
            view.Href(document),
            view.Tip(document),
            item?.Slot,
            item?.ArmorType?.ToString(),
            item?.Heroes?.Select(hero => hero.ToString()).ToArray());
    }
}

public sealed record SearchGroup(string Type, string Label, int Count);

public sealed record SearchResponse(
    string Query, int Total, IReadOnlyList<SearchGroup> Groups, IReadOnlyList<SearchResult> Results)
{
    public static readonly SearchResponse Empty = new(string.Empty, 0, [], []);
}

public sealed record EntitiesResponse(int Total, int Page, int Pages, IReadOnlyList<SearchResult> Rows);
