using System.Globalization;

using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ItemCardTests : BunitContext
{
    private readonly Export export;

    public ItemCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IEnumerable<ItemDocument> Items => export.Store.Entities.OfType<ItemDocument>();

    private IRenderedComponent<Item> Draw(ItemDocument document, TooltipQuery query) =>
        Render<Item>(p => p
            .AddCascadingValue((EntityDocument)document)
            .Add(x => x.Query, query));

    [Fact]
    public void An_item_draws_its_art_beside_the_card()
    {
        var worn = Items.First(row => row.Icon is not null);

        var drawn = Draw(worn, new TooltipQuery());

        Assert.NotEmpty(drawn.FindAll($".{TooltipView.Root} > figure.fx-slot > img"));
        Assert.NotNull(drawn.Find($".{TooltipView.Root} > .card > .head > .title"));
    }

    [Fact]
    public void The_rung_asks_for_the_rarity_chrome_and_its_absence_asks_for_the_plain_window()
    {
        var rung = export.Store.Settings.Rarities[^1];
        var asked = new TooltipQuery { Rarity = rung.Tier.ToString(CultureInfo.InvariantCulture) };

        var coloured = Draw(Items.First(), asked);
        var root = coloured.Find($".{TooltipView.Root}");

        Assert.DoesNotContain("plain", root.ClassList, StringComparer.Ordinal);
        Assert.NotEmpty(coloured.FindAll(".card > .head > img.orn"));
        Assert.NotEmpty(coloured.FindAll(".lines .rar"));
    }

    [Fact]
    public void A_granted_power_draws_the_ability_group_under_its_name()
    {
        var granting = Items.First(row =>
            row.Grants is not null && export.Tooltip.Granted(row) is not null);

        var drawn = Draw(granting, new TooltipQuery());

        var badge = drawn.Find(".lines .sec.ability span:last-child").TextContent;

        Assert.StartsWith("Ability", badge, StringComparison.Ordinal);
    }

    [Fact]
    public void Legendary_stats_and_a_set_bonus_each_draw_a_group_of_their_own()
    {
        var rolled = Items.First(row => export.Tooltip.LegendaryStats(row).Count > 0);
        Assert.NotEmpty(Draw(rolled, new TooltipQuery()).FindAll(".lines .sec.legendary"));

        var setted = Items.First(row => row.SetBonus is { Count: > 0 });
        Assert.NotEmpty(Draw(setted, new TooltipQuery()).FindAll(".lines .sec.setbonus"));
    }

    [Fact]
    public void The_imbued_line_and_the_modifier_rows_sit_between_the_rolls_and_the_groups_below()
    {
        var worn = Items.First(row => row.Stats is { Count: > 0 } && row.SetBonus is { Count: > 0 });

        var drawn = Draw(worn, Rich());

        var at = Groups(drawn, ".rar", ".stats", ".sec.imbued", ".mod", ".sec.setbonus");

        Assert.DoesNotContain(-1, at);
        Assert.Equal(at.Count, at.Distinct().Count());
        Assert.Equal([.. at.Order()], at);
    }

    [Fact]
    public void Every_group_the_card_draws_comes_out_in_the_order_the_card_builds_them()
    {
        string[] markers =
        [
            ".rar", ".stats", ".sec.imbued", ".mod",
            ".sec.ability", ".sec.legendary", ".sec.setbonus",
        ];

        ItemDocument[] shapes =
        [
            Items.First(row => row.Grants is not null && export.Tooltip.Granted(row) is not null),
            Items.First(row => export.Tooltip.LegendaryStats(row).Count > 0),
            Items.First(row => row.SetBonus is { Count: > 0 }),
            Items.First(row => row.Stats is { Count: > 0 }),
        ];

        foreach (var worn in shapes)
        {
            var at = Groups(Draw(worn, Rich()), markers).Where(index => index >= 0).ToArray();

            Assert.Equal([.. at.Order()], at);
        }
    }

    private TooltipQuery Rich() => new()
    {
        Rarity = export.Store.Settings.Rarities[^1].Tier.ToString(CultureInfo.InvariantCulture),
        HasImbuedTraits = true,
        Modifiers = [new ItemModifier(ModifierKind.Trait, 1, 1)],
    };

    private static IReadOnlyList<int> Groups(IRenderedComponent<Item> drawn, params string[] markers)
    {
        var groups = drawn.FindAll(".lines > .grp");

        return
        [
            .. markers.Select(marker => Enumerable.Range(0, groups.Count)
                .FirstOrDefault(at => groups[at].QuerySelector(marker) is not null, -1)),
        ];
    }

    [Fact]
    public void A_rule_sits_between_every_pair_of_groups_and_never_above_the_first()
    {
        var drawn = Draw(Items.First(row => row.SetBonus is { Count: > 0 }), new TooltipQuery());

        var lines = drawn.Find(".lines");
        var children = lines.Children;

        Assert.NotEqual("HR", children[0].TagName, StringComparer.OrdinalIgnoreCase);

        var rules = children.Count(child => string.Equals(child.TagName, "HR", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(children.Length - rules - 1, rules);
    }
}
