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

    private AttunementTreePlan Plan(IReadOnlyList<int> path, params (int Node, AugmentationDocument Augmentation)[] assigned) =>
        AttunementTrees.Planned(
            Tree, null, null, assigned.ToDictionary(pick => pick.Node, pick => pick.Augmentation), path.ToHashSet());

    private AttunementTreePlan RoundTrip(AttunementTreePlan plan) =>
        AttunementTrees.Read(AttunementTrees.Write(Tree, plan), Tree, export.Store);

    [Fact]
    public void A_plan_reads_back_from_its_code()
    {
        var plan = Plan([1, 3, 6], (1, In(1)), (2, In(2, 1)), (3, In(3)), (5, In(5, 1)), (6, In(6, 2)), (12, In(12)));

        var read = RoundTrip(plan);

        Assert.Equal([1, 3, 6], read.Path.Order());
        Assert.Equal(
            plan.Assigned.OrderBy(pick => pick.Key).Select(pick => (pick.Key, pick.Value.Id)),
            read.Assigned.OrderBy(pick => pick.Key).Select(pick => (pick.Key, pick.Value.Id)));
    }

    private HeroDocument Hero(Hero hero) =>
        AttunementTrees.Heroes(export.Store).Single(each => each.Name == hero.ToString());

    [Fact]
    public void The_hero_and_item_read_back_from_the_code()
    {
        var hero = Hero(Fellowship.SDK.Hero.Xavian);
        var item = AttunementTrees.Items(Tree, hero, export.Store)[0];

        var read = RoundTrip(Plan([1], (1, In(1))) with { Hero = hero, Item = item });

        Assert.Equal(hero.Id, read.Hero?.Id);
        Assert.Equal((item.Id, item.Hero), (read.Item?.Id, read.Item?.Hero));
        Assert.Equal([1], read.Path);
    }

    [Fact]
    public void Every_item_on_a_tree_names_one_reading_for_each_hero_it_offers()
    {
        foreach (var hero in AttunementTrees.Heroes(export.Store))
        {
            Assert.All(AttunementTrees.Items(Tree, hero, export.Store), item => Assert.Equal(hero.Name, item.Hero?.ToString()));
        }
    }

    [Fact]
    public void An_item_from_another_tree_is_dropped()
    {
        var neck = AttunementTrees.BySlot(export.Store).Single(entry => entry.Slot == Printed(ItemPower.NecklaceType)).Tree;
        var hero = Hero(Fellowship.SDK.Hero.Xavian);

        var read = RoundTrip(Plan([]) with { Hero = hero, Item = AttunementTrees.Items(neck, hero, export.Store)[0] });

        Assert.Equal(hero.Id, read.Hero?.Id);
        Assert.Null(read.Item);
    }

    [Fact]
    public void An_item_without_a_hero_is_dropped()
    {
        var hero = Hero(Fellowship.SDK.Hero.Xavian);

        Assert.Null(RoundTrip(Plan([]) with { Item = AttunementTrees.Items(Tree, hero, export.Store)[0] }).Item);
    }

    [Fact]
    public void A_code_is_base64url() =>
        Assert.Matches("^[A-Za-z0-9_-]+$", AttunementTrees.Write(Tree, Plan([1], (1, In(1)), (12, In(12)))));

    [Fact]
    public void An_empty_plan_writes_no_code() =>
        Assert.Empty(AttunementTrees.Write(Tree, AttunementTreePlan.Empty));

    [Fact]
    public void A_code_outside_the_alphabet_reads_as_an_empty_plan()
    {
        var read = AttunementTrees.Read("1.123,3.456", Tree, export.Store);

        Assert.Empty(read.Assigned);
        Assert.Empty(read.Path);
    }

    [Fact]
    public void A_node_whose_parents_are_off_the_path_leaves_the_path() =>
        Assert.Empty(Plan([3], (3, In(3))).Path);

    [Fact]
    public void A_node_with_no_augmentation_leaves_the_path() =>
        Assert.Empty(Plan([1]).Path);

    [Fact]
    public void A_row_takes_one_infusion() =>
        Assert.Equal([1], Plan([1, 2], (1, In(1)), (2, In(2, 1))).Path);

    [Fact]
    public void A_augmentation_assigned_once_is_not_assigned_again() =>
        Assert.Equal([1, 3], Plan([], (1, In(1)), (3, In(3)), (6, In(1))).Assigned.Keys.Order());

    [Fact]
    public void A_augmentation_outside_the_node_category_is_dropped() =>
        Assert.Empty(Plan([], (1, In(3))).Assigned);

    [Fact]
    public void Leaving_a_node_drops_every_node_infused_through_it_and_keeps_every_assignment()
    {
        var plan = Plan([1, 3, 6], (1, In(1)), (3, In(3)), (6, In(6, 1)));
        Assert.Equal(3, plan.Path.Count);

        var left = AttunementTrees.Planned(Tree, plan.Hero, plan.Item, plan.Assigned, plan.Path.Where(node => node != 1).ToHashSet());

        Assert.Empty(left.Path);
        Assert.Equal(3, left.Assigned.Count);
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
