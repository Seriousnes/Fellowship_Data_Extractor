using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class WeaponPowerCard(ICodexViewService ui, TooltipProse prose) : ProseCard<WeaponPowerDocument>(ui, prose);
