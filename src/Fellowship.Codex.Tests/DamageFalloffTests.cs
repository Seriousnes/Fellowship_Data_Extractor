namespace Fellowship.Codex.Tests;

public class DamageFalloffTests
{
    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 5)]
    [InlineData(8, 8)]
    public void Targets_up_to_the_threshold_take_full_damage(double threshold, int targets)
    {
        var split = DamageFalloff.Split(threshold, null, 1000, targets);

        Assert.Equal(1, split.Scaler);
        Assert.Equal(1000, split.PerTarget);
        Assert.Equal(1000 * targets, split.Total);
    }

    [Fact]
    public void Targets_past_the_threshold_scale_by_the_root_of_threshold_over_targets()
    {
        var split = DamageFalloff.Split(8, null, 1000, 20);

        Assert.Equal(0.632455532, split.Scaler, 9);
        Assert.Equal(632.455532, split.PerTarget, 6);
        Assert.Equal(12649.11064, split.Total, 5);
    }

    [Fact]
    public void A_zero_threshold_scales_every_target_to_nothing()
    {
        var split = DamageFalloff.Split(0, null, 1000, 3);

        Assert.Equal(0, split.Threshold);
        Assert.Equal(0, split.Scaler);
        Assert.Equal(0, split.Total);
    }

    [Fact]
    public void No_targets_deal_no_damage()
    {
        var split = DamageFalloff.Split(8, null, 1000, 0);

        Assert.Equal(1, split.Scaler);
        Assert.Equal(0, split.Total);
    }

    [Fact]
    public void Max_targets_caps_the_targets_hit()
    {
        var split = DamageFalloff.Split(4, 6, 1000, 20);

        Assert.Equal(6, split.Targets);
        Assert.Equal(Math.Sqrt(4.0 / 6), split.Scaler, 12);
        Assert.Equal(6 * 1000 * Math.Sqrt(4.0 / 6), split.Total, 9);
    }

    [Fact]
    public void A_zero_cap_hits_no_targets()
    {
        var split = DamageFalloff.Split(8, 0, 1000, 20);

        Assert.Equal(0, split.Targets);
        Assert.Equal(0, split.Total);
    }

    [Fact]
    public void A_large_cap_leaves_the_targets_entered_alone() =>
        Assert.Equal(20, DamageFalloff.Split(8, 999999, 1000, 20).Targets);

    [Fact]
    public void Max_targets_above_the_targets_entered_leaves_them_alone() =>
        Assert.Equal(5, DamageFalloff.Split(8, 12, 1000, 5).Targets);

    [Theory]
    [InlineData(8.9, 8)]
    [InlineData(8.0, 8)]
    [InlineData(0.5, 0)]
    [InlineData(-3, 0)]
    public void The_threshold_truncates_to_a_whole_target_count(double threshold, int whole)
    {
        Assert.Equal(whole, DamageFalloff.Split(threshold, null, 1000, 1).Threshold);
    }

    [Fact]
    public void A_truncated_threshold_decides_the_scaler()
    {
        var split = DamageFalloff.Split(8.9, null, 1000, 9);

        Assert.Equal(Math.Sqrt(8.0 / 9), split.Scaler, 12);
    }
}
