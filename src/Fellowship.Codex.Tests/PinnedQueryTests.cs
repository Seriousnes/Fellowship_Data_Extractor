namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class PinnedQueryTests(Export export)
{
    private static readonly CodexQuery Chosen = new CodexQuery()
        .WithFacet(FacetType.Slot, "Weapon")
        .WithFacet(FacetType.Source, "Elsewhere") with { Types = [EntityType.Ability], Page = 3 };

    [Fact]
    public void Unpinning_leaves_the_reader_choices_but_the_pinned_facet_and_the_type()
    {
        var free = Chosen.Unpinned(FacetType.Source);

        Assert.Empty(free.Types);
        Assert.False(free.Selections.ContainsKey(FacetType.Source));
        Assert.Equal(["Weapon"], free.Selections[FacetType.Slot]);
    }

    [Fact]
    public void Pinning_fixes_the_type_and_the_value_and_keeps_the_page()
    {
        var pinned = Chosen.Unpinned(FacetType.Source).Pinned(EntityType.Item, FacetType.Source, "Here");

        Assert.Equal([EntityType.Item], pinned.Types);
        Assert.Equal(["Here"], pinned.Selections[FacetType.Source]);
        Assert.Equal(3, pinned.Page);
    }

    [Fact]
    public void A_link_from_an_address_with_a_query_keeps_that_query_first()
    {
        var view = new BrowseView(export.View.Codex, new CodexQuery(), Empty, "/dungeon/1/x?tab=creature");

        Assert.Equal("/dungeon/1/x?tab=creature&slot=Weapon", view.Toggle(FacetType.Slot, "Weapon"));
        Assert.Equal("/dungeon/1/x?tab=creature", view.GroupBy(null));
    }

    [Fact]
    public void A_pinned_facet_is_not_on_the_rail()
    {
        var results = new CodexResults(0, 1, 1, [], [Tally(FacetType.Source), Tally(FacetType.Slot)],
            new Dictionary<EntityType, int>(), null, [], []);
        var view = new BrowseView(export.View.Codex, new CodexQuery(), results, "/", new HashSet<FacetType> { FacetType.Source });

        Assert.Equal([FacetType.Slot], view.Rail.Select(facet => facet.Type));
    }

    private static readonly CodexResults Empty =
        new(0, 1, 1, [], [], new Dictionary<EntityType, int>(), null, [], []);

    private FacetCount Tally(FacetType facet) =>
        export.View.Codex.Tally(export.View.Codex.Facet(facet),
            [.. export.Store.Entities.Where(row => row is ItemDocument).Take(500)]);
}
