using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class RelicPowerCard(ICodexViewService ui, TooltipProse prose) : ProseCard<RelicPowerDocument>(ui, prose);
