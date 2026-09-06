using System.Net;
using System.Text;
using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public sealed class MobileTooltipTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private readonly ICodexViewService view = factory.Services.GetRequiredService<ICodexViewService>();

    private static (EntityDocument Document, string? Hero) Sample(EntityType type)
    {
        var store = RouteTests.Factory.Store;
        var document = store.Entities.First(candidate => EntityTypes.TypeOf(candidate) == type);

        return (document, store.All(type, document.Id).Count > 1 ? store.Wearer(document)?.Name : null);
    }

    [Fact]
    public async Task A_tooltip_asked_for_mobile_is_wrapped_in_modal_chrome()
    {
        var (item, hero) = Sample(EntityType.Item);

        var drawn = await client.TooltipHtmlAsync(
            item.Id, new ItemTooltipRequest { Hero = hero, Mobile = true });

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
        var (item, hero) = Sample(EntityType.Item);

        var drawn = await client.TooltipHtmlAsync(item.Id, new ItemTooltipRequest { Hero = hero });

        Assert.StartsWith("<div class=\"fx-tip", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("fx-modal", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dismiss", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mobile_reads_true_as_chrome_and_false_as_absent()
    {
        var (item, hero) = Sample(EntityType.Item);

        var wrapped = await client.TooltipHtmlAsync(
            item.Id, new ItemTooltipRequest { Hero = hero, Mobile = true });

        Assert.StartsWith("<div class=\"fx-modal\"", wrapped, StringComparison.Ordinal);

        var bare = await client.TooltipHtmlAsync(
            item.Id, new ItemTooltipRequest { Hero = hero, Mobile = false });

        Assert.StartsWith("<div class=\"fx-tip", bare, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_refused()
    {
        var (item, _) = Sample(EntityType.Item);
        var address = $"/{CodexAddresses.Tooltip(EntityType.Item, item.Id)}";

        using var message = new HttpRequestMessage(HttpMethod.Query, address)
        {
            Content = new StringContent("{ \"mobile\": maybe }", Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(message, Xunit.TestContext.Current.CancellationToken);

        var body = await response.BodyAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"A body that is not JSON answered {(int)response.StatusCode}, not 400. Body: {body}");
    }

    [Fact]
    public async Task Every_type_s_tooltip_accepts_mobile_and_opens_its_own_page()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            var (document, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);

            using var response = await client.TooltipAsync(type, document.Id, mobile: true, hero: hero);

            var drawn = await response.BodyAsync();

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
        var (item, hero) = Sample(EntityType.Item);

        var drawn = await client.TooltipHtmlAsync(
            item.Id, new ItemTooltipRequest { Hero = hero, Mobile = true });

        var shut = drawn.IndexOf("<button type=\"button\" class=\"shut\"", StringComparison.Ordinal);
        var scrim = drawn.IndexOf("<div class=\"scrim\" data-dismiss>", StringComparison.Ordinal);

        Assert.True(shut >= 0 && scrim > shut, "The close button is drawn before the scrim, so it never scrolls with the card.");
    }

    [Fact]
    public async Task The_chrome_s_rules_ride_inside_the_fragment()
    {
        var (item, hero) = Sample(EntityType.Item);

        var drawn = await client.TooltipHtmlAsync(
            item.Id, new ItemTooltipRequest { Hero = hero, Mobile = true });

        Assert.Contains(".fx-modal", drawn.Split("<style>")[1].Split("</style>")[0], StringComparison.Ordinal);
    }
}
