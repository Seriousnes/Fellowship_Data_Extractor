using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CraftSlotTests : BunitContext
{
    private readonly Export export;

    public CraftSlotTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Store);
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private (ItemDocument Item, RarityEntry Rung) Rolling()
    {
        var item = export.Store.Entities
            .OfType<ItemDocument>()
            .First(row =>
                row.RuleSet is { } rule &&
                export.Store.Settings.StatCalculation.RarityAdditions.ContainsKey(rule) &&
                row.Rarities is { Count: > 0 });

        var top = item.Rarities!.Max();

        return (item, export.Store.Settings.Rarities.Single(rung => rung.Tier == (int)top));
    }

    private IRenderedComponent<Craft> Island(ItemDocument item, RarityEntry rung) =>
        Render<Craft>(p => p
            .Add(x => x.Slug, EntityTypes.Slug(EntityType.Item))
            .Add(x => x.Id, item.Id)
            .Add(x => x.Search, $"?rarity={rung.Tier}"));

    [Fact]
    public void A_button_stands_for_every_slot_the_rarity_grants_that_takes_a_choice()
    {
        var (item, rung) = Rolling();

        var expected = ItemCraft.Slots(item, export.Store.Settings, rung)
            .Where(slot => slot.Accepts.Count > 0)
            .ToArray();

        Assert.NotEmpty(expected);

        var hits = Island(item, rung).FindAll("button.cx-craft-hit");

        Assert.Equal(expected.Length, hits.Count);

        Assert.Equal(
            [.. expected.Select(slot => slot.Key)],
            [.. hits.Select(hit => $"{hit.GetAttribute("data-craft")}:{hit.GetAttribute("data-at")}")]);
    }

    [Fact]
    public void Every_button_names_the_slot_it_edits_and_opens_a_dialog()
    {
        var (item, rung) = Rolling();

        Assert.All(Island(item, rung).FindAll("button.cx-craft-hit"), hit =>
        {
            Assert.Equal("button", hit.GetAttribute("type"));
            Assert.Equal("dialog", hit.GetAttribute("aria-haspopup"));
            Assert.False(string.IsNullOrWhiteSpace(hit.GetAttribute("aria-label")));
        });
    }

    [Fact]
    public void An_item_at_a_rarity_that_grants_nothing_draws_no_button()
    {
        var (item, _) = Rolling();
        var lowest = export.Store.Settings.Rarities.OrderBy(rung => rung.Tier).First();

        var island = Render<Craft>(p => p
            .Add(x => x.Slug, EntityTypes.Slug(EntityType.Item))
            .Add(x => x.Id, item.Id)
            .Add(x => x.Search, $"?rarity={lowest.Tier}"));

        Assert.Equal(
            ItemCraft.Slots(item, export.Store.Settings, lowest).Count(slot => slot.Accepts.Count > 0),
            island.FindAll("button.cx-craft-hit").Count);
    }

    [Fact]
    public void Every_random_stat_slot_has_a_line_naming_its_ordinal()
    {
        var (item, rung) = Rolling();

        var slots = ItemCraft.Slots(item, export.Store.Settings, rung)
            .Where(slot => slot.SlotName == CraftSlots.RandomStat)
            .ToArray();

        var island = Island(item, rung);

        Assert.All(slots, slot =>
            Assert.NotNull(island.Find($".stats > li[data-rolls~=\"{slot.Ordinal}\"]")));
    }

    [Fact]
    public void Every_modifier_slot_has_a_row_at_its_position()
    {
        var (item, rung) = Rolling();

        var rolls = ItemCraft.Slots(item, export.Store.Settings, rung)
            .Count(slot => slot.SlotName == CraftSlots.RandomRolls);

        Assert.Equal(rolls, Island(item, rung).FindAll(".grp.mods > .mod").Count);
    }
}
