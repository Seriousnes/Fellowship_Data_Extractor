using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CardTests : BunitContext
{
    private readonly Export export;

    public CardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Tooltip> Draw(EntityDocument document) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, document).Add(x => x.Query, new TooltipQuery()));

    private EntityDocument? FirstOf(EntityType type) => export.Store.Entities
        .Where(row => EntityTypes.TypeOf(row) == type)
        .OrderBy(row => row.Id)
        .FirstOrDefault();

    [Fact]
    public void Every_type_the_build_carries_is_drawn_by_exactly_one_card()
    {
        foreach (var type in Enum.GetValues<EntityType>())
        {
            if (FirstOf(type) is not { } document) continue;

            var drawn = Draw(document);

            var cards =
                drawn.FindComponents<Item>().Count +
                drawn.FindComponents<Ability>().Count +
                drawn.FindComponents<Generic>().Count +
                drawn.FindComponents<Blessing>().Count +
                drawn.FindComponents<Trait>().Count;

            Assert.True(cards == 1, $"{EntityTypes.Slug(type)} drew {cards} cards.");
            Assert.NotNull(drawn.Find($".{TooltipView.Root}"));
        }
    }

    [Fact]
    public void The_type_names_the_card()
    {
        Assert.Single(Draw(export.Store.Entities.OfType<ItemDocument>().First()).FindComponents<Item>());
        Assert.Single(Draw(export.Store.Entities.OfType<AbilityDocument>().First()).FindComponents<Ability>());
        Assert.Single(Draw(export.Store.Entities.OfType<EffectDocument>().First()).FindComponents<Ability>());
        Assert.Single(Draw(export.Store.Entities.OfType<BlessingDocument>().First()).FindComponents<Blessing>());
        Assert.Single(Draw(export.Store.Entities.OfType<TraitDocument>().First()).FindComponents<Trait>());
        Assert.Single(Draw(export.Store.Entities.OfType<GemDocument>().First()).FindComponents<Generic>());
    }

    [Fact]
    public void A_group_the_query_adds_and_takes_away_leaves_the_card_holding_neither_a_stale_group_nor_a_stray_rule()
    {
        var worn = export.Store.Entities.OfType<ItemDocument>()
            .First(row => row.Stats is { Count: > 0 } && row.SetBonus is { Count: > 0 });

        var drawn = Render<Tooltip>(p => p.Add(x => x.Entity, worn).Add(x => x.Query, new TooltipQuery()));

        var bare = Shape(drawn);
        var sets = drawn.Find(".lines .sec.setbonus").ParentElement!.TextContent;

        Assert.Empty(drawn.FindAll(".lines .sec .alloc"));

        drawn.Render(p => p.Add(x => x.Entity, worn).Add(x => x.Query, new TooltipQuery { MaxTraits = 3 }));

        var imbued = Shape(drawn);
        Assert.Equal(bare.Groups + 1, imbued.Groups);
        Assert.Equal(bare.Rules + 1, imbued.Rules);
        Assert.NotEmpty(drawn.FindAll(".lines .sec .alloc"));
        Assert.Equal(sets, Assert.Single(drawn.FindAll(".lines .sec.setbonus")).ParentElement!.TextContent);

        drawn.Render(p => p.Add(x => x.Entity, worn).Add(x => x.Query, new TooltipQuery()));

        Assert.Equal(bare, Shape(drawn));
        Assert.Empty(drawn.FindAll(".lines .sec .alloc"));
        Assert.Equal(sets, Assert.Single(drawn.FindAll(".lines .sec.setbonus")).ParentElement!.TextContent);
    }

    private static (int Groups, int Rules, string Order) Shape(IRenderedComponent<Tooltip> drawn)
    {
        var children = drawn.Find(".lines").Children;

        var order = string.Concat(children.Select(child =>
            string.Equals(child.TagName, "HR", StringComparison.OrdinalIgnoreCase) ? "-" : "G"));

        return (order.Count(mark => mark == 'G'), order.Count(mark => mark == '-'), order);
    }

    [Fact]
    public void The_dispatcher_adds_no_element_of_its_own()
    {
        var markup = Draw(export.Store.Entities.OfType<ItemDocument>().First()).Markup.TrimStart();

        Assert.StartsWith("<div class=\"fx-tip", markup, StringComparison.Ordinal);
    }
}
