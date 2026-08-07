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

    private IRenderedComponent<Components.Tooltip.Tooltip> Draw(EntityDocument document) =>
        Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, document));

    [Fact]
    public void Carries_its_own_stylesheet_and_no_scope_attribute()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Description = "burns" });

        Assert.DoesNotMatch(@"\sb-[a-z0-9]{10}", cut.Markup);

        var style = cut.Find("div.fx-tip > style").TextContent;

        Assert.Contains($".{TooltipView.Root} .desc {{", style, StringComparison.Ordinal);
        Assert.Contains("white-space: pre-line", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_the_stylesheet_as_css_rather_than_as_encoded_text()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Description = "burns" });

        var style = cut.Find("div.fx-tip > style").InnerHtml;

        Assert.DoesNotContain("&quot;", style, StringComparison.Ordinal);
        Assert.DoesNotContain("&#xA;", style, StringComparison.Ordinal);
        Assert.Contains("\"Noto Sans\"", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_the_row_in_its_head()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Name = "Frosted Cloak" });

        Assert.Equal("Frosted Cloak", cut.Find("header.head h3.title").TextContent);
    }

    [Fact]
    public void Draws_no_body_when_the_row_states_nothing()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Name = "Bare" });

        Assert.Empty(cut.FindAll("div.lines"));
    }

    [Fact]
    public void Rules_between_blocks_and_never_above_the_first()
    {
        var entity = new ItemDocument
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
        var cut = Draw(new AbilityDocument { Id = 1, Description = "burns" });

        Assert.Single(cut.FindAll("div.grp"));
        Assert.Empty(cut.FindAll("hr.rule"));
    }

    [Fact]
    public void Renders_a_description_with_no_whitespace_of_its_own()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Description = "one two" });

        Assert.Equal("<p class=\"desc\">one two</p>", cut.Find("p.desc").OuterHtml.Bare());
    }

    [Fact]
    public void Renders_a_rich_text_run_inside_the_description()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Description = "grants you <rt.warning>+3%</> haste" });

        var run = cut.Find("p.desc span.rt");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("desc", run.ParentElement.ClassList);
    }

    [Fact]
    public void States_its_scalars_two_to_a_row()
    {
        var entity = new AbilityDocument { Id = 1, Cooldown = 30, Range = 25, Duration = 8 };

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
        var entity = new ItemDocument { Id = 5246, GrantsId = 432 };

        var cut = Draw(entity);

        Assert.Equal("Ability: Conjure Portal", cut.Find("div.sec.ability span").TextContent);
        Assert.Contains("Conjures two interactable", cut.Find("p.desc").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_the_legendary_rolls_it_draws()
    {
        var entity = new ItemDocument { Id = 5216, LegendaryStatIds = [696, 695, 694] };

        var cut = Draw(entity);

        Assert.Equal("(3)", cut.Find("div.sec.legendary span.n").TextContent);
        Assert.Equal(3, cut.FindAll("p.nm").Count);
    }

    [Fact]
    public void Names_the_sets_a_piece_counts_towards_with_no_piece_count()
    {
        var entity = new ItemDocument { Id = 1, SetBonus = ["Ironhide", "Emberweave"] };

        var cut = Draw(entity);

        Assert.Equal("Set Bonus", cut.Find("div.sec.setbonus span").TextContent);
        Assert.Equal("Ironhide, Emberweave", cut.Find("div.grp p.desc").TextContent);
    }

    [Fact]
    public void Prints_a_gem_rung_under_the_gem_ladders_word()
    {
        var rung = Store.Vocabulary.Rarities[^1];

        var asGem = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new GemDocument { Id = 1, Type = GemType.Amethyst, Power = 10, Rarity = Rarity.Champion })
            .Add(x => x.Query, new TooltipQuery { Rarity = rung.Name }));

        var asItem = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 2, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { Rarity = rung.Name }));

        Assert.Equal(rung.GemDisplayName, asGem.Find("span.rar").TextContent);
        Assert.Equal(rung.DisplayName, asItem.Find("span.rar").TextContent);
    }

    [Fact]
    public void Answers_an_item_at_the_highest_rung_it_comes_in_when_the_caller_names_none()
    {
        var cut = Draw(new ItemDocument
        {
            Id = 1,
            Rarities = [Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Champion, Rarity.Heroic],
        });

        Assert.Equal("Regal", cut.Find("span.rar").TextContent);
    }

    [Fact]
    public void Answers_an_item_fixed_at_a_rung_at_that_rung()
    {
        var legendary = Draw(new ItemDocument { Id = 1, Rarity = Rarity.Legendary, Rarities = [Rarity.Legendary] });
        var heroItem = Draw(new ItemDocument { Id = 2, Rarities = [Rarity.Common] });

        Assert.Equal("Legendary", legendary.Find("span.rar").TextContent);
        Assert.Equal("Common", heroItem.Find("span.rar").TextContent);
    }

    [Fact]
    public void Takes_the_rung_the_caller_names_over_the_one_the_item_comes_in()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 1, Rarities = [Rarity.Uncommon, Rarity.Heroic] })
            .Add(x => x.Query, new TooltipQuery { Rarity = "Uncommon" }));

        Assert.Equal("Uncommon", cut.Find("span.rar").TextContent);
    }

    [Fact]
    public void Colours_the_header_of_an_item_that_states_no_rung_of_its_own()
    {
        var coloured = Draw(new ItemDocument
        {
            Id = 1,
            Rarities = [Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Champion, Rarity.Heroic],
        });

        var style = coloured.Find("div.fx-tip").GetAttribute("style")!;

        var shade = new DesignTokens(Store.Vocabulary).Rarity(Rarity.Heroic.ToString());

        Assert.Contains($"--fx-title:{shade}", style, StringComparison.Ordinal);
        Assert.DoesNotContain("--fx-title:var(--core-text-primary)", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Answers_a_currency_at_the_rung_its_row_states()
    {
        var cut = Draw(new CurrencyDocument { Id = 1, Name = "Divine Mithril", Rarity = Rarity.Legendary });

        Assert.Equal("Legendary", cut.Find("span.rar").TextContent);
    }

    [Fact]
    public void Leaves_a_row_with_no_rarity_of_any_kind_unstyled()
    {
        var cut = Draw(new AbilityDocument { Id = 1, Name = "Fire Ball", Description = "burns" });

        var style = cut.Find("div.fx-tip").GetAttribute("style")!;

        Assert.Empty(cut.FindAll("span.rar"));
        Assert.Contains("--fx-title:var(--core-text-primary)", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Prints_the_item_level_the_caller_asked_for()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { ItemLevel = 400 }));

        Assert.Equal("400", cut.Find("span.power").TextContent);
    }

    [Fact]
    public void Prints_the_tempering_a_reading_can_take_beside_the_tempering_it_has()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery { Rarity = "Rare", ItemLevel = 315, Upgrades = 0 }));

        var line = cut.FindAll("div.row")[^1].QuerySelectorAll(":scope > span")[^1];

        Assert.Equal("Tempered", line.QuerySelector("span.temper")!.TextContent);
        Assert.Equal("Tempered0 / 5", line.TextContent);
    }

    [Fact]
    public void Prints_the_ceiling_the_caller_named_over_the_one_the_ladder_states()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] })
            .Add(x => x.Query, new TooltipQuery
            {
                Rarity = "Rare", ItemLevel = 315, Upgrades = 2, MaxUpgrades = 9,
            }));

        Assert.Equal("Tempered2 / 9", cut.FindAll("div.row")[^1].QuerySelectorAll(":scope > span")[^1].TextContent);
    }

    [Fact]
    public void Prints_the_power_potential_the_caller_asked_for()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Name = "Darkfrost Arms" })
            .Add(x => x.Query, new TooltipQuery { PotentialPower = 885 }));

        Assert.Equal("Power Potential", cut.Find("div.grp span.k").TextContent);
        Assert.Equal("885", cut.Find("span.pot").TextContent);
    }

    [Fact]
    public void Carries_the_armor_the_caller_gave_above_the_stats_it_works_out()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument
            {
                Id = 3,
                Slot = "Weapon",
                Stats = [new StatSlot("Stamina", 40) { Kind = SlotKinds.Fixed }],
            })
            .Add(x => x.Query, new TooltipQuery { Armor = 303, ItemLevel = 315 }));

        var lines = cut.FindAll("ul.stats li");

        Assert.Equal(2, lines.Count);
        Assert.Equal("+303", lines[0].QuerySelector("span.v")!.TextContent);
        Assert.Equal("Armor", lines[0].QuerySelectorAll("span")[^1].TextContent);
        Assert.Contains("guaranteed", lines[0].ClassList);
        Assert.Equal("Stamina", lines[1].QuerySelectorAll("span")[^1].TextContent);
    }

    [Fact]
    public void States_the_armor_of_an_item_that_declares_no_stat_slots()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Slot = "Chest" })
            .Add(x => x.Query, new TooltipQuery { Armor = 1052 }));

        Assert.Equal("+1052", cut.Find("ul.stats li span.v").TextContent);
    }

    [Fact]
    public void Names_every_imbued_trait_beside_the_rank_it_sits_at()
    {
        var trait = Store.Entities.OfType<TraitDocument>().First(imbued => imbued.Name is not null);

        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Name = "Darkfrost Arms" })
            .Add(x => x.Query, new TooltipQuery
            {
                Traits = [new ImbuedTrait(trait.Id, 1), new ImbuedTrait(trait.Id, 3)],
                AllocatedTraits = 5,
                MaxTraits = 5,
            }));

        var rows = cut.FindAll("span.trait");

        Assert.Equal(2, rows.Count);
        Assert.Equal($"Trait: {trait.Name} +1", rows[0].TextContent);
        Assert.Equal($"Trait: {trait.Name} +3", rows[1].TextContent);
    }

    [Fact]
    public void Counts_the_traits_imbued_against_the_traits_the_item_can_hold()
    {
        var counted = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Name = "Darkfrost Arms" })
            .Add(x => x.Query, new TooltipQuery { AllocatedTraits = 5, MaxTraits = 5 }));

        var uncapped = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Name = "Darkfrost Arms" })
            .Add(x => x.Query, new TooltipQuery { AllocatedTraits = 2 }));

        Assert.Equal("Imbued Traits", counted.Find("div.sec span").TextContent);
        Assert.Equal("(5/5)", counted.Find("div.sec span.n").TextContent);
        Assert.Equal("5", counted.Find("div.sec span.alloc").TextContent);
        Assert.Equal("(2)", uncapped.Find("div.sec span.n").TextContent);
    }

    [Fact]
    public void Counts_nothing_imbued_on_an_item_that_can_still_hold_traits()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Name = "Darkfrost Arms" })
            .Add(x => x.Query, new TooltipQuery { MaxTraits = 5 }));

        Assert.Equal("(0/5)", cut.Find("div.sec span.n").TextContent);
        Assert.Empty(cut.FindAll("span.trait"));
    }

    [Fact]
    public void Draws_the_traits_between_the_stats_and_the_prose()
    {
        var cut = Render<Components.Tooltip.Tooltip>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 3, Description = "burns" })
            .Add(x => x.Query, new TooltipQuery { Armor = 12, AllocatedTraits = 1 }));

        var groups = cut.Find("div.lines").QuerySelectorAll(":scope > div.grp");

        Assert.Equal(3, groups.Length);
        Assert.NotNull(groups[0].QuerySelector("ul.stats"));
        Assert.Equal("Imbued Traits", groups[1].QuerySelector("div.sec span")!.TextContent);
        Assert.Equal("burns", groups[2].QuerySelector("p.desc")!.TextContent);
    }

    [Fact]
    public void Draws_the_glyph_the_game_gives_that_slot()
    {
        foreach (var (slot, texture) in Store.Vocabulary.SlotIcons)
        {
            var cut = Draw(new ItemDocument { Id = 3, Slot = slot });

            Assert.EndsWith(
                $"/{ChromeAssets.Prefix}/{texture}.png",
                cut.Find("div.row img.gi").GetAttribute("src"),
                StringComparison.Ordinal);
        }
    }
}
