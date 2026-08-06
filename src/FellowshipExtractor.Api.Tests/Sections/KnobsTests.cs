namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class KnobsTests : BunitContext
{
    private readonly Export export;

    public KnobsTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private Entity AnyHero() => export.Store.Vocabulary.Heroes[0] is { } who
        ? new Entity { Heroes = [who.Name], ArmorType = who.ArmorType, PrimaryStat = who.PrimaryStat }
        : new Entity();

    [Fact]
    public void Renders_nothing_for_a_record_with_no_knob_to_turn()
    {
        var cut = Render<Knobs>(p => p.AddCascadingValue(new Bearings()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Navigates_the_whole_page_when_the_hero_changes()
    {
        var here = new Bearings { Shown = AnyHero(), Readings = [AnyHero(), AnyHero()] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        var select = cut.Find("select[data-knob=hero]");

        Assert.Equal(
            "location.search = '?hero=' + encodeURIComponent(this.value)",
            select.GetAttribute("onchange"));
    }

    [Fact]
    public void Renders_every_rarity_rung_as_an_unpressed_button()
    {
        var wearable = new Entity { Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] };
        var here = new Bearings { Shown = wearable, Readings = [wearable] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        var rungs = cut.FindAll("button[data-knob=rarity]");

        Assert.Equal(export.Store.Vocabulary.Rarities.Count, rungs.Count);
        Assert.All(rungs, rung =>
        {
            Assert.Equal("button", rung.GetAttribute("type"));
            Assert.False(string.IsNullOrEmpty(rung.GetAttribute("data-choice")));
            Assert.Equal("false", rung.GetAttribute("aria-pressed"));
        });
    }

    [Fact]
    public void Renders_both_number_inputs_under_the_names_the_tooltip_api_reads()
    {
        var wearable = new Entity { Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] };
        var here = new Bearings { Shown = wearable, Readings = [wearable] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        Assert.NotNull(cut.Find("input[data-knob=itemLevel]"));
        Assert.NotNull(cut.Find("input[data-knob=upgrades]"));
        Assert.Equal(
            StatMagnitudes.MaximumItemLevel.ToString(),
            cut.Find("input[data-knob=itemLevel]").GetAttribute("value"));
    }

    [Fact]
    public void Offers_an_any_option_only_where_the_id_has_one_reading()
    {
        var heroes = export.Store.Vocabulary.Heroes.Take(2).Select(h => h.Name).ToList();
        var shared = new Entity { Heroes = heroes };

        var one = Render<Knobs>(p => p.AddCascadingValue(
            new Bearings { Shown = shared, Readings = [shared] }));

        Assert.Contains(one.FindAll("option"), o => o.GetAttribute("value") == string.Empty);

        var two = Render<Knobs>(p => p.AddCascadingValue(
            new Bearings { Shown = shared, Readings = [shared, shared] }));

        Assert.DoesNotContain(two.FindAll("option"), o => o.GetAttribute("value") == string.Empty);
    }

    [Fact]
    public void Selects_the_hero_whose_reading_is_on_the_stage()
    {
        var who = export.Store.Vocabulary.Heroes[0];
        var reading = new Entity
        {
            Heroes = [who.Name],
            ArmorType = who.ArmorType,
            PrimaryStat = who.PrimaryStat,
        };

        var here = new Bearings { Shown = reading, Readings = [reading, reading], Reading = who.Name };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        var selected = cut.FindAll("option").Single(o => o.HasAttribute("selected"));

        Assert.Equal(who.Name, selected.GetAttribute("value"));
    }

    [Fact]
    public void Names_the_gem_ladder_for_a_gem_and_the_item_ladder_otherwise()
    {
        var gem = new Entity { GemTier = 3 };
        var wearable = new Entity { Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] };

        var asGem = Render<Knobs>(p => p.AddCascadingValue(
            new Bearings { Shown = gem, Readings = [gem] }));
        var asItem = Render<Knobs>(p => p.AddCascadingValue(
            new Bearings { Shown = wearable, Readings = [wearable] }));

        var rung = export.Store.Vocabulary.Rarities[0];

        Assert.Equal(rung.GemDisplayName, asGem.FindAll("button[data-knob=rarity]")[0].TextContent.Trim());
        Assert.Equal(rung.DisplayName, asItem.FindAll("button[data-knob=rarity]")[0].TextContent.Trim());
    }
}
