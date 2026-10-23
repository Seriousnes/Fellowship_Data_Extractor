using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public abstract class ProseCard<TDocument>(ICodexViewService ui, TooltipProse prose) : EntityCard<TDocument>(ui)
    where TDocument : EntityDocument
{
    protected TooltipProse Prose => prose;

    protected override Task<IReadOnlyList<CardLine>> Lines(TDocument entity, TooltipQuery query) =>
        prose.Lines(entity, query);
}
