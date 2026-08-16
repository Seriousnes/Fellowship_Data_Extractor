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
        var older = JsonNode.Parse("""{"icon":"T_Wrists_A_Crit.png"}""");
        var newer = JsonNode.Parse("""{"icon":"T_Wrists_A_Crit_02.png"}""");

        Assert.Equal(
            "icon: T_Wrists_A_Crit.png -> T_Wrists_A_Crit_02.png",
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

public class ChangesTests : BunitContext
{
    private static VersionValue At(string build, string? json) =>
        new(build, json is null ? null : JsonNode.Parse(json));

    private IRenderedComponent<Changes> Draw(params VersionValue[] byVersion) =>
        Render<Changes>(p => p.Add(x => x.ByVersion, byVersion));

    [Fact]
    public void One_version_heads_its_build_and_marks_it_current()
    {
        var cut = Draw(At("1002", """{"id":5224}"""));

        Assert.Equal("1002current", cut.Find("p.cx-change-build").TextContent);
        Assert.Equal("current", cut.Find("span.cx-change-current").TextContent);
        Assert.Empty(cut.FindAll("ul.cx-change-leaves"));
    }

    [Fact]
    public void An_adjacent_pair_heads_the_older_build_and_states_what_the_newer_changed()
    {
        var cut = Draw(At("1002", """{"slot":"Chest"}"""), At("1001", """{"slot":"Back"}"""));

        var heads = cut.FindAll("p.cx-change-build").Select(head => head.TextContent).ToArray();

        Assert.Equal(["1002current", "1001"], heads);

        var leaf = cut.Find("ul.cx-change-leaves li");

        Assert.Equal("slot", leaf.QuerySelector("span.cx-change-path")!.TextContent);
        Assert.Equal("Back", leaf.QuerySelector("span.cx-was")!.TextContent);
        Assert.Equal("Chest", leaf.QuerySelector("span.cx-now")!.TextContent);
    }

    [Fact]
    public void A_document_the_older_build_does_not_carry_reads_as_added()
    {
        var cut = Draw(At("1002", """{"id":5224}"""), At("1001", null));

        Assert.Equal("added in 1002", cut.Find("p.cx-change-note").TextContent);
    }

    [Fact]
    public void A_document_the_newer_build_does_not_carry_reads_as_removed()
    {
        var cut = Draw(At("1002", null), At("1001", """{"id":5224}"""));

        Assert.Equal("removed in 1002", cut.Find("p.cx-change-note").TextContent);
    }

    [Fact]
    public void Two_equal_versions_read_as_no_change()
    {
        var cut = Draw(At("1002", """{"id":5224}"""), At("1001", """{"id":5224}"""));

        Assert.Equal("no change", cut.Find("p.cx-change-note").TextContent);
    }

    [Fact]
    public void Every_pair_down_the_list_gets_its_own_block()
    {
        var cut = Draw(
            At("1002", """{"slot":"Legs"}"""),
            At("1001", """{"slot":"Chest"}"""),
            At("1000", """{"slot":"Back"}"""));

        Assert.Equal(
            ["1002current", "1001", "1000"],
            cut.FindAll("p.cx-change-build").Select(head => head.TextContent));

        Assert.Equal(
            ["Chest", "Back"],
            cut.FindAll("ul.cx-change-leaves span.cx-was").Select(was => was.TextContent));
    }
}
