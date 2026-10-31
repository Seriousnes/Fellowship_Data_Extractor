using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class ItemCard(ICodexViewService ui, ITooltipViewService tips, CardMarkup markup) : EntityCard<ItemDocument>(ui)
{
    public const string ItemLevel = "Item Level";

    private const string Tempered = "Tempered";

    protected override string? Tier(ItemDocument entity, TooltipQuery query, RarityEntry? rarity) =>
        tips.Rarity(entity, query) is { } resolved ? tips.RarityName(entity, resolved) : base.Tier(entity, query, rarity);

    protected override string Noun(ItemDocument entity) => entity.Slot ?? base.Noun(entity);

    protected override IEnumerable<string> Kind(ItemDocument entity, TooltipQuery query)
    {
        var level = tips.Level(entity, query);

        if (level is { } power) yield return $"{ItemLevel} {CardText.Number(power)}";

        if (tips.Temper(entity, query, tips.Rarity(entity, query), level) is { } temper)
        {
            yield return $"{Tempered} {temper}";
        }
    }

    protected override Task<IReadOnlyList<CardLine>> Lines(ItemDocument entity, TooltipQuery query)
    {
        var hero = Ui.Store.Hero(query.Hero);
        var lines = new List<CardLine>();

        var rolls = tips.Rolls(entity, query, tips.Rarity(entity, query));

        if (rolls.Count > 0)
        {
            lines.Add(new CardLine(null, string.Join(" ", rolls.Select(line =>
                CardText.Unbroken($"{TooltipView.Magnitude(line)} {StatNames.Short(line.Stat, line.DisplayName)}")))));
        }

        if (tips.Granted(entity) is { } granted && PublicTypes.DisplayNameOf(granted) is { } name)
        {
            lines.AddRange(markup.Lines(Prose(granted, query, hero) ?? string.Empty, name));
        }

        if (Prose(entity, query, hero) is { } description) lines.AddRange(markup.Lines(description));

        return Task.FromResult<IReadOnlyList<CardLine>>(lines);
    }

    private string? Prose(EntityDocument document, TooltipQuery query, HeroEntry? hero) =>
        DocumentFields.Text(document, nameof(AbilityDocument.Description)) is { } text
            ? tips.Text(text, CodexView.PlaceholdersOf(document), query, hero, FellowshipCodex.Schools(document)).Value
            : null;
}
