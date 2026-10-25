namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class StateChromeTests(Export export)
{
    [Theory]
    [InlineData(null)]
    [InlineData("no such state")]
    [InlineData("SELECTED")]
    [InlineData("3")]
    public void A_node_nothing_asks_for_is_the_one_the_reader_is_looking_at(string? state) =>
        Assert.Equal(AttunementNodeState.Selected, ChromeAssetService.State<AttunementNodeState>(state));

    [Fact]
    public void A_node_state_draws_its_own_texture() =>
        Assert.Equal(3, Enum.GetValues<AttunementNodeState>().Select(export.Chrome.AttunementNode).Distinct().Count());

    [Fact]
    public void Only_the_selected_node_draws_its_icon_in_colour()
    {
        Assert.True(ChromeAssetService.Allocated(AttunementNodeState.Selected));
        Assert.False(ChromeAssetService.Allocated(AttunementNodeState.Available));
        Assert.False(ChromeAssetService.Allocated(AttunementNodeState.Unavailable));
    }

    [Fact]
    public void Only_the_unavailable_node_is_padlocked()
    {
        Assert.True(ChromeAssetService.Padlocked(ChromeAssetService.State<AttunementNodeState>("UNAVAILABLE")));
        Assert.False(ChromeAssetService.Padlocked(AttunementNodeState.Available));
        Assert.False(ChromeAssetService.Padlocked(AttunementNodeState.Selected));
        Assert.False(ChromeAssetService.Padlocked(ChromeAssetService.State<AttunementNodeState>("allocated")));
    }
}
