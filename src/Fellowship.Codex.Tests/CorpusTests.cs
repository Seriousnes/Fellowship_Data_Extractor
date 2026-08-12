namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CorpusTests : BunitContext
{
    private readonly IEntityStoreService Store;

    private readonly ITooltipViewService Tooltip;

    private readonly Export Exported;

    public CorpusTests(Export export)
    {
        Store = export.Store;
        Tooltip = export.Tooltip;
        Exported = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    [Fact]
    public void Every_listed_type_answers_a_first_page_that_draws_a_header_row()
    {
        var listed = PublicTypes.All.Where(type => type.Listed).ToArray();

        Assert.NotEmpty(listed);

        foreach (var type in listed)
        {
            var results = Searching.Run(Exported, ("type", type.Slug));

            Assert.Equal(type.Type, results.Chosen);
            Assert.NotEmpty(results.Rows);

            var view = new BrowseView(Exported.View.Codex, new CodexQuery { Types = [type.Type] }, results);

            var cut = Render<Components.Browse.Rows>(p => p
                .Add(x => x.Type, type.Type)
                .Add(x => x.Documents, results.Rows)
                .Add(x => x.View, view));

            var heads = cut.FindAll("thead th");

            Assert.NotEmpty(heads);
            Assert.Equal(results.Rows.Count, cut.FindAll("tbody tr").Count);

            Assert.All(cut.FindAll("tbody tr"),
                row => Assert.Equal(heads.Count, row.QuerySelectorAll(":scope > td").Length));
        }
    }

    [Fact]
    public void Every_item_that_states_the_rungs_it_comes_in_answers_at_one_unasked()
    {
        var items = Store.Entities
            .OfType<ItemDocument>()
            .Where(item => item.Rarities is { Count: > 0 })
            .ToArray();

        Assert.NotEmpty(items);

        foreach (var item in items)
        {
            var rung = Tooltip.Rarity(item, new TooltipQuery());

            Assert.NotNull(rung);
            Assert.Equal(item.Rarities!.Max().ToString(), rung.Name);
        }
    }

    [Fact]
    public void Item_5309_reads_Regal_without_being_asked_for_a_rarity()
    {
        var pants = Store.All(EntityType.Item, 5309)[0];

        var cut = Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, pants));

        Assert.Equal("Regal", cut.Find("span.rar").TextContent);
        Assert.Equal("315", cut.Find("span.power").TextContent);

        Assert.Equal(4, cut.FindAll("ul.stats li.guaranteed").Count);
        Assert.Equal(2, cut.FindAll("ul.stats li.random").Count);

        var first = cut.FindAll("ul.stats li")[0];

        Assert.Equal("Armor", first.QuerySelectorAll("span")[^1].TextContent);
        Assert.Equal("+89", first.QuerySelector("span.v")!.TextContent);
    }

    [Fact]
    public void A_legendary_is_answered_at_the_level_a_legendary_drop_pays()
    {
        var legendary = Store.All(EntityType.Item, 5224)[0];

        var cut = Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, legendary));

        Assert.Equal("Legendary", cut.Find("span.rar").TextContent);
        Assert.Equal("360", cut.Find("span.power").TextContent);
    }

    [Fact]
    public void Item_5309_tempers_eight_times_without_being_asked_for_a_rarity()
    {
        var pants = Store.All(EntityType.Item, 5309)[0];

        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, pants)
            .Add(x => x.Query, new TooltipQuery { Upgrades = 8 }));

        Assert.Equal("Tempered8 / 8", cut.FindAll("div.row")[^1].QuerySelectorAll(":scope > span")[^1].TextContent);
    }

    [Fact]
    public void Every_id_in_the_corpus_resolves_through_all_and_through_its_wearers_name()
    {
        var ids = Store.Entities
            .Select(document => (Type: EntityTypes.TypeOf(document), document.Id))
            .Distinct()
            .ToArray();

        Assert.NotEmpty(ids);

        foreach (var (type, id) in ids)
        {
            var readings = Store.All(type, id);
            Assert.NotEmpty(readings);

            var heroName = Store.Wearer(readings[0])?.Name;

            var outcome = Store.Resolve(type, id, heroName, out var document, out _);

            Assert.Equal(Lookup.Found, outcome);
            Assert.NotNull(document);
        }
    }

    [Fact]
    public void Every_wholly_resolvable_row_renders_no_unresolved_token()
    {
        var withPlaceholders = Store.Entities
            .Where(document => Tokens(document).Count > 0)
            .ToArray();

        Assert.NotEmpty(withPlaceholders);

        var blank = withPlaceholders.Where(document => Tokens(document).Any(IsBlank)).ToArray();
        var resolvable = withPlaceholders.Except(blank).ToArray();

        Assert.NotEmpty(resolvable);
        Assert.NotEmpty(blank);

        foreach (var document in resolvable)
        {
            var cut = Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, document));

            Assert.DoesNotContain("class=\"tok\"", cut.Markup, StringComparison.Ordinal);
        }
    }

    private static bool IsBlank(Placeholder token) => token is { Text: null, Coefficient: null, Value: null };

    private static IReadOnlyList<Placeholder> Tokens(EntityDocument document) =>
        document is GemTypeDocument { OverflowPlaceholders: { } overflow } ? overflow : CodexView.PlaceholdersOf(document);

    [Fact]
    public void Ability_1855_spends_BrillFlashHeal_twice_carrying_the_same_number()
    {
        var ability = Assert.Single(Store.All(EntityType.Ability, 1855));

        var cut = Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, ability));

        var numbers = cut.FindAll("p.desc span.sub").Select(e => e.TextContent).ToArray();

        Assert.Equal(3, numbers.Length);
        Assert.Equal(numbers[0], numbers[2]);
    }
}
