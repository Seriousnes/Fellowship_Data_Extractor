using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class AspectCard(ICodexViewService ui, TooltipProse prose) : AugmentationRankCard<AspectDocument>(ui, prose);
