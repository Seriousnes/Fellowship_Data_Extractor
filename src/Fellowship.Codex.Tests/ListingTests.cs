namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ListingTests(Export export)
{
    private const int Betrayers = 5191;

    private CodexResults Run(params (string Key, string Value)[] asked) => Searching.Run(export, asked);

    private EntityDocument Find(CodexResults results, int id, string hero) =>
        results.Rows.Single(row => row.Id == id && export.Store.Wearer(row)?.Name == hero);

    [Fact]
    public void An_id_is_drawn_once_per_reading()
    {
        var results = Run(("type", "item"));

        Assert.Equal(results.Rows.Count, results.Rows.Distinct().Count());
        Assert.Equal(2684, results.Total);
    }

    [Fact]
    public void An_id_reading_twelve_ways_draws_twelve_rows()
    {
        var id = Betrayers.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(12, export.Store.All(EntityType.Item, Betrayers).Count);

        var drawn = Run(("type", "item"), ("idMin", id), ("idMax", id));

        Assert.Equal(12, drawn.Total);
        Assert.All(drawn.Rows, row => Assert.Equal(Betrayers, row.Id));
        Assert.Equal(12, drawn.Rows.Select(row => export.Store.Wearer(row)?.Name).Distinct().Count());
    }

    [Fact]
    public void A_talent_slot_id_draws_one_row_per_hero()
    {
        var slots = export.Store.Entities.OfType<TalentSlotDocument>().ToArray();

        Assert.Equal(216, slots.Length);
        Assert.Equal(19, slots.Select(slot => slot.Id).Distinct().Count());

        var one = slots.GroupBy(slot => slot.Id).First(group => group.Count() > 1);

        Assert.Equal(one.Count(), one.Select(slot => slot.Hero).Distinct().Count());
    }

    [Fact]
    public void A_rows_link_carries_the_hero_its_tooltip_carries()
    {
        var row = Run(("type", "item"), ("q", "Betrayer's")).Rows.First(drawn => drawn.Id == Betrayers);

        var hero = export.Store.Wearer(row)!.Name;
        var wearer = Uri.EscapeDataString(hero);

        Assert.Equal($"/item/{Betrayers}?hero={wearer}", export.View.Href(row));
        Assert.Equal($"/api/item/{Betrayers}/tooltip?hero={wearer}", export.View.Tip(row));
    }

    [Fact]
    public void A_row_of_an_id_with_one_reading_carries_no_hero()
    {
        var only = export.Store.Entities
            .First(row => EntityTypes.TypeOf(row) == EntityType.Item &&
                export.Store.All(EntityType.Item, row.Id).Count == 1);

        Assert.Equal($"/item/{only.Id}", export.View.Href(only));
    }

    [Fact]
    public void A_filter_decides_which_reading_a_row_is_named_by()
    {
        var row = Find(Run(("type", "item"), ("hero", "Meiko")), Betrayers, "Meiko");

        Assert.Equal("Betrayer's Dragonhide Gloves", export.View.Name(row));
    }

    [Fact]
    public void Every_count_beneath_the_list_counts_rows()
    {
        var results = Run(("type", "item"), ("hero", "Meiko"));

        Assert.Equal(results.Total, results.Types[EntityType.Item]);

        var slot = results.Facets.Single(facet => facet.Key == "slot");
        var chosen = slot.Values[0];

        Assert.Equal(chosen.Count, Run(("type", "item"), ("hero", "Meiko"), ("slot", chosen.Value)).Total);
    }

    [Fact]
    public void A_more_like_this_badge_counts_the_rows_its_link_opens()
    {
        var slot = ((ItemDocument)export.Store.All(EntityType.Item, Betrayers)[0]).Slot!;
        var facet = export.View.Codex.Facet("slot")!;
        var named = export.View.Codex.Flag(CodexQuery.NamedFlag)!;

        var badge = export.Store.Entities.Count(row =>
            EntityTypes.TypeOf(row) == EntityType.Item && facet.Matches(row, [slot]) && named.Predicate(row));

        Assert.True(badge >= 2);
        Assert.Equal(badge, Run(("type", "item"), ("slot", slot)).Total);
    }
}
