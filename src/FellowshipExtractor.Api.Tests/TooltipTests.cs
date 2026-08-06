namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TooltipTests : BunitContext
{
    private readonly EntityStore Store;

    public TooltipTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
    }

    private IRenderedComponent<Components.Tooltip.Tooltip> Draw(Entity entity) =>
        Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, entity));

    [Fact]
    public void Carries_its_own_stylesheet_and_no_scope_attribute()
    {
        var cut = Draw(new Entity { Description = "burns" });

        Assert.DoesNotMatch(@"\sb-[a-z0-9]{10}", cut.Markup);

        var style = cut.Find("div.fx-tip > style").TextContent;

        Assert.Contains(".fx-tip .desc{white-space:pre-line}", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_the_stylesheet_as_css_rather_than_as_encoded_text()
    {
        var cut = Draw(new Entity { Description = "burns" });

        var style = cut.Find("div.fx-tip > style").InnerHtml;

        Assert.DoesNotContain("&quot;", style, StringComparison.Ordinal);
        Assert.DoesNotContain("&#xA;", style, StringComparison.Ordinal);
        Assert.Contains("\"Noto Sans\"", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_the_row_in_its_head()
    {
        var cut = Draw(new Entity { DisplayName = "Frosted Cloak" });

        Assert.Equal("Frosted Cloak", cut.Find("header.head h3.title").TextContent);
    }

    [Fact]
    public void Draws_no_body_when_the_row_states_nothing()
    {
        var cut = Draw(new Entity { DisplayName = "Bare" });

        Assert.Empty(cut.FindAll("div.lines"));
    }

    [Fact]
    public void Rules_between_blocks_and_never_above_the_first()
    {
        var entity = new Entity
        {
            Slot = "Back",
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
        var cut = Draw(new Entity { Description = "burns" });

        Assert.Single(cut.FindAll("div.grp"));
        Assert.Empty(cut.FindAll("hr.rule"));
    }

    [Fact]
    public void Renders_a_description_with_no_whitespace_of_its_own()
    {
        var cut = Draw(new Entity { Description = "one two" });

        Assert.Equal("<p class=\"desc\">one two</p>", cut.Find("p.desc").OuterHtml.Bare());
    }

    [Fact]
    public void Renders_a_rich_text_run_inside_the_description()
    {
        var cut = Draw(new Entity { Description = "grants you <rt.warning>+3%</> haste" });

        var run = cut.Find("p.desc span.rt");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("desc", run.ParentElement.ClassList);
    }

    [Fact]
    public void States_its_scalars_two_to_a_row()
    {
        var entity = new Entity { Cooldown = 30, Range = 25, Duration = 8 };

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
        var entity = new Entity
        {
            Grants = new Entity { DisplayName = "Sunder", Description = "cleaves" },
        };

        var cut = Draw(entity);

        Assert.Equal("Ability: Sunder", cut.Find("div.sec.ability span").TextContent);
        Assert.Equal("cleaves", cut.Find("p.desc").TextContent);
    }

    [Fact]
    public void Counts_the_legendary_rolls_it_draws()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { DisplayName = "First", Description = "one" },
                new Entity { DisplayName = "Second", Description = "two" },
            ],
        };

        var cut = Draw(entity);

        Assert.Equal("(2)", cut.Find("div.sec.legendary span.n").TextContent);
        Assert.Equal(2, cut.FindAll("p.nm").Count);
    }

    [Fact]
    public void Counts_the_pieces_a_set_bonus_asks_for()
    {
        var entity = new Entity { SetBonus = ["Ironhide", "Emberweave"], SetCount = 4 };

        var cut = Draw(entity);

        Assert.Equal("(4 pieces)", cut.Find("div.sec.setbonus span.n").TextContent);
        Assert.Equal("Ironhide, Emberweave", cut.Find("div.grp p.desc").TextContent);
    }

    [Fact]
    public void Prints_a_gem_rung_under_the_gem_ladders_word()
    {
        var rung = Store.Vocabulary.Rarities[^1];

        var asGem = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new Entity { GemTier = 3 })
            .Add(x => x.Query, new TooltipQuery { Rarity = rung.Name }));

        var asItem = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new Entity { Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { Rarity = rung.Name }));

        Assert.Equal(rung.GemDisplayName, asGem.Find("span.rar").TextContent);
        Assert.Equal(rung.DisplayName, asItem.Find("span.rar").TextContent);
    }

    [Fact]
    public void Prints_the_item_level_the_caller_asked_for()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new Entity { Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { ItemLevel = 400 }));

        Assert.Equal("400", cut.Find("span.power").TextContent);
    }
}
