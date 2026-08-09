namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class PlaceholderReadingTests(Export export)
{
    private readonly Placeholders Readings =
        new(export.Store, new RichTextRenderer(export.Store.Vocabulary.TextStyles));

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
    public void Trait_power_684_reads_the_armor_it_adds_rather_than_the_armor_it_leaves()
    {
        var oak = Assert.Single(export.Store.All(EntityType.TraitPower, 684));
        var token = Assert.Single(CodexView.PlaceholdersOf(oak));

        Assert.Equal("35%", Read(token).Text);
    }

    [Fact]
    public void The_substitutions_panel_reads_the_same_increase_the_sentence_does()
    {
        var oak = Assert.Single(export.Store.All(EntityType.TraitPower, 684));

        Assert.Equal("35%", Assert.Single(export.View.Substitutions(oak)).Reading);
    }
}
