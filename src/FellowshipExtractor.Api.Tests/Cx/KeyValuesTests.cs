namespace FellowshipExtractor.Api.Tests;

public class KeyValuesTests : BunitContext
{
    [Fact]
    public void Wraps_children_in_a_key_value_list()
    {
        var cut = Render<KeyValues>(p => p.AddChildContent(
            "<li><span class=\"cx-k\">Printed</span><span class=\"cx-v\">10</span></li>"));

        cut.MarkupMatches(
            "<ul class=\"cx-kv\"><li><span class=\"cx-k\">Printed</span>" +
            "<span class=\"cx-v\">10</span></li></ul>");
    }
}
