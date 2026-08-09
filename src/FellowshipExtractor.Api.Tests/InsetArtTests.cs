using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class InsetArtTests : BunitContext
{
    private readonly IEntityStoreService Store;

    public InsetArtTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Tooltip> Draw(EntityDocument document) =>
        Render<Tooltip>(p => p
            .Add(x => x.Entity, document)
            .Add(x => x.Query, new TooltipQuery()));

    [Fact]
    public void A_hero_trait_carries_its_art_inside_the_card()
    {
        var drawn = Draw(Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null));

        Assert.NotNull(drawn.Find(".card .fx-trait-crest img"));
        Assert.Empty(drawn.FindAll("figure.slot"));
    }

    [Fact]
    public void A_blessing_carries_its_art_inside_the_card()
    {
        var drawn = Draw(Store.Entities.OfType<BlessingDocument>().First(row => row.Icon is not null));

        Assert.NotNull(drawn.Find(".card .fx-blessing-crest img"));
        Assert.Empty(drawn.FindAll("figure.slot"));
    }

    [Fact]
    public void Every_other_row_keeps_its_art_beside_the_card()
    {
        EntityDocument[] beside =
        [
            Store.Entities.OfType<ItemDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<TalentDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<AbilityDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<EffectDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<GemTraitDocument>().First(),
        ];

        Assert.All(beside, document =>
        {
            var drawn = Draw(document);

            Assert.Empty(drawn.FindAll(".card .fx-trait-crest"));
            Assert.NotEmpty(drawn.FindAll("figure.slot"));
        });
    }
}
