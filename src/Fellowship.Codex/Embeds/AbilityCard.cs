using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class AbilityCard(ICodexViewService ui, TooltipProse prose) : ProseCard<AbilityDocument>(ui, prose);
