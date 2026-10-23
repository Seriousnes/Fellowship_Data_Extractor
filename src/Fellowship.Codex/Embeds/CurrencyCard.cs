using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class CurrencyCard(ICodexViewService ui, TooltipProse prose) : ProseCard<CurrencyDocument>(ui, prose);
