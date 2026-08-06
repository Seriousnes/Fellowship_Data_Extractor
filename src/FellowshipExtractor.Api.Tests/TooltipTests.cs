using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The one component both readers of a tooltip go through: the entity page places it, and the API
/// renders it to a string for callers who have no page.
///
/// Granted and legendary rolls resolve against the store rather than nesting a bare record, so
/// those two tests read real corpus rows: item 5246 (Humming Portalstone) grants relic power 432
/// (Conjure Portal), and item 5216 carries the three legendary stat ids 696, 695 and 694.
/// </summary>
[Collection(Corpus.Name)]
public class TooltipTests : BunitContext
{
    private readonly EntityStore Store;

    public TooltipTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
    }

    private IRenderedComponent<Components.Tooltip.Tooltip> Draw(EntityDocument document) =>
        Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, document));

    /// <summary>
    /// The fragment is painted into a shadow root that no stylesheet of this app enters, and is
    /// served to callers who have no page at all, so it carries its whole stylesheet inside it and
    /// owns no scoped sheet. A .razor.scss beside this component would bake scope attributes into
    /// markup whose rules never load, and every hover tooltip would draw unstyled.
    /// </summary>
    [Fact]
    public void Carries_its_own_stylesheet_and_no_scope_attribute()
    {
        var cut = Draw(new Ability { Id = 1, Description = "burns" });

        Assert.DoesNotMatch(@"\sb-[a-z0-9]{10}", cut.Markup);

        var style = cut.Find("div.fx-tip > style").TextContent;

        Assert.Contains(".fx-tip .desc{white-space:pre-line}", style, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stylesheet reaches the page as CSS, not as encoded text.
    ///
    /// A string interpolated into markup is HTML-encoded, which turns every font-family quote into
    /// &amp;quot; and every newline into &amp;#xA;. The sheet still loads and still parses, and the
    /// declarations carrying a quoted family silently stop applying, so the tooltip draws in the
    /// wrong face with a green build and every other assertion here still passing.
    /// </summary>
    [Fact]
    public void Writes_the_stylesheet_as_css_rather_than_as_encoded_text()
    {
        var cut = Draw(new Ability { Id = 1, Description = "burns" });

        var style = cut.Find("div.fx-tip > style").InnerHtml;

        Assert.DoesNotContain("&quot;", style, StringComparison.Ordinal);
        Assert.DoesNotContain("&#xA;", style, StringComparison.Ordinal);
        Assert.Contains("\"Noto Sans\"", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_the_row_in_its_head()
    {
        var cut = Draw(new Ability { Id = 1, Name = "Frosted Cloak" });

        Assert.Equal("Frosted Cloak", cut.Find("header.head h3.title").TextContent);
    }

    /// <summary>
    /// A row with nothing to say past its name draws no body at all, which is what the string
    /// renderer did by returning early when every block came back empty.
    /// </summary>
    [Fact]
    public void Draws_no_body_when_the_row_states_nothing()
    {
        var cut = Draw(new Ability { Id = 1, Name = "Bare" });

        Assert.Empty(cut.FindAll("div.lines"));
    }

    /// <summary>
    /// The separator goes between neighbours and never above the first block. Razor cannot ask
    /// whether a child rendered anything, so a naive port emits a leading rule or doubles them
    /// wherever a block is empty; this is the assertion that catches it.
    /// </summary>
    [Fact]
    public void Rules_between_blocks_and_never_above_the_first()
    {
        // Identity (an armour type), prose and a set bonus: three blocks, so two rules, none leading.
        var entity = new Item
        {
            Id = 1,
            ArmorType = ArmorType.Cloth,
            Description = "burns",
            SetBonus = ["Ironhide"],
        };

        var cut = Draw(entity);

        var lines = cut.Find("div.lines");

        Assert.Equal(3, lines.QuerySelectorAll(":scope > div.grp").Length);
        Assert.Equal(2, lines.QuerySelectorAll(":scope > hr.rule").Length);
        Assert.Equal("DIV", lines.FirstElementChild!.TagName);
        Assert.Contains("grp", lines.FirstElementChild.ClassList);
    }

    [Fact]
    public void Draws_one_block_with_no_rule_at_all()
    {
        var cut = Draw(new Ability { Id = 1, Description = "burns" });

        Assert.Single(cut.FindAll("div.grp"));
        Assert.Empty(cut.FindAll("hr.rule"));
    }

    /// <summary>
    /// .desc is white-space: pre-line, so a newline the source indentation contributed would be a
    /// rendered blank line. The paragraph holds the text and nothing else.
    /// </summary>
    [Fact]
    public void Renders_a_description_with_no_whitespace_of_its_own()
    {
        var cut = Draw(new Ability { Id = 1, Description = "one two" });

        Assert.Equal("<p class=\"desc\">one two</p>", cut.Find("p.desc").OuterHtml.Bare());
    }

    [Fact]
    public void Renders_a_rich_text_run_inside_the_description()
    {
        var cut = Draw(new Ability { Id = 1, Description = "grants you <rt.warning>+3%</> haste" });

        var run = cut.Find("p.desc span.rt");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("desc", run.ParentElement.ClassList);
    }

    [Fact]
    public void States_its_scalars_two_to_a_row()
    {
        var entity = new Ability { Id = 1, Cooldown = 30, Range = 25, Duration = 8 };

        var cut = Draw(entity);

        var rows = cut.FindAll("div.row");

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].QuerySelectorAll(":scope > span").Length);
        Assert.Single(rows[1].QuerySelectorAll(":scope > span"));
        Assert.Equal("Cooldown", rows[0].QuerySelector("span.k")!.TextContent);
        Assert.Equal("30s", rows[0].QuerySelector("span.v")!.TextContent);
    }

    [Fact]
    public void Names_a_granted_ability_beside_its_badge()
    {
        var entity = new Item { Id = 5246, GrantsId = 432 };

        var cut = Draw(entity);

        Assert.Equal("Ability: Conjure Portal", cut.Find("div.sec.ability span").TextContent);
        Assert.Contains("Conjures two interactable", cut.Find("p.desc").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_the_legendary_rolls_it_draws()
    {
        var entity = new Item { Id = 5216, LegendaryStatIds = [696, 695, 694] };

        var cut = Draw(entity);

        Assert.Equal("(3)", cut.Find("div.sec.legendary span.n").TextContent);
        Assert.Equal(3, cut.FindAll("p.nm").Count);
    }

    /// <summary>
    /// How many pieces a set bonus asks for is stated on the set bonus's own row now, not the
    /// item's - the typed model carries Pieces on SetBonus rather than SetCount on Item - so the
    /// badge this line draws is the plain "Set Bonus" heading with no count beside it.
    /// </summary>
    [Fact]
    public void Names_the_sets_a_piece_counts_towards_with_no_piece_count()
    {
        var entity = new Item { Id = 1, SetBonus = ["Ironhide", "Emberweave"] };

        var cut = Draw(entity);

        Assert.Equal("Set Bonus", cut.Find("div.sec.setbonus span").TextContent);
        Assert.Equal("Ironhide, Emberweave", cut.Find("div.grp p.desc").TextContent);
    }

    /// <summary>
    /// Gems and items print the same tier under different words, and which ladder a row reads is
    /// what its gem tier settles. That is domain vocabulary, not a formatting choice.
    /// </summary>
    [Fact]
    public void Prints_a_gem_rung_under_the_gem_ladders_word()
    {
        var rung = Store.Vocabulary.Rarities[^1];

        var asGem = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new GemTier { Id = 1, Gem = GemTypeName.Amethyst, Tier = 3, Power = 10, Rarity = Rarity.Champion })
            .Add(x => x.Query, new TooltipQuery { Rarity = rung.Name }));

        var asItem = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new Item { Id = 2, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { Rarity = rung.Name }));

        Assert.Equal(rung.GemDisplayName, asGem.Find("span.rar").TextContent);
        Assert.Equal(rung.DisplayName, asItem.Find("span.rar").TextContent);
    }

    /// <summary>The item power the line prints, which the caller can set and a stat-rolling row
    /// defaults to the top of the ladder.</summary>
    [Fact]
    public void Prints_the_item_level_the_caller_asked_for()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new Item { Id = 3, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { ItemLevel = 400 }));

        Assert.Equal("400", cut.Find("span.power").TextContent);
    }
}
