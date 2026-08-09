namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class StatParameterTests(Export export)
{
    [Fact]
    public void Names_every_display_name_the_attribute_table_carries_once()
    {
        var rows = export.Store.Vocabulary.StatCalculation.Attributes;

        var shared = rows.Values
            .Select(row => row.DisplayName)
            .Where(name => name is { Length: > 0 })
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expected = rows.Values
            .Select(row => row.DisplayName)
            .Where(name => name is { Length: > 0 } && !shared.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expected, export.Store.Stats.Names);
    }

    [Fact]
    public void Carries_the_stats_a_hero_sheet_prints_that_no_item_rolls()
    {
        Assert.Contains("Parry", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Dodge", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Cooldown Reduction", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leaves_a_name_two_attributes_share_for_the_printed_line()
    {
        Assert.Null(export.Store.Stats.Resolve("armor"));
        Assert.DoesNotContain("Armor", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolves_a_display_name_written_without_its_spaces()
    {
        Assert.Equal("BlockChance", export.Store.Stats.Resolve("blockchance"));
        Assert.Equal("CritChance", export.Store.Stats.Resolve("Critical Strike"));
    }
}
