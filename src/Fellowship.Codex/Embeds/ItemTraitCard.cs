using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class ItemTraitCard(ICodexViewService ui, TooltipProse prose) : AugmentationRankCard<ItemTraitDocument>(ui, prose);
