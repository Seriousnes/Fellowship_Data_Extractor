using Fellowship.SDK.Records;

namespace Fellowship.Codex.Tests;

public sealed class RecordFieldTests
{
    private static ItemRecord Item => new()
    {
        Type = EntityType.Item,
        Id = 7,
        Name = "Apron",
        Slot = "Chest",
        GemSlot = true,
        Heroes = [Hero.Mara, Hero.Rime],
        ItemLevel = 300,
    };

    private static RecordField Field(string key) => RecordFields.Find(typeof(ItemRecord), key)!;

    [Fact]
    public void Every_scalar_and_scalar_list_field_is_a_filter()
    {
        var keys = RecordFields.Of(typeof(ItemRecord)).Select(field => field.Key).ToArray();

        Assert.Contains("slot", keys);
        Assert.Contains("heroes", keys);
        Assert.Contains("itemLevel", keys);
        Assert.DoesNotContain("stats", keys);
        Assert.DoesNotContain("type", keys);
    }

    [Fact]
    public void A_field_matches_any_wanted_value_case_insensitively()
    {
        Assert.True(new RecordFieldFilter(Field("slot")) { Wanted = ["head", "chest"] }.Matches(Item));
        Assert.False(new RecordFieldFilter(Field("slot")) { Wanted = ["Head"] }.Matches(Item));
    }

    [Fact]
    public void A_list_field_matches_when_any_of_its_values_does()
    {
        Assert.True(new RecordFieldFilter(Field("heroes")) { Wanted = ["rime"] }.Matches(Item));
        Assert.False(new RecordFieldFilter(Field("heroes")) { Wanted = ["Gunde"] }.Matches(Item));
    }

    [Fact]
    public void A_boolean_field_reads_true_and_false()
    {
        Assert.True(new RecordFieldFilter(Field("gemSlot")) { Wanted = ["true"] }.Matches(Item));
        Assert.False(new RecordFieldFilter(Field("gemSlot")) { Wanted = ["false"] }.Matches(Item));
    }

    [Fact]
    public void A_numeric_field_bounds_a_range()
    {
        Assert.True(new RecordFieldFilter(Field("itemLevel")) { Min = 300, Max = 300 }.Matches(Item));
        Assert.False(new RecordFieldFilter(Field("itemLevel")) { Min = 301 }.Matches(Item));
        Assert.False(new RecordFieldFilter(Field("itemLevel")) { Max = 299 }.Matches(Item));
    }

    [Fact]
    public void A_bound_leaves_out_a_record_with_no_value() =>
        Assert.False(new RecordFieldFilter(Field("itemLevel")) { Min = 1 }.Matches(Item with { ItemLevel = null }));
}
