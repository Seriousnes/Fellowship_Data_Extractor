using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class RelicPassiveCard(ICodexViewService ui, TooltipProse prose) : ProseCard<RelicPassiveDocument>(ui, prose);
