namespace FellowshipExtractor.Api.Tests;

public class KeyValueTests : BunitContext
{
    [Fact]
    public void Renders_a_key_and_a_value_on_one_line()
    {
        var cut = Render<KeyValue>(p => p
            .Add(x => x.Key, "Arithmetic")
            .AddChildContent("Add over 5 ticks"));

        Assert.Equal(
            "<li><span class=\"cx-k\">Arithmetic</span>" +
            "<span class=\"cx-v\">Add over 5 ticks</span></li>",
            cut.Markup.Bare());
    }

    [Fact]
    public void Marks_a_missing_value()
    {
        var cut = Render<KeyValue>(p => p
            .Add(x => x.Key, "Note")
            .Add(x => x.Missing, true)
            .AddChildContent("printed and computed values disagree"));

        Assert.Equal(
            "<li><span class=\"cx-k\">Note</span>" +
            "<span class=\"cx-v cx-miss\">printed and computed values disagree</span></li>",
            cut.Markup.Bare());
    }

    [Fact]
    public void Renders_a_prose_value_span_with_no_whitespace_of_its_own()
    {
        var cut = Render<KeyValue>(p => p
            .Add(x => x.Key, "Printed")
            .Add(x => x.Class, "cx-prose")
            .AddChildContent("10<span> / </span>20"));

        Assert.Equal(
            "<li><span class=\"cx-k\">Printed</span>" +
            "<span class=\"cx-v cx-prose\">10<span> / </span>20</span></li>",
            cut.Markup.Bare());
    }
}
