namespace Fellowship.Codex.Tests;

[Collection(Readings25647984.Name)]
public sealed class ReadingTests25647984(Export25647984 export)
{
    private const int NoorTheBetrayer = 168;

    private const double NoorAtEternal44 = 12_365_031;

    private SettingsFile Settings => export.Store.Settings;

    [Theory]
    [InlineData("ItemTraitTrees.EASeason3Weapon", 5)]
    [InlineData("ItemTraitTrees.EASeason3Necklace", 2)]
    public void A_trait_tree_imbues_one_trait_per_row_below_its_root(string tree, int capacity) =>
        Assert.Equal(capacity, TraitTrees.Capacity(tree, Settings));

    [Fact]
    public void Noor_at_Eternal_44_reads_within_a_hundredth_of_a_percent_of_the_figure_the_game_showed()
    {
        var eternal = Settings.Leagues.Single(league => league.Kind == LeagueKind.Eternal);
        var noor = export.Store.Entities.OfType<CreatureDocument>().Single(row => row.Id == NoorTheBetrayer);

        var health = Difficulties.Health(noor, Settings, eternal.MinimumDifficulty + 44);

        Assert.NotNull(health);
        Assert.Equal(NoorAtEternal44, health.Value, NoorAtEternal44 * 0.0001);
        Assert.Equal(12_364_857d, health.Value, 1d);
    }
}
