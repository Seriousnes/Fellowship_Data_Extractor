using System.Globalization;

using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class AbilityCardTests : BunitContext
{
    private readonly Export export;

    public AbilityCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Ability> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Ability>(p => p
            .AddCascadingValue(document)
            .Add(x => x.Query, query));

    private AbilityDocument Spell => export.Store.Entities.OfType<AbilityDocument>()
        .First(row => row.Description is not null && row.Cooldown is not null);

    private EffectDocument Aura => export.Store.Entities.OfType<EffectDocument>()
        .First(row => row.Description is not null);

    [Fact]
    public void An_ability_and_an_effect_draw_the_same_card()
    {
        Assert.NotNull(Draw(Spell, new TooltipQuery()).Find($".{TooltipView.Root} > .card"));
        Assert.NotNull(Draw(Aura, new TooltipQuery()).Find($".{TooltipView.Root} > .card"));
    }

    [Fact]
    public void The_facts_row_carries_the_numbers_the_document_states()
    {
        var drawn = Draw(Spell, new TooltipQuery());

        var labels = drawn.FindAll(".lines .row .k").Select(cell => cell.TextContent).ToArray();

        Assert.Contains("Cooldown", labels, StringComparer.Ordinal);
    }

    [Fact]
    public void A_bare_ability_draws_the_plain_window_and_no_identity_group()
    {
        var drawn = Draw(Spell, new TooltipQuery());
        var root = drawn.Find($".{TooltipView.Root}");

        Assert.Contains("plain", root.ClassList, StringComparer.Ordinal);
        Assert.Empty(drawn.FindAll(".lines .rar"));
        Assert.Empty(drawn.FindAll(".card > .head > img.orn"));
    }

    [Fact]
    public void A_rarity_in_the_query_puts_the_rarity_chrome_on_an_ability()
    {
        var rung = export.Store.Settings.Rarities[^1];

        var drawn = Draw(Spell, new TooltipQuery
        {
            Rarity = rung.Tier.ToString(CultureInfo.InvariantCulture),
        });

        Assert.DoesNotContain("plain", drawn.Find($".{TooltipView.Root}").ClassList, StringComparer.Ordinal);
        Assert.NotEmpty(drawn.FindAll(".card > .head > img.orn"));
        Assert.NotEmpty(drawn.FindAll(".lines .rar"));
    }

    [Fact]
    public void An_ability_draws_neither_the_imbued_line_nor_a_modifier_row()
    {
        var drawn = Draw(Spell, new TooltipQuery
        {
            Modifiers = [new ItemModifier(ModifierKind.Trait, 1, 1)],
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
    public void The_prose_reads_the_description_in_the_styles_the_build_ships()
    {
        var styled = export.Store.Entities.OfType<AbilityDocument>().First(row =>
            row.Description is { } text && text.Contains("<rt.", StringComparison.Ordinal));

        var prose = Draw(styled, new TooltipQuery()).Find(".lines .desc");

        Assert.NotEmpty(prose.QuerySelectorAll("span.rt"));
        Assert.DoesNotContain("<rt.", prose.TextContent, StringComparison.Ordinal);
    }
}
