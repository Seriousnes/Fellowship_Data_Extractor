using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

public class StaticTableTests
{
    private static readonly IReadOnlyList<TableProbe.Probe> Rows =
    [
        new("boot", "Old Boots", 315, 1.5, "Feet"),
        new("bow", "Frozen Longbow", 360, null, "Weapon"),
    ];

    private static async Task<string> Drawn()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        var markup = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<TableProbe>(
                ParameterView.FromDictionary(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [nameof(TableProbe.Rows)] = Rows,
                }));

            return rendered.ToHtmlString();
        });

        return markup.Bare();
    }

    [Fact]
    public async Task Every_column_registers_before_the_head_draws_in_one_static_pass()
    {
        Assert.Contains(
            "<thead><tr>" +
            "<th class=\"cx-art\"></th>" +
            "<th aria-sort=\"ascending\"><a href=\"/browse?sort=name\">Name</a></th>" +
            "<th class=\"cx-numeric\"><a href=\"/browse?sort=ilvl\">ILvl</a></th>" +
            "<th>Cast</th>" +
            "</tr></thead>",
            await Drawn(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_row_draws_every_cell_in_one_static_pass()
    {
        Assert.Contains(
            "<tr>" +
            "<td class=\"cx-art\"><i>boot</i></td>" +
            "<td>Old Boots</td>" +
            "<td class=\"cx-numeric\">315</td>" +
            "<td>1.5<span class=\"cx-unit\">s</span></td>" +
            "</tr>",
            await Drawn(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Draws_an_empty_cell_where_a_bound_column_reads_null()
    {
        Assert.Contains(
            "<td>Frozen Longbow</td><td class=\"cx-numeric\">360</td><td></td>",
            await Drawn(),
            StringComparison.Ordinal);
    }
}
