using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class InsetArtTests : BunitContext
{
    private readonly IEntityStoreService Store;

    public InsetArtTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private IRenderedComponent<Tooltip> Draw(EntityDocument document) =>
        Render<Tooltip>(p => p
            .Add(x => x.Entity, document)
            .Add(x => x.Query, new TooltipQuery { ShowIcon = true }));

    [Fact]
    public void A_hero_trait_carries_its_art_inside_the_card()
    {
        var drawn = Draw(Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null));

        Assert.NotNull(drawn.Find(".card .fx-trait-crest img"));
        Assert.Empty(drawn.FindAll("figure.fx-slot"));
    }

    [Fact]
    public void A_blessing_carries_its_art_inside_the_card()
    {
        var drawn = Draw(Store.Entities.OfType<BlessingDocument>().First(row => row.Icon is not null));

        Assert.NotNull(drawn.Find(".card .fx-blessing-crest img"));
        Assert.Empty(drawn.FindAll("figure.fx-slot"));
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
            Assert.NotEmpty(drawn.FindAll("figure.fx-slot"));
        });
    }

    [Fact]
    public void A_rung_chooses_the_rarity_chrome_and_its_absence_chooses_the_plain_chrome()
    {
        var item = Draw(Store.Entities.OfType<ItemDocument>()
            .First(row => row.Icon is not null && (row.Rarity is not null || row.Rarities is { Count: > 0 })));

        var slot = item.Find("figure.fx-slot");
        var worn = slot.GetAttribute("style") ?? string.Empty;

        Assert.DoesNotContain("plain", slot.ClassList);
        Assert.Contains("T_Borders_EmptyWithoutFrame.png", worn, StringComparison.Ordinal);
        Assert.Contains("T_Borders_RarityFrame-", worn, StringComparison.Ordinal);
        Assert.Contains("--fx-slot-bevel:url(", worn, StringComparison.Ordinal);

        var ability = Draw(Store.Entities.OfType<AbilityDocument>().First(row => row.Icon is not null))
            .Find("figure.fx-slot");

        var cast = ability.GetAttribute("style") ?? string.Empty;

        Assert.Contains("plain", ability.ClassList);
        Assert.Contains("T_Buttons_AbilityBarBackground_Small.png", cast, StringComparison.Ordinal);
        Assert.Contains("T_Buttons_SmallAbilityButton_Default.png", cast, StringComparison.Ordinal);
        Assert.DoesNotContain("--fx-slot-bevel", cast, StringComparison.Ordinal);
    }
}
