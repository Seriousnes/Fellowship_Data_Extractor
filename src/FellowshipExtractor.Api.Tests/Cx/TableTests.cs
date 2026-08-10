namespace FellowshipExtractor.Api.Tests;

public class TableTests : BunitContext
{
    private static readonly IReadOnlyList<TableProbe.Probe> Rows =
    [
        new("boot", "Old Boots", 315, 1.5, "Feet"),
        new("bow", "Frozen Longbow", 360, null, "Weapon"),
    ];

    private IRenderedComponent<TableProbe> Drawn(bool grouping = false) =>
        Render<TableProbe>(p => p.Add(t => t.Rows, Rows).Add(t => t.Grouping, grouping));

    [Fact]
    public void Draws_every_declared_column_in_the_head_on_the_first_render()
    {
        var heads = Drawn().FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();

        Assert.Equal(["", "Name", "ILvl", "Cast"], heads);
    }

    [Fact]
    public void Draws_a_body_cell_for_every_column_of_every_row()
    {
        var cut = Drawn();

        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Equal(8, cut.FindAll("tbody td").Count);
    }

    [Fact]
    public void Reads_a_bound_column_off_the_row()
    {
        var cells = Drawn().FindAll("tbody tr:first-child td").Select(td => td.TextContent.Trim()).ToArray();

        Assert.Equal(["boot", "Old Boots", "315", "1.5s"], cells);
    }

    [Fact]
    public void Draws_a_template_column_as_its_own_markup()
    {
        Assert.Equal("boot", Drawn().Find("tbody tr:first-child td i").TextContent);
    }

    [Fact]
    public void Draws_nothing_where_a_bound_column_reads_null()
    {
        Assert.Equal(string.Empty, Drawn().FindAll("tbody tr")[1].Children[3].TextContent.Trim());
    }

    [Fact]
    public void Hangs_the_sort_link_and_state_on_the_column_that_names_a_key()
    {
        var head = Drawn().FindAll("thead th")[1];

        Assert.Equal("ascending", head.GetAttribute("aria-sort"));
        Assert.Equal("/browse?sort=name", head.QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public void Leaves_a_column_that_names_no_key_unsorted()
    {
        var head = Drawn().FindAll("thead th")[3];

        Assert.Null(head.GetAttribute("aria-sort"));
        Assert.Null(head.QuerySelector("a"));
    }

    [Fact]
    public void Marks_a_numeric_column_on_both_its_head_and_its_cell()
    {
        var cut = Drawn();

        Assert.Equal("cx-numeric", cut.FindAll("thead th")[2].GetAttribute("class"));
        Assert.Equal("cx-numeric", cut.FindAll("tbody tr:first-child td")[2].GetAttribute("class"));
    }

    [Fact]
    public void Heads_each_run_of_a_grouped_list_and_spans_every_column()
    {
        var headings = Drawn(grouping: true).FindAll("tr.cx-group-head td");

        Assert.Equal(["Feet", "Weapon"], headings.Select(td => td.TextContent).ToArray());
        Assert.All(headings, td => Assert.Equal("4", td.GetAttribute("colspan")));
    }

    [Fact]
    public void Draws_a_head_and_no_rows_for_an_empty_list()
    {
        var cut = Render<TableProbe>(p => p.Add(t => t.Rows, []));

        Assert.Equal(4, cut.FindAll("thead th").Count);
        Assert.Empty(cut.FindAll("tbody tr"));
    }
}
