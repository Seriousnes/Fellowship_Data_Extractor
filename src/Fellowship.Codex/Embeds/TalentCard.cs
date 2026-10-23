using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class TalentCard(ICodexViewService ui, TooltipProse prose) : ProseCard<TalentDocument>(ui, prose);
