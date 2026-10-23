using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class SigilCard(ICodexViewService ui, TooltipProse prose) : AugmentationRankCard<SigilDocument>(ui, prose);
