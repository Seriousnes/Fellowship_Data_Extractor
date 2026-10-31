using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class DungeonAffixCard(ICodexViewService ui, TooltipProse prose) : ProseCard<DungeonAffixDocument>(ui, prose);
