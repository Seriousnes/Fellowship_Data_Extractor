using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class EffectCard(ICodexViewService ui, TooltipProse prose) : ProseCard<EffectDocument>(ui, prose);
