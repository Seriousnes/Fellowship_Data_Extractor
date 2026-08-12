using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ImbuedTraitsCardTests : BunitContext
{
    private readonly Export export;

    public ImbuedTraitsCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<TipImbuedTraits> Draw(ItemDocument document, TooltipQuery query) =>
        Render<TipImbuedTraits>(p => p
            .Add(x => x.Entity, document)
            .Add(x => x.Query, query));

    private ItemDocument Worn(string slot) => export.Store.Entities.OfType<ItemDocument>()
        .First(row => string.Equals(row.Slot, slot, StringComparison.OrdinalIgnoreCase));

    private static string Fraction(IRenderedComponent<TipImbuedTraits> drawn) =>
        drawn.Find(".grp > .sec.imbued > .n").TextContent;

    [Fact]
    public void Every_slot_the_table_names_draws_the_capacity_it_states()
    {
        foreach (var (slot, capacity) in TooltipView.ImbuedTraitCapacities)
        {
            var drawn = Draw(Worn(slot), new TooltipQuery());

            Assert.Equal($"(0/{capacity})", Fraction(drawn));
            Assert.Equal("0", drawn.Find(".sec.imbued .alloc").TextContent);
        }
    }

    [Fact]
    public void A_weapon_draws_five_and_a_neck_item_draws_two()
    {
        Assert.Equal("(0/5)", Fraction(Draw(Worn("Weapon"), new TooltipQuery())));
        Assert.Equal("(0/2)", Fraction(Draw(Worn("Neck"), new TooltipQuery())));
    }

    [Fact]
    public void Every_other_slot_draws_no_line_at_all()
    {
        var others = export.Store.Entities.OfType<ItemDocument>()
            .Where(row => row.Slot is { } slot && !TooltipView.ImbuedTraitCapacities.ContainsKey(slot))
            .Select(row => row.Slot!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(others);

        foreach (var slot in others)
        {
            Assert.Empty(Draw(Worn(slot), new TooltipQuery()).FindAll(".sec.imbued"));
        }
    }

    [Fact]
    public void The_list_of_imbued_traits_counts_the_line_where_the_allocated_count_says_otherwise()
    {
        var drawn = Draw(Worn("Weapon"), new TooltipQuery
        {
            ImbuedTraits = [new ImbuedTrait(1, 1), new ImbuedTrait(2, 3)],
            AllocatedTraits = 4,
        });

        Assert.Equal("(2/5)", Fraction(drawn));
    }

    [Fact]
    public void The_allocated_count_supplies_the_number_where_no_list_is_given()
    {
        Assert.Equal("(3/5)", Fraction(Draw(Worn("Weapon"), new TooltipQuery { AllocatedTraits = 3 })));
    }

    [Fact]
    public void The_override_draws_the_line_on_an_item_whose_slot_names_no_capacity()
    {
        var ringed = Worn("Ring");

        Assert.Empty(Draw(ringed, new TooltipQuery()).FindAll(".sec.imbued"));

        var drawn = Draw(ringed, new TooltipQuery { HasImbuedTraits = true });

        Assert.Equal($"(0/{TooltipView.ImbuedTraitCapacities.Values.Max()})", Fraction(drawn));
    }

    [Fact]
    public void The_override_takes_the_line_away_from_an_item_whose_slot_names_one()
    {
        foreach (var slot in TooltipView.ImbuedTraitCapacities.Keys)
        {
            Assert.Empty(
                Draw(Worn(slot), new TooltipQuery { HasImbuedTraits = false }).FindAll(".sec.imbued"));
        }
    }

    [Fact]
    public void The_override_keeps_the_capacity_the_slot_states_when_the_slot_states_one()
    {
        Assert.Equal("(0/2)", Fraction(Draw(Worn("Neck"), new TooltipQuery { HasImbuedTraits = true })));
    }
}
