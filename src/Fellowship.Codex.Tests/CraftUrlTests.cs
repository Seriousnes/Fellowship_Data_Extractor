using Fellowship.Codex.Components.Tooltip;

using Microsoft.AspNetCore.Components;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CraftUrlTests : BunitContext
{
    private readonly Export export;

    public CraftUrlTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Store);
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private ItemDocument Wearable => export.Store.Entities
        .OfType<ItemDocument>()
        .First(item => item.Stats is { Count: > 0 } && item.RuleSet is not null && item.Rarities is { Count: > 1 });

    private IRenderedComponent<Craft> Island(string search = "") =>
        Render<Craft>(p => p
            .Add(x => x.Slug, EntityTypes.Slug(EntityType.Item))
            .Add(x => x.Id, Wearable.Id)
            .Add(x => x.Search, search));

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    private RarityEntry Top
    {
        get
        {
            var tier = (int)Wearable.Rarities!.Max();

            return export.Store.Settings.Rarities.Single(rung => rung.Tier == tier);
        }
    }

    [Fact]
    public void The_island_draws_the_card_the_query_asks_for()
    {
        var island = Island($"?rarity={Top.Tier}&itemLevel=360");

        Assert.Contains(
            Top.DisplayName, island.Find($".{TooltipView.Root} .rar").TextContent, StringComparison.Ordinal);
        Assert.Contains("360", island.Find($".{TooltipView.Root} .power").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_a_rung_writes_the_rarity_parameter()
    {
        var island = Island();
        var rung = island.FindAll("button[data-knob=rarity]")[^1];
        var tier = rung.GetAttribute("data-choice");

        rung.Click();

        Assert.Contains($"rarity={tier}", Nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_the_same_rung_twice_drops_the_rarity_parameter()
    {
        var tier = export.Store.Settings.Rarities
            .First(rung => Wearable.Rarities!.Any(carry => (int)carry == rung.Tier))
            .Tier;

        var island = Island($"?rarity={tier}");

        island.FindAll($"button[data-knob=rarity][data-choice=\"{tier}\"]")[0].Click();

        Assert.DoesNotContain("rarity=", Nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_parameter_the_address_already_carries_survives_an_edit()
    {
        var island = Island("?itemLevel=360&randomStats=Haste");

        island.FindAll("button[data-knob=rarity]")[0].Click();

        Assert.Contains("itemLevel=360", Nav.Uri, StringComparison.Ordinal);
        Assert.Contains("randomStats=Haste", Nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void The_island_reads_a_written_parameter_back_to_the_same_query()
    {
        var island = Island("?itemLevel=360&upgrades=8");

        Assert.Equal("360", island.Find("input[data-knob=itemLevel]").GetAttribute("value"));
        Assert.Equal("8", island.Find("input[data-knob=upgrades]").GetAttribute("value"));
    }

    [Fact]
    public void An_edit_the_island_writes_reads_back_as_the_card_it_asked_for()
    {
        var first = Island();

        first.FindAll($"button[data-knob=rarity][data-choice=\"{Top.Tier}\"]")[0].Click();

        var written = new Uri(Nav.Uri).Query;

        Assert.Contains($"rarity={Top.Tier}", written, StringComparison.Ordinal);

        var again = Island(written);

        Assert.Contains(
            Top.DisplayName, again.Find($".{TooltipView.Root} .rar").TextContent, StringComparison.Ordinal);

        Assert.Equal(
            "true",
            again.Find($"button[data-knob=rarity][data-choice=\"{Top.Tier}\"]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_copy_link_button_sits_with_the_knobs()
    {
        var island = Island("?itemLevel=360");

        var copy = island.Find("button.cx-craft-copy");

        Assert.Equal("button", copy.GetAttribute("type"));
        Assert.Contains("Copy link", copy.TextContent, StringComparison.Ordinal);
    }

    private static string Payload() => File.ReadAllText(CombatantInfo.Payload);

    [Fact]
    public void Pasting_one_gear_entry_writes_the_parameters_it_states()
    {
        CombatantInfo.Require();

        var island = Island();

        island.Find("button.cx-craft-import").Click();

        using var payload = System.Text.Json.JsonDocument.Parse(Payload());

        var entry = FellowshipLogs.Gear(payload.RootElement)[0];

        island.Find("textarea.cx-craft-paste").Change(entry.GetRawText());
        island.Find("button.cx-craft-read").Click();

        Assert.Contains("itemLevel=", Nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Pasting_a_whole_combatantinfo_lists_the_gear_so_one_piece_can_be_chosen()
    {
        CombatantInfo.Require();

        var island = Island();

        island.Find("button.cx-craft-import").Click();
        island.Find("textarea.cx-craft-paste").Change(Payload());
        island.Find("button.cx-craft-read").Click();

        Assert.NotEmpty(island.FindAll("ul.cx-craft-pieces li button"));
    }

    [Fact]
    public void Choosing_a_piece_navigates_to_the_item_that_piece_names()
    {
        CombatantInfo.Require();

        var island = Island();

        island.Find("button.cx-craft-import").Click();
        island.Find("textarea.cx-craft-paste").Change(Payload());
        island.Find("button.cx-craft-read").Click();

        island.FindAll("ul.cx-craft-pieces li button")[0].Click();

        using var payload = System.Text.Json.JsonDocument.Parse(Payload());

        var first = FellowshipLogs.Gear(payload.RootElement)[0];

        Assert.Contains(
            $"/{EntityTypes.Slug(EntityType.Item)}/{first.GetProperty("id").GetInt32()}",
            Nav.Uri,
            StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_paste_is_said_rather_than_dropped()
    {
        var island = Island();

        island.Find("button.cx-craft-import").Click();
        island.Find("textarea.cx-craft-paste").Change("not json");
        island.Find("button.cx-craft-read").Click();

        Assert.False(string.IsNullOrWhiteSpace(island.Find("p.cx-craft-said").TextContent));
    }

    [Fact]
    public void Exporting_writes_the_gear_shape_back()
    {
        var island = Island("?itemLevel=360");

        island.Find("button.cx-craft-export").Click();

        var written = island.Find("textarea.cx-craft-paste").GetAttribute("value");

        Assert.NotNull(written);
        Assert.Contains("\"itemLevel\":360", written, StringComparison.Ordinal);
        Assert.Contains($"\"id\":{Wearable.Id}", written, StringComparison.Ordinal);
    }

    [Fact]
    public void What_the_island_exports_reads_back_as_the_same_item()
    {
        var island = Island("?itemLevel=360");

        island.Find("button.cx-craft-export").Click();

        var written = island.Find("textarea.cx-craft-paste").GetAttribute("value")!;

        using var entry = System.Text.Json.JsonDocument.Parse(written);

        FellowshipLogs.Read(entry.RootElement, export.Store, out var itemId, out var unresolved);

        Assert.Equal(Wearable.Id, itemId);
        Assert.Empty(unresolved);
    }
}

