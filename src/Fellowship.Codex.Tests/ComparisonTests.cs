using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class ComparisonTests(Export export)
{
    private readonly SearchIndexService index = new(export.Store, NullLogger<SearchIndexService>.Instance);

    private ICodexService Codex => export.View.Codex;

    private CodexQuery Parse(params (string Key, string Value)[] pairs)
    {
        var query = CodexQuery.Parse(
            new QueryCollection(pairs
                .GroupBy(pair => pair.Key)
                .ToDictionary(group => group.Key, group => new StringValues([.. group.Select(pair => pair.Value)]))),
            Codex,
            RequestProperties.Of(typeof(EntitiesRequest)),
            null,
            out var error);

        Assert.Null(error);
        return query;
    }

    private CodexResults Run(CodexQuery query) =>
        new CodexSearchService(export.Store, Codex, index).Run(query with { PageSize = int.MaxValue / 2 });

    private IReadOnlyList<ItemDocument> Items(params (string Key, string Value)[] pairs) =>
        [.. Run(Parse([("type", "item"), .. pairs])).Rows.OfType<ItemDocument>()];

    [Fact]
    public void A_parameter_names_its_measure_and_comparator()
    {
        var query = Parse(("hasteGt", "0"));

        Assert.Equal([new Comparison("haste", Comparator.Gt, 0)], query.Comparisons);
    }

    [Fact]
    public void Min_and_max_read_as_at_least_and_at_most()
    {
        var query = Parse(("cooldownMin", "3"), ("cooldownMax", "10"));

        Assert.Equal(
            [new Comparison("cooldown", Comparator.Min, 3), new Comparison("cooldown", Comparator.Max, 10)],
            query.Comparisons);
    }

    [Fact]
    public void A_measure_takes_the_same_comparator_twice()
    {
        var query = Parse(("hasteGt", "0"), ("hasteGt", "10"));

        Assert.Equal(2, query.Comparisons.Count);
    }

    [Fact]
    public void Comparisons_round_trip_through_the_address()
    {
        var query = Parse(("hasteGt", "0"), ("spiritLt", "50"));

        Assert.Equal("?hasteGt=0&spiritLt=50", query.ToQueryString());
    }

    [Fact]
    public void Greater_than_is_strict()
    {
        var haste = Codex.Measure("haste");
        var rows = Items(("hasteGt", "0"));
        var least = rows.Min(row => haste.Value(row))!.Value;

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(haste.Value(row) > 0));
        Assert.DoesNotContain(
            Items(("hasteGt", FellowshipCodex.Number(least))),
            row => haste.Value(row) == least);
        Assert.Contains(Items(("hasteMin", FellowshipCodex.Number(least))), row => haste.Value(row) == least);
    }

    [Fact]
    public void Each_white_stat_an_item_has_is_a_measure_labelled_by_the_settings()
    {
        var attributes = export.Store.Settings.StatCalculation.Attributes;

        var declared = export.Store.Entities.OfType<ItemDocument>()
            .Where(item => PublicTypes.IsListed(item))
            .SelectMany(item => item.Stats ?? [])
            .Select(slot => slot.Stat)
            .Where(attributes.ContainsKey)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(declared);

        foreach (var stat in declared)
        {
            var measure = Codex.Measure(stat);
            Assert.Equal(attributes[stat].DisplayName, measure.Label);
        }
    }

    [Fact]
    public void A_stat_measure_reads_the_value_the_tooltip_prints()
    {
        var items = export.Store.Entities.OfType<ItemDocument>()
            .Where(item => PublicTypes.IsListed(item) && item.Stats is { Count: > 0 })
            .Take(25)
            .ToArray();

        Assert.NotEmpty(items);

        foreach (var item in items)
        {
            foreach (var line in export.Tooltip.Rolls(item, new TooltipQuery()).Where(line => line.Stat is not null))
            {
                Assert.Equal((double)line.Value, Codex.Measure(line.Stat!).Value(item));
            }
        }
    }

    [Fact]
    public void A_download_has_a_column_for_each_facet_and_measure_of_its_rows()
    {
        var rows = Items(("hasteGt", "0"), ("slot", "Chest"));
        var csv = Encoding.UTF8.GetString(EntityTable.Of(rows, Codex, export.View, "https://codex.test").Write(EntityTable.Csv));
        var header = csv.TrimStart('﻿').Split("\r\n")[0].Split(',');

        Assert.Contains("haste", header);
        Assert.Contains(QueryKeys.Key(FacetType.Source), header);
        Assert.Contains(QueryKeys.Key(FacetType.ArmorType), header);
        Assert.Contains("armor", header);
        Assert.Equal(header.Length, header.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(rows.Count + 2, csv.Split("\r\n").Length);
    }
}
