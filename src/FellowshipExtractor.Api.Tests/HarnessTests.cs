namespace FellowshipExtractor.Api.Tests;

public class HarnessTests : BunitContext
{
    [Fact]
    public void Renders_a_razor_fixture_from_the_test_project()
    {
        var label = PublicTypes.Label("ItemID");

        var cut = Render<Harness>(p => p.Add(x => x.Text, label));

        Assert.Equal($"<p class=\"cx-probe\">{label}</p>", cut.Markup);
    }
}
