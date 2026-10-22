namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class RangeTests(Export export)
{
    private TooltipView.Reading RangeOf(AbilityDocument ability) =>
        export.Tooltip.Overview(ability, new TooltipQuery()).CastAndRange
            .Single(reading => reading.Label is TooltipView.RangeLabel or TooltipView.MeleeRange);

    [Fact]
    public void Web_crack_reads_ten_yards_at_a_hundred_units_to_the_yard()
    {
        var crack = export.Store.Entities.OfType<AbilityDocument>().Single(ability => ability.Name == "Web Crack");

        Assert.Equal(1000, crack.Range);

        var reading = RangeOf(crack);

        Assert.Equal("10", reading.Value);
        Assert.Equal(Distance.Unit, reading.Unit);
        Assert.Equal(TooltipView.RangeLabel, reading.Label);
    }

    [Fact]
    public void A_range_within_melee_reach_reads_melee_range_with_no_number()
    {
        var melee = export.Store.Entities.OfType<AbilityDocument>()
            .First(ability => ability.Range is > 0 and <= Distance.MeleeReach);

        var reading = RangeOf(melee);

        Assert.Null(reading.Value);
        Assert.Null(reading.Unit);
        Assert.Equal(TooltipView.MeleeRange, reading.Label);
    }

    [Fact]
    public void The_first_range_past_melee_reach_rounds_to_three_yards()
    {
        Assert.False(Distance.Melee(Distance.MeleeReach + 1));
        Assert.Equal(3, Distance.Yards(Distance.MeleeReach + 1));
    }
}
