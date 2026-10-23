using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class EnchantCard(ICodexViewService ui, TooltipProse prose) : AugmentationRankCard<EnchantDocument>(ui, prose);
