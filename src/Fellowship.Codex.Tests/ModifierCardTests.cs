using System.Globalization;

using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ModifierCardTests : BunitContext
{
    private readonly Export export;

    public ModifierCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private IRenderedComponent<TipModifiers> Draw(params ItemModifier[] modifiers) =>
        Draw(new ItemDocument { Id = 1 }, rarity: null, modifiers);

    private IRenderedComponent<TipModifiers> Draw(
        ItemDocument document, RarityEntry? rarity, params ItemModifier[] modifiers) =>
        Render<TipModifiers>(p => p.Add(
            x => x.Lines,
            export.Tooltip.Modifiers(document, new TooltipQuery { Modifiers = modifiers }, rarity)));

    private (ItemDocument Item, RarityEntry Rung, int Rolls) Rolling(string ruleSet, int tier)
    {
        var rung = export.Store.Settings.Rarities.Single(entry => entry.Tier == tier);
        var rolls = export.Store.Settings.StatCalculation.RarityAdditions[ruleSet][tier].RandomRolls;

        return (new ItemDocument { Id = 1, RuleSet = ruleSet }, rung, rolls);
    }

    private int Lowest(ModifierKind kind) => export.Store.Entities
        .Where(row => EntityTypes.TypeOf(row) == TooltipView.TypeOf(kind))
        .Select(row => row.Id)
        .Order()
        .First();

    private string Named(ModifierKind kind, int id) =>
        PublicTypes.NameOf(export.Store.All(TooltipView.TypeOf(kind), id)[0]);

    [Fact]
    public void A_row_is_drawn_for_every_entry_in_the_order_the_query_states()
    {
        ItemModifier[] stated =
        [
            new(ModifierKind.Blessing, Lowest(ModifierKind.Blessing), 1),
            new(ModifierKind.Trait, Lowest(ModifierKind.Trait), 2),
            new(ModifierKind.Essence, Lowest(ModifierKind.Essence), 100),
        ];

        var rows = Draw(stated).FindAll(".grp.mods > .mod");

        Assert.Equal(stated.Length, rows.Count);

        Assert.Equal(
            [.. stated.Select(modifier => TooltipView.Token(modifier.Kind))],
            [.. rows.Select(row => row.ClassList.Last())]);
    }

    [Fact]
    public void Every_kind_draws_the_glyph_the_widget_names_for_it()
    {
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            var row = Draw(new ItemModifier(kind, Lowest(kind), 1)).Find(".grp.mods > .mod");

            Assert.Equal(
                export.Chrome.ModifierGlyph(kind),
                row.QuerySelector("img.mi")!.GetAttribute("src"));
        }

        Assert.Equal(
            Enum.GetValues<ModifierKind>().Length,
            Enum.GetValues<ModifierKind>()
                .Select(kind => export.Chrome.ModifierGlyph(kind))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void Two_entries_of_one_id_draw_two_rows_of_their_own()
    {
        var id = Lowest(ModifierKind.Trait);

        var rows = Draw(
            new ItemModifier(ModifierKind.Trait, id, 1),
            new ItemModifier(ModifierKind.Trait, id, 1)).FindAll(".grp.mods > .mod");

        Assert.Equal(2, rows.Count);
        Assert.Equal(rows[0].TextContent, rows[1].TextContent);
        Assert.Contains(Named(ModifierKind.Trait, id), rows[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_kind_draws_the_label_the_game_prints_and_the_amount_it_carries()
    {
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            var id = Lowest(kind);

            var row = Draw(new ItemModifier(kind, id, 3)).Find(".grp.mods > .mod");

            Assert.Equal($"{TooltipView.Label(kind)}:", row.QuerySelector(".mk")!.TextContent);
            Assert.Equal(Named(kind, id), row.QuerySelector(".mn")!.TextContent);
            Assert.Equal("+3", row.QuerySelector(".mv")!.TextContent);
        }
    }

    [Fact]
    public void An_id_the_store_does_not_carry_prints_the_id_itself()
    {
        var unknown = export.Store.Entities.Max(row => row.Id) + 1;

        var row = Draw(new ItemModifier(ModifierKind.Trait, unknown, 1)).Find(".grp.mods > .mod");

        Assert.Equal(unknown.ToString(CultureInfo.InvariantCulture), row.QuerySelector(".mn")!.TextContent);
    }

    [Fact]
    public void Every_texture_a_row_asks_for_composes_under_the_prefix()
    {
        static void Under(string url)
        {
            var name = url[(url.LastIndexOf('/') + 1)..];

            Assert.EndsWith(".png", name, StringComparison.Ordinal);
            Assert.EndsWith($"/{ChromeAssetService.Prefix}/{name}", url, StringComparison.Ordinal);
        }

        Under(export.Chrome.ModifierPlate);
        Under(export.Chrome.ModifierDecor);
        Under(export.Chrome.ModifierGlyph(null));

        foreach (var kind in Enum.GetValues<ModifierKind>()) Under(export.Chrome.ModifierGlyph(kind));

        Assert.DoesNotContain(
            export.Chrome.ModifierGlyph(null),
            [.. Enum.GetValues<ModifierKind>().Select(kind => export.Chrome.ModifierGlyph(kind))],
            StringComparer.Ordinal);
    }

    [Fact]
    public void A_row_stands_for_every_random_modifier_the_rarity_rolls()
    {
        var (item, rung, rolls) = Rolling("Default_NoGemSlot", 5);

        Assert.True(rolls > 0);

        var rows = Draw(item, rung).FindAll(".grp.mods > .mod");

        Assert.Equal(rolls, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Contains(TooltipView.Unresolved, row.ClassList, StringComparer.Ordinal);
            Assert.Equal(TooltipView.RandomModifierLabel, row.QuerySelector(".mn")!.TextContent);
            Assert.Equal(
                export.Chrome.ModifierGlyph(null),
                row.QuerySelector("img.mi")!.GetAttribute("src"));
            Assert.Null(row.QuerySelector(".mk"));
            Assert.Null(row.QuerySelector(".mv"));
        });
    }

    [Fact]
    public void A_stated_modifier_fills_a_roll_rather_than_standing_beside_it()
    {
        var (item, rung, rolls) = Rolling("Default_NoGemSlot", 5);
        var id = Lowest(ModifierKind.Trait);

        var rows = Draw(item, rung, new ItemModifier(ModifierKind.Trait, id, 1)).FindAll(".grp.mods > .mod");

        Assert.Equal(rolls, rows.Count);
        Assert.Equal(Named(ModifierKind.Trait, id), rows[0].QuerySelector(".mn")!.TextContent);

        Assert.All(rows.Skip(1), row =>
            Assert.Equal(TooltipView.RandomModifierLabel, row.QuerySelector(".mn")!.TextContent));
    }

    [Fact]
    public void The_rule_set_the_item_names_decides_how_many_rolls_it_carries()
    {
        var (plain, rung, plainRolls) = Rolling("Default_NoGemSlot", 3);
        var (relic, _, relicRolls) = Rolling("Relic", 3);

        Assert.NotEqual(plainRolls, relicRolls);

        Assert.Equal(plainRolls, Draw(plain, rung).FindAll(".grp.mods > .mod").Count);
        Assert.Equal(relicRolls, Draw(relic, rung).FindAll(".grp.mods > .mod").Count);
    }

    [Fact]
    public void A_rarity_that_rolls_nothing_draws_no_row_at_all()
    {
        var (item, rung, rolls) = Rolling("Default_NoGemSlot", 1);

        Assert.Equal(0, rolls);
        Assert.Empty(Draw(item, rung).FindAll(".grp.mods > .mod"));
    }

    [Fact]
    public void An_item_naming_no_rule_set_draws_only_what_the_query_states()
    {
        var rung = export.Store.Settings.Rarities.Single(entry => entry.Tier == 5);

        Assert.Empty(Draw(new ItemDocument { Id = 1 }, rung).FindAll(".grp.mods > .mod"));
    }

    [Fact]
    public void A_divider_sits_between_every_adjacent_pair_of_rows_and_never_above_the_first()
    {
        var id = Lowest(ModifierKind.Trait);

        var children = Draw(
            new ItemModifier(ModifierKind.Trait, id, 1),
            new ItemModifier(ModifierKind.Trait, id, 1),
            new ItemModifier(ModifierKind.Trait, id, 1)).Find(".grp.mods").Children;

        var rules = children.Count(child =>
            string.Equals(child.TagName, "HR", StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual("HR", children[0].TagName, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual("HR", children[^1].TagName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(children.Length - rules - 1, rules);
    }
}
