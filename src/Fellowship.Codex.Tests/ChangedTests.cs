using System.Text.Json.Nodes;

namespace Fellowship.Codex.Tests;

public class ChangedTests
{
    private static string[] Paths(JsonNode? node) => [.. Changed.Leaves(node).Select(leaf => leaf.Path)];

    private static string Reads(ChangedLeaf leaf) => $"{leaf.Path}: {leaf.Older} -> {leaf.Newer}";

    [Fact]
    public void A_nested_object_walks_to_its_leaves_joined_by_dots()
    {
        var node = JsonNode.Parse("""{"id":5224,"drop":{"level":300,"row":"Default"}}""");

        Assert.Equal(["id", "drop.level", "drop.row"], Paths(node));
        Assert.Equal("300", Changed.Leaves(node)[1].Value);
    }

    [Fact]
    public void An_array_position_walks_as_a_bracketed_index()
    {
        var node = JsonNode.Parse("""{"stats":[{"max":4},{"max":9}]}""");

        Assert.Equal(["stats[0].max", "stats[1].max"], Paths(node));
    }

    [Fact]
    public void A_string_reads_without_its_quotes_and_everything_else_as_its_literal()
    {
        var node = JsonNode.Parse("""{"name":"Old Boots","gemSlot":true,"tag":null,"id":5224}""");

        Assert.Equal(
            ["Old Boots", "true", "null", "5224"],
            Changed.Leaves(node).Select(leaf => leaf.Value));
    }

    [Fact]
    public void Two_equal_values_change_nothing()
    {
        var older = JsonNode.Parse("""{"id":5224,"stats":[{"max":4}]}""");
        var newer = JsonNode.Parse("""{"id":5224,"stats":[{"max":4}]}""");

        Assert.Empty(Changed.Between(older, newer));
    }

    [Fact]
    public void A_changed_scalar_reads_its_older_and_newer_value()
    {
        var older = JsonNode.Parse("""{"itemLevel":300}""");
        var newer = JsonNode.Parse("""{"itemLevel":315}""");

        Assert.Equal("itemLevel: 300 -> 315", Reads(Assert.Single(Changed.Between(older, newer))));
    }

    [Fact]
    public void A_changed_string_reads_both_sides_unquoted()
    {
        var older = JsonNode.Parse("""{"icon":"T_Wrists_A_Crit"}""");
        var newer = JsonNode.Parse("""{"icon":"T_Wrists_A_Crit_02"}""");

        Assert.Equal(
            "icon: T_Wrists_A_Crit -> T_Wrists_A_Crit_02",
            Reads(Assert.Single(Changed.Between(older, newer))));
    }

    [Fact]
    public void A_changed_array_element_reads_under_its_own_index()
    {
        var older = JsonNode.Parse("""{"stats":[{"max":4},{"max":9}]}""");
        var newer = JsonNode.Parse("""{"stats":[{"max":4},{"max":11}]}""");

        Assert.Equal("stats[1].max: 9 -> 11", Reads(Assert.Single(Changed.Between(older, newer))));
    }

    [Fact]
    public void An_added_property_reads_with_nothing_before_it()
    {
        var older = JsonNode.Parse("""{"id":5224}""");
        var newer = JsonNode.Parse("""{"id":5224,"slot":"Back"}""");

        var leaf = Assert.Single(Changed.Between(older, newer));

        Assert.Equal("slot", leaf.Path);
        Assert.Null(leaf.Older);
        Assert.Equal("Back", leaf.Newer);
    }

    [Fact]
    public void A_removed_property_reads_with_nothing_after_it()
    {
        var older = JsonNode.Parse("""{"id":5224,"slot":"Back"}""");
        var newer = JsonNode.Parse("""{"id":5224}""");

        var leaf = Assert.Single(Changed.Between(older, newer));

        Assert.Equal("slot", leaf.Path);
        Assert.Equal("Back", leaf.Older);
        Assert.Null(leaf.Newer);
    }

    [Fact]
    public void A_changed_leaf_inside_a_nested_object_carries_its_whole_path()
    {
        var older = JsonNode.Parse("""{"drop":{"level":300,"row":"Default"}}""");
        var newer = JsonNode.Parse("""{"drop":{"level":360,"row":"Default"}}""");

        Assert.Equal("drop.level: 300 -> 360", Reads(Assert.Single(Changed.Between(older, newer))));
    }
}
