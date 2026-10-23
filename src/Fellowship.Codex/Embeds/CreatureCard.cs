using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Embeds;

public sealed class CreatureCard(ICodexViewService ui, TooltipProse prose) : ProseCard<CreatureDocument>(ui, prose)
{
    private const string KillScore = "Kill Score";

    protected override IEnumerable<string> Kind(CreatureDocument entity, TooltipQuery query)
    {
        if (entity.KillScore is { } score) yield return $"{KillScore} {CardText.Number(score)}";
    }

    protected override async Task<IReadOnlyList<CardLine>> Lines(CreatureDocument entity, TooltipQuery query) =>
        [.. Haunts(entity), .. await Prose.Lines(entity, query)];

    private IEnumerable<CardLine> Haunts(CreatureDocument entity)
    {
        var dungeons = (entity.FoundIn ?? [])
            .Where(found => found.Type == EntityType.Dungeon)
            .SelectMany(found => Ui.Store.All(found.Type, found.Id).Take(1))
            .Select(Ui.Name)
            .Distinct()
            .ToList();

        if (dungeons.Count == 0) yield break;

        yield return new CardLine(null, dungeons.Count == 1 ? dungeons[0] : string.Join(", ", dungeons.Select(Shortened)));
    }

    private static string Shortened(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } words
            ? string.Concat(words.Select(word => word[0]))
            : name;
}
