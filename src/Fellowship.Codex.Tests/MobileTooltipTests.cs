using System.Net;

namespace Fellowship.Codex.Tests;

public sealed class MobileTooltipTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private readonly ICodexViewService view = factory.Services.GetRequiredService<ICodexViewService>();

    private Task<HttpResponseMessage> Get(string address) =>
        client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

    private static Task<string> Body(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

    private static (EntityDocument Document, string Wearer) Sample(EntityType type)
    {
        var store = RouteTests.Factory.Store;
        var document = store.Entities.First(candidate => EntityTypes.TypeOf(candidate) == type);
        var hero = store.All(type, document.Id).Count > 1 ? store.Wearer(document)?.Name : null;

        return (document, hero is null ? string.Empty : $"hero={Uri.EscapeDataString(hero)}&");
    }

    [Fact]
    public async Task A_tooltip_asked_for_mobile_is_wrapped_in_modal_chrome()
    {
        var (item, wearer) = Sample(EntityType.Item);

        var drawn = await Body(await Get($"/api/item/{item.Id}/tooltip?{wearer}mobile"));

        Assert.StartsWith("<div class=\"fx-modal\"", drawn, StringComparison.Ordinal);
        Assert.Contains("<div class=\"scrim\" data-dismiss>", drawn, StringComparison.Ordinal);
        Assert.Contains("<div class=\"sheet\" data-dismiss>", drawn, StringComparison.Ordinal);
        Assert.Contains(
            "<button type=\"button\" class=\"shut\" data-dismiss aria-label=\"Close\">", drawn, StringComparison.Ordinal);
        Assert.Contains("class=\"fx-tip", drawn, StringComparison.Ordinal);
        Assert.Contains(
            $"<a class=\"open\" href=\"http://localhost{view.Href(item)}\">Open {PublicTypes.Noun(EntityType.Item)}</a>",
            drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tooltip_asked_plainly_carries_no_chrome()
    {
        var (item, wearer) = Sample(EntityType.Item);

        var drawn = await Body(await Get($"/api/item/{item.Id}/tooltip?{wearer}"));

        Assert.StartsWith("<div class=\"fx-tip", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("fx-modal", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dismiss", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mobile_reads_bare_as_true_and_false_as_absent()
    {
        var (item, wearer) = Sample(EntityType.Item);

        foreach (var given in new[] { "mobile", "mobile=true", "mobile=1" })
        {
            var response = await Get($"/api/item/{item.Id}/tooltip?{wearer}{given}");
            var drawn = await Body(response);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK, $"?{given} answered {(int)response.StatusCode}: {drawn}");
            Assert.StartsWith("<div class=\"fx-modal\"", drawn, StringComparison.Ordinal);
        }

        foreach (var given in new[] { "mobile=false", "mobile=0" })
        {
            var drawn = await Body(await Get($"/api/item/{item.Id}/tooltip?{wearer}{given}"));

            Assert.StartsWith("<div class=\"fx-tip", drawn, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_value_that_is_neither_yes_nor_no_is_refused()
    {
        var (item, wearer) = Sample(EntityType.Item);

        var response = await Get($"/api/item/{item.Id}/tooltip?{wearer}mobile=maybe");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Every_type_s_tooltip_accepts_mobile_and_opens_its_own_page()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            var (document, wearer) = Sample(type);
            var slug = EntityTypes.Slug(type);

            var response = await Get($"/api/{slug}/{document.Id}/tooltip?{wearer}mobile");
            var drawn = await Body(response);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK, $"{slug} answered {(int)response.StatusCode}: {drawn}");
            Assert.StartsWith("<div class=\"fx-modal\"", drawn, StringComparison.Ordinal);
            Assert.Contains(
                $"<a class=\"open\" href=\"http://localhost{view.Href(document)}\">Open {PublicTypes.Noun(type)}</a>",
                drawn, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_close_button_sits_outside_the_scrolling_sheet()
    {
        var (item, wearer) = Sample(EntityType.Item);

        var drawn = await Body(await Get($"/api/item/{item.Id}/tooltip?{wearer}mobile"));

        var shut = drawn.IndexOf("<button type=\"button\" class=\"shut\"", StringComparison.Ordinal);
        var scrim = drawn.IndexOf("<div class=\"scrim\" data-dismiss>", StringComparison.Ordinal);

        Assert.True(shut >= 0 && scrim > shut, "The close button is drawn before the scrim, so it never scrolls with the card.");
    }

    [Fact]
    public async Task The_chrome_s_rules_ride_inside_the_fragment()
    {
        var (item, wearer) = Sample(EntityType.Item);

        var drawn = await Body(await Get($"/api/item/{item.Id}/tooltip?{wearer}mobile"));

        Assert.Contains(".fx-modal", drawn.Split("<style>")[1].Split("</style>")[0], StringComparison.Ordinal);
    }
}
