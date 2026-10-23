using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class DungeonCard(ICodexViewService ui, TooltipProse prose) : ProseCard<DungeonDocument>(ui, prose);
