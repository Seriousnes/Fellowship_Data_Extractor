using Fellowship.SDK.Documents;

using Fellowship.Codex.Embeds;
using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ICardService
{
    int Width { get; }

    int Height(CardModel card);

    Task<CardModel> Card(EntityDocument document, TooltipQuery query);

    Task<byte[]> Render(CardModel card, CancellationToken cancellationToken);
}

public sealed class CardService(IEnumerable<IEntityCard> cards, CardRenderer renderer) : ICardService
{
    private readonly IReadOnlyDictionary<EntityType, IEntityCard> byType = cards.ToDictionary(card => card.Type);

    public int Width => CardRenderer.Width;

    public int Height(CardModel card) => CardRenderer.Height(card);

    public Task<CardModel> Card(EntityDocument document, TooltipQuery query) =>
        byType.TryGetValue(EntityTypes.TypeOf(document), out var card)
            ? card.Fill(document, query)
            : throw new InvalidOperationException(
                $"No card class fills a {PublicTypes.Noun(EntityTypes.TypeOf(document))}, so its page has no Open Graph card.");

    public Task<byte[]> Render(CardModel card, CancellationToken cancellationToken) =>
        renderer.Render(card, cancellationToken);
}
