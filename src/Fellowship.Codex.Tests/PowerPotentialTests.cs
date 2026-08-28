using System.Text.RegularExpressions;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Tests;

public partial class PowerPotentialTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private static readonly IEntityStoreService Store = RouteTests.Factory.Store;

    private const string Page = "/tools/power-potential";

    private Task<string> Draw(string where = Page) =>
        client.GetStringAsync(where, Xunit.TestContext.Current.CancellationToken);

    [GeneratedRegex("<td class=\"cx-pp-amount\">([^<]+)</td>")]
    private static partial Regex Amounts();

    [GeneratedRegex("<article class=\"cx-pp-case\">", RegexOptions.Singleline)]
    private static partial Regex Cases();

    private static SettingsFile Settings => Store.Settings;

    private static float Summed(int id, string rung, int? level)
    {
        var item = (ItemDocument)Store.All(EntityType.Item, id)[0];
        var query = new TooltipQuery { Rarity = rung, ItemLevel = level };

        var given = ItemPower.Inputs(
            item, Settings, Store.Rarity(rung, false), query, level ?? item.ItemLevel ?? 0);

        Assert.NotNull(given);

        return ItemPower.Potential(given, Settings);
    }

    [Fact]
    public async Task The_page_names_every_term_the_settings_carry()
    {
        var page = (await Draw()).Bare();
        var factors = Settings.Items.PowerPotential;

        foreach (var term in new[]
        {
            "Item level", "Tempering ceiling", "Rerollable slots", "Gem socket", "Set bonus",
            "Legendary",
        })
        {
            Assert.Contains($">{term}</td>", page, StringComparison.Ordinal);
        }

        foreach (var factor in new[]
        {
            factors.ItemLevelScoreFactor, factors.TemperScoreFactor, factors.ModSlotScoreFactor,
            factors.GemSocketScoreFactor, factors.WeaponAbilityScoreFactor,
            factors.RelicAbilityScoreFactor, factors.WeaponTraitTreeScoreFactor,
            factors.NecklaceTraitTreeScoreFactor, factors.SetBonusScoreFactor,
            factors.LegendaryScoreFactor,
        })
        {
            Assert.Contains($">{CodexView.Number(factor)}</td>", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_ability_and_trait_tree_terms_are_named_for_the_slots_the_game_prints()
    {
        var page = (await Draw()).Bare();
        var items = Settings.Items;

        var relic = ItemPower.Printed(items, ItemPower.TrinketType);
        var neck = ItemPower.Printed(items, ItemPower.NecklaceType);

        Assert.NotEqual(ItemPower.TrinketType, relic);
        Assert.NotEqual(ItemPower.NecklaceType, neck);

        Assert.DoesNotContain($">{ItemPower.TrinketType} ability<", page, StringComparison.Ordinal);
        Assert.DoesNotContain($">{ItemPower.NecklaceType} trait tree<", page, StringComparison.Ordinal);

        Assert.Contains($">{relic} ability</td>", page, StringComparison.Ordinal);
        Assert.Contains($">{neck} trait tree</td>", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_slot_the_weights_cover_is_listed_with_its_own_weight()
    {
        var page = (await Draw()).Bare();

        Assert.NotEmpty(Settings.StatCalculation.SlotWeights);

        foreach (var (slot, weights) in Settings.StatCalculation.SlotWeights)
        {
            Assert.Contains($">{slot}</td>", page, StringComparison.Ordinal);
            Assert.Contains($">{CodexView.Number(weights.StatWeight)}</td>", page, StringComparison.Ordinal);
        }

        Assert.Contains($"{Settings.StatCalculation.SlotWeights.Count} slots", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_worked_example_totals_what_the_calculator_totals()
    {
        var page = (await Draw()).Bare();

        Assert.Equal(7, Cases().Count(page));

        foreach (var (id, rung, level) in new (int, string, int?)[]
        {
            (5326, "Epic", null), (5184, "Regal", null), (5254, "Epic", null), (5252, "Regal", null),
            (5235, "Heroic", 255), (5309, "Regal", null), (5221, "Legendary", null),
        })
        {
            Assert.Contains(
                $">{CodexView.Number(Summed(id, rung, level))} rounded</td>",
                page,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_examples_print_the_totals_the_cards_print()
    {
        var printed = Amounts().Matches((await Draw()).Bare()).Select(hit => hit.Groups[1].Value).ToArray();

        foreach (var total in new[] { "825", "485", "545", "470", "617", "1342" })
        {
            Assert.Contains(total, printed, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task The_tools_menu_carries_the_page()
    {
        var page = (await Draw("/")).Bare();

        Assert.Contains($"href=\"{Page}\"", page, StringComparison.Ordinal);
    }
}
