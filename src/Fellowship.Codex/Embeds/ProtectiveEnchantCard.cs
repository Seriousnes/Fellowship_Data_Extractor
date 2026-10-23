using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class ProtectiveEnchantCard(ICodexViewService ui, TooltipProse prose) : AugmentationRankCard<ProtectiveEnchantDocument>(ui, prose);
