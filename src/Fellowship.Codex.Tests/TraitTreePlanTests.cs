namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class TraitTreePlanTests(Export export)
{
    private const string Weapon = "ItemTraitTrees.EASeason3Weapon";

    private TraitTreeEntry Tree => TraitTrees.Find(Weapon, export.Store.Settings)!;

    private TraitDocument In(int node, int skip = 0) =>
        TraitTrees.Offered(Tree.Nodes.Single(each => each.UITreeIndex == node), export.Store)[skip];

    private string Picks(params (int Node, TraitDocument Trait)[] picks) =>
        string.Join(',', picks.Select(pick => $"{pick.Node}.{pick.Trait.Id}"));

    [Fact]
    public void A_path_up_the_tree_reads_back_as_written()
    {
        var picks = Picks((1, In(1)), (3, In(3)), (6, In(6, 1)));

        var read = TraitTrees.Read(picks, Tree, export.Store);

        Assert.Equal([1, 3, 6], read.Keys.Order());
        Assert.Equal(picks, TraitTrees.Write(read));
    }

    [Fact]
    public void A_node_whose_parents_are_all_empty_is_dropped() =>
        Assert.Empty(TraitTrees.Read(Picks((3, In(3))), Tree, export.Store));

    [Fact]
    public void A_row_takes_one_trait() =>
        Assert.Equal([1], TraitTrees.Read(Picks((1, In(1)), (2, In(2, 1))), Tree, export.Store).Keys);

    [Fact]
    public void A_trait_infused_once_is_not_infused_again() =>
        Assert.Equal([1, 3], TraitTrees.Read(Picks((1, In(1)), (3, In(3)), (6, In(1))), Tree, export.Store).Keys.Order());

    [Fact]
    public void A_trait_outside_the_node_category_is_dropped() =>
        Assert.Empty(TraitTrees.Read(Picks((1, In(3))), Tree, export.Store));

    [Fact]
    public void Removing_a_node_drops_every_node_infused_through_it()
    {
        var read = TraitTrees.Read(Picks((1, In(1)), (3, In(3)), (6, In(6, 1))), Tree, export.Store);
        Assert.Equal(3, read.Count);

        var asked = new Dictionary<int, TraitDocument>(read);
        asked.Remove(1);

        Assert.Empty(TraitTrees.Allocated(Tree, asked));
    }

    [Fact]
    public void Every_trait_tree_item_names_a_tree_with_a_layout() =>
        Assert.All(
            TraitTrees.Items(export.Store),
            item => Assert.NotNull(TraitTrees.Find(item.TraitTree, export.Store.Settings)?.Layout));
}
