using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public abstract class AugmentationRankCard<TDocument>(ICodexViewService ui, TooltipProse prose)
    : ProseCard<TDocument>(ui, prose)
    where TDocument : AugmentationDocument
{
    protected override IEnumerable<string> Kind(TDocument entity, TooltipQuery query)
    {
        if (query.ItemLevel is { } level) yield return $"{ItemCard.ItemLevel} {CardText.Number(level)}";

        if (AugmentationRanks.Ranked(Ui.Store, entity))
        {
            yield return $"{AugmentationRanks.Text(Ui.Store.Settings).EquippedRank} " +
                CardText.Number(AugmentationRanks.Rank(Ui.Store, query, entity));
        }
    }
}
