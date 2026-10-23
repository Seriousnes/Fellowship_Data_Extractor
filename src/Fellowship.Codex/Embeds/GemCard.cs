using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class GemCard(ICodexViewService ui, TooltipProse prose) : ProseCard<GemDocument>(ui, prose)
{
    protected override string? Tier(GemDocument entity, TooltipQuery query, RarityEntry? rarity) =>
        rarity is null ? Ui.Rarity(entity) : rarity.GemDisplayName;
}
