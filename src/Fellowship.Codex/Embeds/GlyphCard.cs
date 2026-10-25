using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class GlyphCard(ICodexViewService ui, TooltipProse prose) : ProseCard<GlyphDocument>(ui, prose)
{
    private const float PlateShare = 0.8f;

    private const float RuneShare = 0.4f;

    protected override IReadOnlyList<CardLayer> Icon(GlyphDocument entity, TooltipQuery query)
    {
        var state = ChromeAssetService.State<GlyphState>(query.State);
        var plate = new CardLayer(Ui.Chrome.GlyphPlate(state), PlateShare, true);

        return Ui.Chrome.GlyphRune(entity, state) is { } rune
            ? [plate, new CardLayer(rune, RuneShare, true)]
            : [plate];
    }
}
