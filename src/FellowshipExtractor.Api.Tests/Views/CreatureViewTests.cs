namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CreatureViewTests : BunitContext
{
    private readonly Export export;

    public CreatureViewTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Tooltip);
    }

    private CreatureDocument Wielding => export.Store.Entities
        .OfType<CreatureDocument>()
        .First(row => row.Abilities is { Count: > 0 } && row.FoundIn is { Count: > 0 });

    private static string Head(IElement panel) =>
        panel.QuerySelector(":scope > div.cx-panel-head")?.TextContent ?? string.Empty;

    private IRenderedComponent<Detail> Draw(EntityDocument document) =>
        Render<Detail>(p => p.Add(x => x.Entity, document));

    [Fact]
    public void A_creature_draws_its_abilities_grouped_by_the_phase_that_slots_them()
    {
        var creature = Wielding;

        var cut = Draw(creature);

        var panel = cut.FindAll("div.cx-panel")
            .Single(element => Head(element).StartsWith("Abilities", StringComparison.Ordinal));

        var phases = panel.QuerySelectorAll("p.cx-sub-head").Select(head => head.TextContent).ToArray();

        Assert.Equal(
            creature.Abilities!.Select(slot => slot.Phase).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            phases.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_ability_the_game_hides_is_marked_and_a_shown_one_is_not()
    {
        var creature = export.Store.Entities
            .OfType<CreatureDocument>()
            .First(row => row.Abilities is { Count: > 0 } &&
                row.Abilities.Any(slot => !slot.Shown) &&
                row.Abilities.Any(slot => slot.Shown));

        var cut = Draw(creature);

        var panel = cut.FindAll("div.cx-panel")
            .Single(element => Head(element).StartsWith("Abilities", StringComparison.Ordinal));

        var marked = panel.QuerySelectorAll("ul.cx-pills > li").Count(item => item.TextContent.Contains("hidden"));

        Assert.Equal(creature.Abilities!.Count(slot => !slot.Shown), marked);
    }

    [Fact]
    public void A_creature_links_every_dungeon_it_is_found_in()
    {
        var creature = Wielding;

        var cut = Draw(creature);

        var panel = cut.FindAll("div.cx-panel")
            .Single(element => Head(element).StartsWith("Found in", StringComparison.Ordinal));

        var hrefs = panel.QuerySelectorAll("ul.cx-pills a").Select(link => link.GetAttribute("href")).ToArray();

        Assert.Equal(
            creature.FoundIn!.Select(level => $"/dungeon/{level.Id}").Order(StringComparer.Ordinal),
            hrefs.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_dungeon_lists_every_creature_found_there_with_its_rank_and_kill_score()
    {
        var dungeon = export.Store.Entities
            .OfType<DungeonDocument>()
            .First(level => export.Store.Entities
                .OfType<CreatureDocument>()
                .Any(creature => creature.FoundIn?.Any(found => found.Id == level.Id) is true));

        var expected = export.Store.Entities
            .OfType<CreatureDocument>()
            .Count(creature => creature.FoundIn?.Any(found => found.Id == dungeon.Id) is true);

        var cut = Draw(dungeon);

        var panel = cut.FindAll("div.cx-panel")
            .Single(element => Head(element).StartsWith("Creatures", StringComparison.Ordinal));

        var heads = panel.QuerySelectorAll("thead th").Select(cell => cell.TextContent).ToArray();

        Assert.Equal(["", "Name", "Rank", "Health", "Kill score"], heads);
        Assert.Equal(expected, panel.QuerySelectorAll("tbody tr").Length);
    }
}
