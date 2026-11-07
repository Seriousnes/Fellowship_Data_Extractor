using System.Net;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;

using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

/// <summary>A dungeon page lists what drops there and who lives there in the catalogue's own grid, pinned to the dungeon.</summary>
[Collection(Admin.Name)]
public sealed partial class DungeonPageTests(AdminHost host)
{
    private IEntityStoreService Store => host.Factory.Services.GetRequiredService<IEntityStoreService>();

    private ICodexViewService Ui => host.Factory.Services.GetRequiredService<ICodexViewService>();

    private ICodexService Codex => host.Factory.Services.GetRequiredService<ICodexService>();

    /// <summary>The dungeon dropping the most items, so every tab has rows.</summary>
    private DungeonDocument Dungeon =>
        Store.Entities.OfType<ItemDocument>()
            .SelectMany(item => item.Sources ?? [])
            .Where(source => source.Dungeon is not null)
            .GroupBy(source => source.Dungeon!.Id)
            .OrderByDescending(group => group.Count())
            .Select(group => Store.All(EntityType.Dungeon, group.Key).OfType<DungeonDocument>().First())
            .First();

    private async Task<string> Page(string query = "")
    {
        var cancellation = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, Ui.Path(Dungeon) + query);
        request.Headers.Add("Cookie", $"{CatalogueFilterGate.Cookie}=1");

        var response = await host.Client().SendAsync(request, cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(cancellation));
    }

    private int Listed(EntityType type, Func<EntityDocument, bool>? also = null)
    {
        var source = Codex.Facet(FacetType.Source);
        var name = PublicTypes.NameOf(Dungeon);

        return Store.Entities.Count(row =>
            EntityTypes.TypeOf(row) == type && PublicTypes.IsListed(row) &&
            source.Matches(row, [name]) &&
            Codex.Flags.All(flag => flag.Admits(row)) &&
            (also?.Invoke(row) ?? true));
    }

    private static int Counted(string body) => int.Parse(Count().Match(body).Groups[1].Value.Replace(",", ""));

    [GeneratedRegex(@"cx-count""[^>]*><b[^>]*>([\d,]+)</b>")]
    private static partial Regex Count();

    [Fact]
    public async Task The_loot_tab_lists_every_item_the_dungeon_drops()
    {
        var body = await Page();

        Assert.Equal(Listed(EntityType.Item), Counted(body));
        Assert.Contains($"Source: {PublicTypes.NameOf(Dungeon)}", body);
    }

    [Fact]
    public async Task A_filter_narrows_the_loot_and_keeps_the_dungeon()
    {
        var body = await Page("?slot=Weapon");

        Assert.Equal(Listed(EntityType.Item, row => row is ItemDocument { Slot: "Weapon" }), Counted(body));
    }

    [Fact]
    public async Task The_creatures_tab_lists_the_creatures_found_in_the_dungeon()
    {
        var body = await Page($"?{Components.Views.Dungeon.TabKey}={EntityTypes.Slug(EntityType.Creature)}");

        Assert.Equal(Listed(EntityType.Creature), Counted(body));
    }

    [Fact]
    public async Task A_filter_link_on_the_creatures_tab_keeps_the_tab()
    {
        var body = await Page($"?{Components.Views.Dungeon.TabKey}={EntityTypes.Slug(EntityType.Creature)}");
        var tab = $"{Ui.Path(Dungeon)}?{Components.Views.Dungeon.TabKey}={EntityTypes.Slug(EntityType.Creature)}&";

        Assert.Contains($"href=\"{tab}", body);
    }

    [Fact]
    public async Task The_rail_offers_no_source_to_choose()
    {
        var body = await Page();

        Assert.DoesNotMatch($@"href=""{Regex.Escape(Ui.Path(Dungeon))}[^""]*source=", body);
    }

    [Fact]
    public async Task The_gallery_holds_the_loading_screen_and_every_floor_map()
    {
        var body = await Page();
        var images = (Dungeon.Background is null ? 0 : 1) + (Dungeon.Floors?.Count(floor => floor.Map is not null) ?? 0);

        Assert.Equal(images, Regex.Matches(body, "data-gallery-slide").Count);
    }

    [Fact]
    public async Task An_item_source_links_to_its_dungeon_and_holds_no_loot_table()
    {
        var item = Store.Entities.OfType<ItemDocument>().First(row =>
            PublicTypes.IsListed(row) && row.Sources?.Any(source => source.Dungeon?.Id == Dungeon.Id) is true);

        var response = await host.Client().GetAsync(Ui.Href(item), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"href=\"{Ui.Path(Dungeon)}\"", body);
        var panel = body.IndexOf("Location / Source", StringComparison.Ordinal);
        var next = body.IndexOf("cx-panel-head", panel, StringComparison.Ordinal);

        Assert.True(panel >= 0);
        Assert.DoesNotContain("<details", next < 0 ? body[panel..] : body[panel..next]);
    }
}
