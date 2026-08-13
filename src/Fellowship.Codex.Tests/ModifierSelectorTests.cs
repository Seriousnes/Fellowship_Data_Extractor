using Fellowship.Codex.Components.Tooltip;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ModifierSelectorTests : BunitContext
{
    private readonly Export export;

    public ModifierSelectorTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Store);
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static ItemDocument Worn(string slot) => new()
    {
        Id = 1,
        Slot = slot,
        RuleSet = "Default_NoGemSlot",
        Stats = [new StatSlot("Stamina", 40) { Kind = SlotKinds.Primary }],
    };

    private RarityEntry Top => export.Store.Settings.Rarities.OrderBy(rung => rung.Tier).Last();

    private (IRenderedComponent<ModifierSelector> Dialog, List<CraftCandidate> Picked, List<int> Shut) Open(
        CraftSlot slot, TooltipQuery query, ItemDocument? item = null)
    {
        var picked = new List<CraftCandidate>();
        var shut = new List<int>();

        var dialog = Render<ModifierSelector>(p => p
            .Add(x => x.Slot, slot)
            .Add(x => x.Item, item ?? Worn("Head"))
            .Add(x => x.Query, query)
            .Add(x => x.Rung, Top)
            .Add(x => x.OnPick, EventCallback.Factory.Create<CraftCandidate>(this, picked.Add))
            .Add(x => x.OnShut, EventCallback.Factory.Create(this, () => shut.Add(1))));

        return (dialog, picked, shut);
    }

    [Fact]
    public void A_stat_slot_offers_the_pool_the_settings_state_and_the_value_each_would_print()
    {
        var slot = new CraftSlot(CraftSlots.RandomStat, 0, [CraftSlots.RandomStat]);
        var (dialog, _, _) = Open(slot, new TooltipQuery { ItemLevel = 360 });

        var choices = dialog.FindAll("button.cx-sel-choice");

        Assert.Equal(export.Store.Settings.StatCalculation.RandomStatPool.Count, choices.Count);

        Assert.All(choices, choice =>
            Assert.False(string.IsNullOrWhiteSpace(choice.QuerySelector(".cx-sel-value")!.TextContent)));
    }

    [Fact]
    public void A_roll_slot_offers_every_kind_it_accepts_under_its_own_heading()
    {
        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.ItemTrait, CraftSlots.AbilityRank, CraftSlots.GemPower]);
        var (dialog, _, _) = Open(slot, new TooltipQuery());

        var headings = dialog.FindAll(".cx-sel-kind").Select(kind => kind.TextContent.Trim()).ToArray();

        Assert.Contains(TooltipView.Label(ModifierKind.Trait), headings);
        Assert.Contains(TooltipView.Label(ModifierKind.Blessing), headings);
        Assert.Contains(TooltipView.Label(ModifierKind.Essence), headings);
    }

    [Fact]
    public void A_modifier_slot_offers_no_stat_and_a_stat_slot_offers_no_modifier()
    {
        var settings = export.Store.Settings;

        var roll = settings.StatCalculation.RarityAdditions
            .SelectMany(rule => rule.Value.Select(tier => (Rule: rule.Key, Tier: tier.Key)))
            .Select(row => ItemCraft.Slots(
                new ItemDocument { Id = 1, RuleSet = row.Rule, Slot = "Head" },
                settings,
                settings.Rarities.Single(rung => rung.Tier == row.Tier)))
            .SelectMany(slots => slots)
            .First(slot =>
                slot.SlotName == CraftSlots.RandomRolls &&
                slot.Accepts.Contains(CraftSlots.RandomStat, StringComparer.Ordinal));

        var (modifiers, _, _) = Open(roll, new TooltipQuery());

        Assert.DoesNotContain(
            modifiers.FindAll("button.cx-sel-choice"),
            choice => choice.GetAttribute("data-kind") == CraftSlots.RandomStat);

        Assert.All(
            modifiers.FindAll("button.cx-sel-choice"),
            choice => Assert.NotNull(ItemCraft.KindOf(choice.GetAttribute("data-kind")!)));

        var (stats, _, _) = Open(
            new CraftSlot(CraftSlots.RandomStat, 0, [CraftSlots.RandomStat]), new TooltipQuery());

        Assert.All(
            stats.FindAll("button.cx-sel-choice"),
            choice => Assert.Equal(CraftSlots.RandomStat, choice.GetAttribute("data-kind")));
    }

    [Fact]
    public void A_modifier_choice_draws_the_browse_row_art_and_carries_its_hover_address()
    {
        var trait = export.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null);

        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.ItemTrait]);

        var (dialog, _, _) = Open(slot, new TooltipQuery());

        var choice = dialog.Find($"button.cx-sel-choice[data-id=\"{trait.Id}\"]");

        Assert.NotNull(choice.QuerySelector(".cx-sel-art figure.fx-slot"));
        Assert.Equal(export.View.Tip(trait), choice.GetAttribute("data-tip"));
        Assert.Equal(export.View.Name(trait), choice.QuerySelector(".cx-sel-name")!.TextContent);
    }

    [Fact]
    public void A_stat_choice_draws_no_art_because_a_stat_is_no_entity()
    {
        var slot = new CraftSlot(CraftSlots.RandomStat, 0, [CraftSlots.RandomStat]);

        var (dialog, _, _) = Open(slot, new TooltipQuery());

        Assert.All(
            dialog.FindAll("button.cx-sel-choice"),
            choice => Assert.Null(choice.QuerySelector(".cx-sel-art")));
    }

    [Fact]
    public void The_filter_narrows_the_choices_by_name()
    {
        var trait = export.Store.Entities.OfType<TraitDocument>().First(row => row.Name is { Length: > 0 });

        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.ItemTrait]);

        var (dialog, _, _) = Open(slot, new TooltipQuery());

        var all = dialog.FindAll("button.cx-sel-choice").Count;

        dialog.Find("input.cx-sel-find").Input(trait.Name!);

        var narrowed = dialog.FindAll("button.cx-sel-choice");

        Assert.True(narrowed.Count < all);
        Assert.Contains(narrowed, choice => choice.GetAttribute("data-id") == trait.Id.ToString());

        Assert.All(narrowed, choice => Assert.Contains(
            trait.Name!,
            choice.QuerySelector(".cx-sel-name")!.TextContent,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_filter_narrows_the_choices_by_id()
    {
        var trait = export.Store.Entities.OfType<TraitDocument>().First();

        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.ItemTrait]);

        var (dialog, _, _) = Open(slot, new TooltipQuery());

        dialog.Find("input.cx-sel-find").Input(trait.Id.ToString());

        var narrowed = dialog.FindAll("button.cx-sel-choice");

        Assert.Contains(narrowed, choice => choice.GetAttribute("data-id") == trait.Id.ToString());

        Assert.All(narrowed, choice => Assert.True(
            choice.GetAttribute("data-id")!.StartsWith(trait.Id.ToString(), StringComparison.Ordinal) ||
            choice.QuerySelector(".cx-sel-name")!.TextContent.Contains(
                trait.Id.ToString(), StringComparison.OrdinalIgnoreCase),
            choice.GetAttribute("data-id")));
    }

    [Fact]
    public void A_filter_that_matches_nothing_says_so_rather_than_drawing_an_empty_list()
    {
        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.ItemTrait]);

        var (dialog, _, _) = Open(slot, new TooltipQuery());

        dialog.Find("input.cx-sel-find").Input("no trait is called this");

        Assert.Empty(dialog.FindAll("button.cx-sel-choice"));
        Assert.False(string.IsNullOrWhiteSpace(dialog.Find("p.cx-sel-none").TextContent));
    }

    [Fact]
    public void A_choice_the_rules_forbid_is_disabled_and_states_its_reason()
    {
        var trait = export.Store.Entities.OfType<TraitDocument>().First();
        var already = new ItemModifier(ModifierKind.Trait, trait.Id, 1);

        var slot = new CraftSlot(CraftSlots.RandomRolls, 2, [CraftSlots.ItemTrait]);

        var (dialog, _, _) = Open(slot, new TooltipQuery { Modifiers = [already, already] });

        var choice = dialog.Find($"button.cx-sel-choice[data-id=\"{trait.Id}\"]");

        Assert.True(choice.HasAttribute("disabled"));
        Assert.Equal(CraftReasons.TwoOfAKind, choice.GetAttribute("title"));
    }

    [Fact]
    public void A_blessing_off_the_slots_pool_is_disabled_with_the_pool_reason()
    {
        var blessing = export.Store.Entities
            .OfType<BlessingDocument>()
            .First(row => row.Slots is { Count: > 0 });

        const string Elsewhere = "Offhand";

        Assert.DoesNotContain(Elsewhere, blessing.Slots!, StringComparer.OrdinalIgnoreCase);

        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.AbilityRank]);

        var (dialog, _, _) = Open(slot, new TooltipQuery(), Worn(Elsewhere));

        var choice = dialog.Find($"button.cx-sel-choice[data-id=\"{blessing.Id}\"]");

        Assert.True(choice.HasAttribute("disabled"));
        Assert.Equal(CraftReasons.NotInPool(Elsewhere), choice.GetAttribute("title"));
    }

    [Fact]
    public void Reopening_a_filled_slot_offers_what_it_already_holds()
    {
        var offered = export.Store.Entities
            .OfType<BlessingDocument>()
            .Where(row => row.Slots is { Count: > 0 })
            .Take(2)
            .ToArray();

        var worn = offered[0].Slots![0];

        var carried = offered
            .Select(blessing => new ItemModifier(ModifierKind.Blessing, blessing.Id, 1))
            .ToArray();

        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.AbilityRank]);

        var (dialog, _, _) = Open(slot, new TooltipQuery { Modifiers = carried }, Worn(worn));

        var standing = dialog.Find($"button.cx-sel-choice[data-id=\"{offered[0].Id}\"]");

        Assert.False(standing.HasAttribute("disabled"));
    }

    [Fact]
    public void Picking_a_choice_reports_the_candidate_it_stands_for()
    {
        var trait = export.Store.Entities.OfType<TraitDocument>().First(row => row.MaxRank is > 0);
        var slot = new CraftSlot(CraftSlots.RandomRolls, 0, [CraftSlots.ItemTrait]);

        var (dialog, picked, _) = Open(slot, new TooltipQuery());

        dialog.Find($"button.cx-sel-choice[data-id=\"{trait.Id}\"]").Click();

        var candidate = Assert.Single(picked);

        Assert.Equal(CraftSlots.ItemTrait, candidate.Accepts);
        Assert.Equal(trait.Id, candidate.Id);
        Assert.Equal(1, candidate.Amount);
    }

    [Fact]
    public void The_dialog_shuts_on_the_close_button_on_the_backdrop_and_on_escape()
    {
        var slot = new CraftSlot(CraftSlots.RandomStat, 0, [CraftSlots.RandomStat]);

        var (byButton, _, shutByButton) = Open(slot, new TooltipQuery());
        byButton.Find("button.cx-sel-close").Click();
        Assert.Single(shutByButton);

        var (byBackdrop, _, shutByBackdrop) = Open(slot, new TooltipQuery());
        byBackdrop.Find("dialog").Click();
        Assert.Single(shutByBackdrop);

        var (byEscape, _, shutByEscape) = Open(slot, new TooltipQuery());
        byEscape.Find("dialog").TriggerEvent("onclose", new EventArgs());
        Assert.Single(shutByEscape);
    }

    [Fact]
    public void A_socket_slot_offers_the_gems_the_store_carries()
    {
        var slot = new CraftSlot(CraftSlots.GemSocket, 0, [CraftSlots.GemSocket]);

        var (dialog, _, _) = Open(slot, new TooltipQuery());

        Assert.Equal(
            export.Store.Entities.OfType<GemDocument>().Count(),
            dialog.FindAll("button.cx-sel-choice").Count);
    }

    [Fact]
    public void A_stat_slot_names_a_stat_the_way_the_game_prints_it()
    {
        var stat = export.Store.Settings.StatCalculation.RandomStatPool
            .First(named => !named.Equals(export.Store.StatName(named), StringComparison.Ordinal));

        var slot = new CraftSlot(CraftSlots.RandomStat, 0, [CraftSlots.RandomStat]);

        var (dialog, picked, _) = Open(slot, new TooltipQuery { ItemLevel = 360 });

        var choice = dialog.FindAll("button.cx-sel-choice")
            .First(row => row.QuerySelector(".cx-sel-name")!.TextContent == export.Store.StatName(stat));

        choice.Click();

        Assert.Equal(stat, Assert.Single(picked).Name);
    }

    [Fact]
    public void The_arrow_keys_walk_the_choices()
    {
        var slot = new CraftSlot(CraftSlots.RandomStat, 0, [CraftSlots.RandomStat]);
        var (dialog, _, _) = Open(slot, new TooltipQuery());

        dialog.Find("dialog").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var walked = dialog.FindAll("button.cx-sel-choice");

        Assert.True(walked[1].HasAttribute("data-active"));
        Assert.Single(walked.Where(choice => choice.HasAttribute("data-active")));

        dialog.Find("dialog").KeyDown(new KeyboardEventArgs { Key = "End" });

        var ended = dialog.FindAll("button.cx-sel-choice");

        Assert.True(ended[^1].HasAttribute("data-active"));
        Assert.Single(ended.Where(choice => choice.HasAttribute("data-active")));
    }
}
