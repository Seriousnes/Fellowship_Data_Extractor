using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class GenericCardTests : BunitContext
{
    private readonly Export export;

    public GenericCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Generic> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Generic>(p => p
            .AddCascadingValue(document)
            .Add(x => x.Query, query));

    [Fact]
    public void A_gem_draws_its_kind_in_the_identity_group()
    {
        var gem = export.Store.Entities.OfType<GemDocument>().First();

        Assert.NotEmpty(Draw(gem, new TooltipQuery()).FindAll(".lines .kind"));
    }

    [Fact]
    public void A_currency_carries_the_rarity_chrome_its_own_rung_asks_for()
    {
        var coin = export.Store.Entities.OfType<CurrencyDocument>().FirstOrDefault(row => row.Rarity is not null);

        Assert.SkipWhen(coin is null, "The build carries no currency with a rarity.");

        Assert.DoesNotContain("plain", Draw(coin!, new TooltipQuery()).Find($".{TooltipView.Root}").ClassList, StringComparer.Ordinal);
    }

    [Fact]
    public void A_gem_type_draws_its_overflow_wording_in_a_group_of_its_own()
    {
        var overflowing = export.Store.Entities.OfType<GemTypeDocument>()
            .FirstOrDefault(row => row.OverflowDescription is not null);

        Assert.SkipWhen(overflowing is null, "The build carries no gem type with overflow wording.");

        var drawn = Draw(overflowing!, new TooltipQuery());

        Assert.Contains(
            "Past Max Power",
            drawn.Find(".lines").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_gem_draws_neither_the_imbued_line_nor_a_modifier_row()
    {
        var gem = export.Store.Entities.OfType<GemDocument>().First();

        var drawn = Draw(gem, new TooltipQuery
        {
            Modifiers = [new ItemModifier(ModifierKind.Essence, 1, 100)],
            ImbuedTraits = [new ImbuedTrait(1, 1)],
            AllocatedTraits = 2,
            HasImbuedTraits = true,
        });

        Assert.Empty(drawn.FindComponents<TipImbuedTraits>());
        Assert.Empty(drawn.FindComponents<TipModifiers>());
        Assert.Empty(drawn.FindAll(".sec.imbued"));
        Assert.Empty(drawn.FindAll(".mod"));
    }

    [Fact]
    public void A_power_the_query_says_nothing_about_draws_only_the_groups_it_carries()
    {
        var power = export.Store.Entities.OfType<RelicPowerDocument>().First();

        var drawn = Draw(power, new TooltipQuery());

        Assert.Empty(drawn.FindAll(".lines .stats"));
        Assert.Empty(drawn.FindAll(".lines .sec.setbonus"));
        Assert.Empty(drawn.FindAll(".lines .sec.legendary"));
    }
}
