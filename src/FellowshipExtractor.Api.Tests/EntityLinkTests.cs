namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class EntityLinkTests : BunitContext
{
    private readonly EntityStore Store;

    public EntityLinkTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.View);
    }

    private IElement Draw(EntityDocument document) =>
        Render<EntityLink>(p => p.Add(x => x.Entity, document)).Find("a");

    [Fact]
    public void A_link_to_a_row_the_build_colours_carries_that_colour()
    {
        EntityDocument[] coloured =
        [
            Store.Entities.OfType<ItemDocument>().First(item => item.Rarities is { Count: > 0 }),
            Store.Entities.OfType<GemTypeDocument>().First(),
            Store.Entities.OfType<GemTierDocument>().First(),
            Store.Entities.OfType<GemTraitDocument>().First(),
        ];

        Assert.All(coloured, document => Assert.StartsWith(
            "color:", Draw(document).GetAttribute("style"), StringComparison.Ordinal));
    }

    [Fact]
    public void A_link_to_every_other_row_asks_for_no_colour_at_all()
    {
        EntityDocument[] plain =
        [
            Store.Entities.OfType<AbilityDocument>().First(),
            Store.Entities.OfType<EffectDocument>().First(),
            Store.Entities.OfType<TalentDocument>().First(),
            Store.Entities.OfType<TalentSlotDocument>().First(),
            Store.Entities.OfType<CreatureDocument>().First(),
            Store.Entities.OfType<TraitDocument>().First(),
        ];

        Assert.All(plain, document => Assert.False(Draw(document).HasAttribute("style")));
    }
}
