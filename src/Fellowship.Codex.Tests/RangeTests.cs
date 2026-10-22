namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class RangeTests(Export export)
{
    private AbilityTooltipText Text => TooltipView.AbilityText(export.Store.Settings);

    private TooltipView.Reading RangeOf(AbilityDocument ability) =>
        export.Tooltip.Overview(ability, new TooltipQuery()).CastAndRange
            .Single(reading => reading.Label == Text.Range || reading.Label == Text.MeleeRange || reading.Label == Text.Self);

    [Fact]
    public void The_widget_numbers_and_words_are_read_from_the_settings()
    {
        Assert.Equal(100, Text.UnitsPerYard);
        Assert.Equal(250, Text.MeleeMaximum);
        Assert.Equal("range", Text.Range);
        Assert.Equal("Melee Range", Text.MeleeRange);
        Assert.Equal("Self", Text.Self);
        Assert.Equal("yd", Text.RangeUnit);
        Assert.Equal("Instant", Text.Instant);
    }

    [Fact]
    public void Web_crack_reads_ten_yards_at_a_hundred_units_to_the_yard()
    {
        var crack = export.Store.Entities.OfType<AbilityDocument>().Single(ability => ability.Name == "Web Crack");

        Assert.Equal(1000, crack.Range);

        var reading = RangeOf(crack);

        Assert.Equal("10", reading.Value);
        Assert.Equal(Text.RangeUnit, reading.Unit);
        Assert.Equal(Text.Range, reading.Label);
    }

    [Fact]
    public void A_range_within_melee_reach_reads_melee_range_with_no_number()
    {
        var melee = export.Store.Entities.OfType<AbilityDocument>()
            .First(ability => ability.Range is > 0 && ability.Range <= Text.MeleeMaximum);

        var reading = RangeOf(melee);

        Assert.Null(reading.Value);
        Assert.Null(reading.Unit);
        Assert.Equal(Text.MeleeRange, reading.Label);
    }

    [Fact]
    public void A_range_read_as_zero_reads_self()
    {
        var self = export.Store.Entities.OfType<AbilityDocument>().First(ability => ability.Range == 0);

        var reading = RangeOf(self);

        Assert.Null(reading.Value);
        Assert.Equal(Text.Self, reading.Label);
    }

    [Fact]
    public void The_first_range_past_melee_reach_rounds_to_three_yards()
    {
        Assert.False(Distance.Melee(Text.MeleeMaximum + 1, Text));
        Assert.Equal(3, Distance.Yards(Text.MeleeMaximum + 1, Text));
    }
}
