namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class TalentPlanTests(Export export)
{
    private IReadOnlyList<TalentRow> Tree(Hero hero) => TalentPlan.Tree(export.Store, hero);

    [Fact]
    public void Every_hero_lays_out_a_complete_tree()
    {
        var slots = export.Store.Entities.OfType<TalentSlotDocument>().Count();
        var laid = 0;

        foreach (var hero in Enum.GetValues<Hero>())
        {
            var tree = Tree(hero);

            Assert.NotEmpty(tree);
            Assert.All(tree, row => Assert.NotEmpty(row.Nodes));

            laid += tree.Sum(row => row.Nodes.Count);
        }

        Assert.Equal(slots, laid);
    }

    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(6, "VI")]
    [InlineData(9, "IX")]
    [InlineData(14, "XIV")]
    [InlineData(40, "XL")]
    public void A_row_number_prints_as_its_roman_numeral(int number, string numeral) =>
        Assert.Equal(numeral, TalentPlan.Numeral(number));
}
