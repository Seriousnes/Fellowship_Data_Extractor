namespace Fellowship.Codex.Tests;

public sealed class CodexKeyTests
{
    [Theory]
    [InlineData("unobtainable")]
    [InlineData("Unobtainable")]
    public void A_flag_key_reads_in_any_case(string key)
    {
        Assert.True(QueryKeys.TryParse<FlagType>(key, out var flag));
        Assert.Equal(FlagType.Unobtainable, flag);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("slot,hero")]
    [InlineData("")]
    public void A_number_or_a_list_is_no_facet(string key) => Assert.False(QueryKeys.TryParse<FacetType>(key, out _));

    [Fact]
    public void A_key_is_the_lowercase_member_name()
    {
        Assert.Equal("ilvl", QueryKeys.Key(SortingType.Ilvl));
        Assert.Equal("slot", QueryKeys.Key(FacetType.Slot));
        Assert.Equal("Show unobtainable entries", CodexKeys.Label(FlagType.Unobtainable));
    }

    [Theory]
    [InlineData("rank", SortingType.Rank)]
    [InlineData("cost", SortingType.Cost)]
    public void A_sort_naming_a_sorting_and_a_measure_is_the_sorting(string key, SortingType sorting)
    {
        Assert.True(SortColumn.TryParse(key, out var sort));
        Assert.Equal(sorting, sort.Sorting);
    }

    [Fact]
    public void A_sort_naming_only_a_measure_is_the_measure()
    {
        Assert.True(SortColumn.TryParse("cooldown", out var sort));
        Assert.Equal(MeasureType.Cooldown, sort.Measure);
    }
}
