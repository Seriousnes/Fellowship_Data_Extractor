using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class LegendaryPowerCard(ICodexViewService ui, TooltipProse prose) : ProseCard<LegendaryPowerDocument>(ui, prose);
