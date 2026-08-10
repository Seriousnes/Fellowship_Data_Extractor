using System.Globalization;

using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ModifierCardTests : BunitContext
{
    private readonly Export export;

    public ModifierCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<TipModifiers> Draw(params ItemModifier[] modifiers) =>
        Render<TipModifiers>(p => p.Add(
            x => x.Lines,
            export.Tooltip.Modifiers(new TooltipQuery { Modifiers = modifiers })));

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
            Enum.GetValues<ModifierKind>().Select(export.Chrome.ModifierGlyph).Distinct(StringComparer.Ordinal).Count());
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
    public void Every_glyph_the_rows_ask_for_is_a_payload_the_seeder_uploads()
    {
        var carried = export.Chrome.Payloads()
            .Select(payload => payload.Name)
            .ToHashSet(StringComparer.Ordinal);

        static string Blob(string url) => url[(url.IndexOf($"/{ChromeAssetService.Prefix}/", StringComparison.Ordinal) + 1)..];

        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            Assert.Contains(Blob(export.Chrome.ModifierGlyph(kind)), carried, StringComparer.Ordinal);
        }

        Assert.Contains(Blob(export.Chrome.ModifierDecor), carried, StringComparer.Ordinal);
        Assert.Contains(Blob(export.Chrome.ModifierPlate), carried, StringComparer.Ordinal);
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
