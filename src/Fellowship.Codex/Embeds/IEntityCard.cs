using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Embeds;

public interface IEntityCard
{
    EntityType Type { get; }

    Task<CardModel> Fill(EntityDocument document, TooltipQuery query);
}
