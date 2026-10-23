namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class PlaceholderReadingTests(Export export)
{
    private readonly IPlaceholderService Readings =
        new PlaceholderService(export.Store, new RichTextService(export.Store.Settings.TextStyles));

    private Substitution Read(Placeholder placeholder) =>
        Readings.Reading(placeholder, new TooltipQuery(), null);

    [Fact]
    public void A_multiplying_constant_reads_as_the_increase_it_grants()
    {
        Assert.Equal("35%", Read(new Placeholder { Value = 1.35, Percent = true, Multiplies = true }).Text);
        Assert.Equal("15%", Read(new Placeholder { Value = 1.15, Percent = true, Multiplies = true }).Text);
    }

    [Fact]
    public void A_share_that_multiplies_nothing_still_reads_as_the_whole_of_itself()
    {
        Assert.Equal("30%", Read(new Placeholder { Value = 0.3, Percent = true }).Text);
        Assert.Equal("275%", Read(new Placeholder { Value = 2.75, Percent = true }).Text);
        Assert.Equal("28%", Read(new Placeholder { Value = 0.28, Percent = true }).Text);
    }

    [Fact]
    public void A_delta_constant_reads_as_the_signed_excess_it_carries_over_1()
    {
        Assert.Equal("+2%", Read(new Placeholder { Value = 1.02, Format = "delta" }).Text);
        Assert.Equal("+5%", Read(new Placeholder { Value = 1.05, Format = "delta" }).Text);
        Assert.Equal("+0.6%", Read(new Placeholder { Value = 1.006, Format = "delta" }).Text);
    }

    [Fact]
    public void A_per_rank_value_reads_at_the_equipped_rank_or_one_item_at_the_queried_level()
    {
        var perRank = new Placeholder { Value = 1, PerRank = true };

        Assert.Equal("10", Readings.Reading(perRank, new TooltipQuery { EquippedTier = 10 }, null).Text);
        Assert.Equal("5", Readings.Reading(perRank, new TooltipQuery { ItemLevel = 200 }, null).Text);
    }

    [Fact]
    public void A_delta_constant_below_1_reads_as_the_reduction_it_carries()
    {
        Assert.Equal("-8%", Read(new Placeholder { Value = 0.92, Format = "delta" }).Text);
    }

    [Fact]
    public void Gem_augmentation_332_reads_the_crit_chance_it_grants_rather_than_its_multiplier()
    {
        var instinct = Assert.Single(export.Store.All(EntityType.GemAbility, 332));
        var token = Assert.Single(CodexView.PlaceholdersOf(instinct));

        Assert.Equal("+2%", Read(token).Text);
    }

    [Fact]
    public void The_substitutions_panel_reads_the_same_grant_gem_augmentation_332_speaks()
    {
        var instinct = Assert.Single(export.Store.All(EntityType.GemAbility, 332));

        Assert.Equal("+2%", Assert.Single(export.View.Substitutions(instinct)).Reading);
    }

    [Fact]
    public void Augmentation_power_684_reads_the_armor_it_adds_rather_than_the_armor_it_leaves()
    {
        var oak = Assert.Single(export.Store.All(EntityType.ProtectiveEnchant, 62));
        var token = Assert.Single(CodexView.PlaceholdersOf(oak));

        Assert.Equal("25%", Read(token).Text);
    }

    [Fact]
    public void The_substitutions_panel_reads_the_same_increase_the_sentence_does()
    {
        var oak = Assert.Single(export.Store.All(EntityType.ProtectiveEnchant, 62));

        Assert.Equal("25%", Assert.Single(export.View.Substitutions(oak)).Reading);
    }
}
