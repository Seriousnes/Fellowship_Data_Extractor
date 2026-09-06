using Fellowship.SDK.Client;

using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class SocketCardTests : BunitContext
{
    private readonly Export export;

    public SocketCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.Store);
    }

    private GemDocument AnyGem => export.Store.Entities.OfType<GemDocument>().First(gem => gem.Icon is not null);

    private IRenderedComponent<TipSocket> Draw(TooltipQuery query) =>
        Render<TipSocket>(p => p
            .Add(x => x.Entity, new ItemDocument { Id = 1, GemSlot = true })
            .Add(x => x.Query, query));

    private (string Rule, int Tier) Socketed => export.Store.Settings.StatCalculation.RarityAdditions
        .SelectMany(rule => rule.Value.Select(tier => (Rule: rule.Key, Tier: tier.Key, tier.Value)))
        .Where(row =>
            row.Value.GuaranteedSlots.Contains(CraftSlots.GemSocket, StringComparer.OrdinalIgnoreCase) ||
            row.Value.SpecialSlots.Any(slot =>
                slot.SlotName.Equals(CraftSlots.GemSocket, StringComparison.OrdinalIgnoreCase)))
        .Select(row => (row.Rule, row.Tier))
        .First();

    [Fact]
    public void The_row_draws_where_the_rule_set_and_tier_state_a_socket_and_nowhere_else()
    {
        var (rule, tier) = Socketed;
        var item = new ItemDocument { Id = 1, RuleSet = rule, Slot = "Head" };
        var settings = export.Store.Settings;
        var rarity = settings.Rarities.Single(entry => entry.Tier == tier);

        Assert.True(TipSocket.Draws(item, settings, rarity, new TooltipQuery()));
        Assert.False(TipSocket.Draws(item, settings, rarity, new TooltipQuery { NoSocket = true }));
        Assert.False(TipSocket.Draws(item, settings, null, new TooltipQuery()));

        var bare = new ItemDocument { Id = 1, Slot = "Head" };

        Assert.False(TipSocket.Draws(bare, settings, rarity, new TooltipQuery()));
    }

    [Fact]
    public void A_legendary_socket_stated_only_as_a_special_slot_still_draws()
    {
        var settings = export.Store.Settings;

        var legendary = settings.StatCalculation.RarityAdditions
            .SelectMany(rule => rule.Value.Select(tier => (Rule: rule.Key, Tier: tier.Key, tier.Value)))
            .First(row => row.Value.SpecialSlots.Any(slot =>
                slot.SlotName.Equals(CraftSlots.GemSocket, StringComparison.OrdinalIgnoreCase)));

        Assert.True(TipSocket.Draws(
            new ItemDocument { Id = 1, RuleSet = legendary.Rule, Slot = "Head" },
            settings,
            settings.Rarities.Single(entry => entry.Tier == legendary.Tier),
            new TooltipQuery()));
    }

    [Fact]
    public void An_empty_socket_reads_the_words_the_widget_ships_beside_the_empty_plate()
    {
        var row = Draw(new TooltipQuery()).Find(".sock");

        Assert.Equal(TooltipView.GemSocketLabel, row.QuerySelector(".gn")!.TextContent);
        Assert.Equal(export.Chrome.GemSocketPlate, row.QuerySelector("img.gi")!.GetAttribute("src"));
    }

    [Fact]
    public void A_socketed_gem_reads_its_own_name_beside_its_own_icon()
    {
        var gem = AnyGem;

        var row = Draw(new TooltipQuery { Gem = gem.Id }).Find(".sock");

        Assert.Equal(PublicTypes.NameOf(gem), row.QuerySelector(".gn")!.TextContent);
        Assert.Equal(export.Chrome.Art(gem.Icon), row.QuerySelector("img.gi")!.GetAttribute("src"));
    }

    [Fact]
    public void A_gem_id_the_store_does_not_carry_falls_back_to_the_empty_socket()
    {
        var unknown = export.Store.Entities.Max(row => row.Id) + 1;

        var row = Draw(new TooltipQuery { Gem = unknown }).Find(".sock");

        Assert.Equal(TooltipView.GemSocketLabel, row.QuerySelector(".gn")!.TextContent);
    }

    [Fact]
    public void The_empty_plate_composes_under_the_ui_prefix()
    {
        var url = export.Chrome.GemSocketPlate;
        var name = url[(url.LastIndexOf('/') + 1)..];

        Assert.Equal("T_Icons_Gems_EmptySlot.png", name);
        Assert.EndsWith($"/{ChromeAssetService.Prefix}/{name}", url, StringComparison.Ordinal);
    }

    [Fact]
    public void The_gem_the_caller_names_reaches_the_query_from_the_request()
    {
        var query = TooltipRequests.Read(new TooltipRequest { Gem = 35 }, export.Store, out var error);

        Assert.Null(error);
        Assert.Equal(35, query.Gem);
    }
}
