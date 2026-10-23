using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class MountCard(ICodexViewService ui, TooltipProse prose) : ProseCard<MountDocument>(ui, prose);
