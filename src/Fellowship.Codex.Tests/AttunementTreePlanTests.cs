namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class AttunementTreePlanTests(Export export)
{
    private string Printed(string type) => ItemPower.Printed(export.Store.Settings.Items, type)!;

    private AttunementTreeEntry Tree => AttunementTrees.BySlot(export.Store)
        .Single(entry => entry.Slot == Printed(ItemPower.WeaponType))
        .Tree;

    private AugmentationDocument In(int node, int skip = 0) =>
        AttunementTrees.Offered(Tree.Nodes.Single(each => each.UITreeIndex == node), export.Store)[skip];

    private string Picks(params (int Node, AugmentationDocument Augmentation)[] picks) =>
        string.Join(',', picks.Select(pick => $"{pick.Node}.{pick.Augmentation.Id}"));

    [Fact]
    public void A_path_up_the_tree_reads_back_as_written()
    {
        var picks = Picks((1, In(1)), (3, In(3)), (6, In(6, 1)));

        var read = AttunementTrees.Read(picks, Tree, export.Store);

        Assert.Equal([1, 3, 6], read.Keys.Order());
        Assert.Equal(picks, AttunementTrees.Write(read));
    }

    [Fact]
    public void A_node_whose_parents_are_all_empty_is_dropped() =>
        Assert.Empty(AttunementTrees.Read(Picks((3, In(3))), Tree, export.Store));

    [Fact]
    public void A_row_takes_one_trait() =>
        Assert.Equal([1], AttunementTrees.Read(Picks((1, In(1)), (2, In(2, 1))), Tree, export.Store).Keys);

    [Fact]
    public void A_augmentation_infused_once_is_not_infused_again() =>
        Assert.Equal([1, 3], AttunementTrees.Read(Picks((1, In(1)), (3, In(3)), (6, In(1))), Tree, export.Store).Keys.Order());

    [Fact]
    public void A_augmentation_outside_the_node_category_is_dropped() =>
        Assert.Empty(AttunementTrees.Read(Picks((1, In(3))), Tree, export.Store));

    [Fact]
    public void Removing_a_node_drops_every_node_infused_through_it()
    {
        var read = AttunementTrees.Read(Picks((1, In(1)), (3, In(3)), (6, In(6, 1))), Tree, export.Store);
        Assert.Equal(3, read.Count);

        var asked = new Dictionary<int, AugmentationDocument>(read);
        asked.Remove(1);

        Assert.Empty(AttunementTrees.Allocated(Tree, asked));
    }

    [Fact]
    public void Each_slot_with_a_attunement_tree_names_one_tree()
    {
        var bySlot = AttunementTrees.BySlot(export.Store);

        Assert.Equal(
            [Printed(ItemPower.NecklaceType), Printed(ItemPower.WeaponType)],
            bySlot.Select(entry => entry.Slot));

        Assert.All(bySlot, entry => Assert.Same(entry.Tree, AttunementTrees.Find(entry.Tree.Tag, export.Store.Settings)));
    }

    [Fact]
    public void Every_attunement_tree_item_names_a_tree_with_a_layout() =>
        Assert.All(
            AttunementTrees.Items(export.Store),
            item => Assert.NotNull(AttunementTrees.Find(item.AttunementTree, export.Store.Settings)?.Layout));
}
