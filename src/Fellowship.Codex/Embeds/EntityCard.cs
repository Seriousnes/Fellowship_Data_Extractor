using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public abstract class EntityCard<TDocument>(ICodexViewService ui) : IEntityCard
    where TDocument : EntityDocument
{
    private static readonly EntityType Documented =
        Enum.GetValues<EntityType>().Single(type => EntityTypes.Document(type) == typeof(TDocument));

    public EntityType Type => Documented;

    protected ICodexViewService Ui => ui;

    public async Task<CardModel> Fill(EntityDocument document, TooltipQuery query)
    {
        var entity = (TDocument)document;
        var rarity = query.Rarity is { } word ? ui.Store.Rarity(word, entity is GemDocument) : null;

        var named = Noun(entity);

        return new CardModel
        {
            Name = ui.Name(entity),
            Colour = rarity is null
                ? ui.NameColour(entity)
                : CodexView.Readable(ui.Tokens.Rarity(rarity.Name)) ?? ui.NameColour(entity),
            Kind = [Tier(entity, query, rarity) is { } tier ? $"{tier} {named}" : named, .. Kind(entity, query)],
            Lines = await Lines(entity, query),
            Icon = Icon(entity, query),
        };
    }

    protected virtual string? Tier(TDocument entity, TooltipQuery query, RarityEntry? rarity) =>
        rarity is null ? ui.Rarity(entity) : rarity.DisplayName;

    protected virtual string Noun(TDocument entity) => CardText.Capitalised(PublicTypes.Noun(Type));

    protected virtual IEnumerable<string> Kind(TDocument entity, TooltipQuery query) => [];

    protected abstract Task<IReadOnlyList<CardLine>> Lines(TDocument entity, TooltipQuery query);

    protected virtual IReadOnlyList<CardLayer> Icon(TDocument entity, TooltipQuery query) =>
        ui.Icon(DocumentFields.Text(entity, nameof(ItemDocument.Icon))) is { } icon
            ? [new CardLayer(icon, 1, false)]
            : [];
}
