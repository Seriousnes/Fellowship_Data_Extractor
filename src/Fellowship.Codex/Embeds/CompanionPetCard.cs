using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class CompanionPetCard(ICodexViewService ui, TooltipProse prose) : ProseCard<CompanionPetDocument>(ui, prose);
