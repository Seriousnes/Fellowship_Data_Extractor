namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class RandomStatKnobTests : BunitContext
{
    private readonly Export export;

    public RandomStatKnobTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private static ItemDocument Wearable() =>
        new()
        {
            Id = 1,
            Stats = [new StatSlot("Stamina", 40) { Kind = SlotKinds.Fixed }],
            Rarities = [Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Champion, Rarity.Heroic],
        };

    private IRenderedComponent<Knobs> Panel(params (string Key, string Value)[] values)
    {
        var wearable = Wearable();

        var here = new Bearings
        {
            Entity = wearable,
            Readings = [wearable],
            Values = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        };

        return Render<Knobs>(p => p.AddCascadingValue(here));
    }

    [Fact]
    public void Offers_a_random_stats_box_for_a_wearable()
    {
        var box = Panel().Find("input[data-knob=randomStats]");

        Assert.Equal("text", box.GetAttribute("type"));
        Assert.Contains(
            export.Store.Settings.StatCalculation.RandomStatPool[0],
            box.GetAttribute("placeholder"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Draws_nothing_for_a_record_that_wears_no_stats() =>
        Assert.Empty(Render<Knobs>(p => p.AddCascadingValue(new Bearings())).Markup);

    [Fact]
    public void Carries_the_random_stats_the_address_asked_for() =>
        Assert.Equal(
            "Stamina,Expertise,Expertise",
            Panel(("randomStats", "Stamina,Expertise,Expertise")).Find("input[data-knob=randomStats]")
                .GetAttribute("value"));

    [Fact]
    public void Carries_the_item_level_and_the_tempers_the_address_asked_for()
    {
        var panel = Panel(("itemLevel", "360"), ("upgrades", "8"));

        Assert.Equal("360", panel.Find("input[data-knob=itemLevel]").GetAttribute("value"));
        Assert.Equal("8", panel.Find("input[data-knob=upgrades]").GetAttribute("value"));
    }

    [Fact]
    public void Presses_the_rarity_the_address_asked_for()
    {
        var rarity = export.Store.Settings.Rarities[^2];

        var pressed = Panel(("rarity", rarity.Name))
            .FindAll("button[data-knob=rarity]")
            .Where(button => button.GetAttribute("aria-pressed") == "true")
            .ToArray();

        Assert.Single(pressed);
        Assert.Equal(rarity.Tier.ToString(), pressed[0].GetAttribute("data-value"));
    }

    [Fact]
    public void Leaves_every_rarity_unpressed_when_the_address_names_none() =>
        Assert.All(
            Panel().FindAll("button[data-knob=rarity]"),
            button => Assert.Equal("false", button.GetAttribute("aria-pressed")));
}
