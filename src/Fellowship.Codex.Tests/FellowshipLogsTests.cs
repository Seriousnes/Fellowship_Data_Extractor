using System.Text.Json;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class FellowshipLogsTests(Export export)
{
    private const string Payload = "combatant-info-events.json";

    private static JsonDocument Load() =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "FellowshipLogs", Payload)));

    public static IEnumerable<object[]> Combatants()
    {
        using var payload = Load();

        for (var at = 0; at < FellowshipLogs.Combatants(payload.RootElement).Count; at++) yield return [at];
    }

    private static (JsonElement Entry, JsonElement Owner)[] Worn(JsonDocument payload, int? combatant = null)
    {
        var events = FellowshipLogs.Combatants(payload.RootElement);
        var chosen = combatant is { } at ? [events[at]] : events;

        return [.. chosen.SelectMany(owner => FellowshipLogs.Gear(owner).Select(entry => (entry, owner)))];
    }

    [Fact]
    public void The_payload_carries_several_combatants_and_every_one_wears_gear()
    {
        using var payload = Load();

        var events = FellowshipLogs.Combatants(payload.RootElement);

        Assert.NotEmpty(events);
        Assert.All(events, owner => Assert.NotEmpty(FellowshipLogs.Gear(owner)));
    }

    [Theory]
    [MemberData(nameof(Combatants))]
    public void Every_gear_entry_resolves_to_an_item_the_store_carries(int combatant)
    {
        using var payload = Load();

        var worn = Worn(payload, combatant);

        Assert.NotEmpty(worn);

        foreach (var (entry, owner) in worn)
        {
            FellowshipLogs.Read(entry, export.Store, out var itemId, out var unresolved, owner);

            Assert.NotEmpty(export.Store.All(EntityType.Item, itemId));
            Assert.Empty(unresolved);
        }
    }

    [Theory]
    [MemberData(nameof(Combatants))]
    public void Every_gear_entry_round_trips_through_import_and_export(int combatant)
    {
        using var payload = Load();

        foreach (var (entry, owner) in Worn(payload, combatant))
        {
            var query = FellowshipLogs.Read(entry, export.Store, out var itemId, out _, owner);
            var item = export.Store.All(EntityType.Item, itemId).OfType<ItemDocument>().First();

            var written = FellowshipLogs.Write(item, query, export.Store);

            using var again = JsonDocument.Parse(written.ToJsonString());

            var back = FellowshipLogs.Read(again.RootElement, export.Store, out var againId, out _);

            Assert.Equal(itemId, againId);
            Assert.Equal(query.Rarity, back.Rarity);
            Assert.Equal(query.ItemLevel, back.ItemLevel);
            Assert.Equal(query.Upgrades, back.Upgrades);
            Assert.Equal(query.MaxUpgrades, back.MaxUpgrades);
            Assert.Equal(query.NoSocket, back.NoSocket);
            Assert.Equal(query.Gem, back.Gem);
            Assert.Equal(query.PotentialPower, back.PotentialPower);
            Assert.Equal(query.Armor, back.Armor);
            Assert.Equal(query.AllocatedTraits, back.AllocatedTraits);
            Assert.Equal(query.Modifiers, back.Modifiers);
            Assert.Equal(query.ImbuedTraits, back.ImbuedTraits);

            Assert.Equal(
                query.Stats.OrderBy(stat => stat.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(stat => (stat.Key, string.Join(',', stat.Value))),
                back.Stats.OrderBy(stat => stat.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(stat => (stat.Key, string.Join(',', stat.Value))));
        }
    }

    [Fact]
    public void A_repeated_stat_keeps_one_entry_per_roll()
    {
        using var payload = Load();

        var repeated = Worn(payload)
            .Select(worn => FellowshipLogs.Read(worn.Entry, export.Store, out _, out _, worn.Owner))
            .First(query => query.Stats.Any(stat => stat.Value.Count > 1));

        Assert.Contains(repeated.Stats, stat => stat.Value.Count > 1);
    }

    [Fact]
    public void A_weapon_takes_the_imbued_traits_the_combatant_allocated_points_to()
    {
        using var payload = Load();

        var imbued = Worn(payload)
            .Select(worn => FellowshipLogs.Read(worn.Entry, export.Store, out _, out _, worn.Owner))
            .Where(query => query.ImbuedTraits.Count > 0)
            .ToArray();

        Assert.NotEmpty(imbued);

        Assert.All(imbued, query => Assert.All(query.ImbuedTraits, trait =>
        {
            Assert.True(trait.Rank > 0);
            Assert.NotEmpty(export.Store.All(TooltipView.TraitType, trait.Id));
        }));
    }

    [Fact]
    public void A_blessing_resolves_through_the_per_hero_ability_rank_id()
    {
        var blessing = export.Store.Entities
            .OfType<BlessingDocument>()
            .First(row => row.AbilityRanks is { Count: > 0 });

        var (_, rankId) = blessing.AbilityRanks!.First();

        Assert.Equal(
            blessing.Id,
            FellowshipLogs.Blessing(rankId + FellowshipLogs.BlessingBase, export.Store));
    }

    [Fact]
    public void An_id_that_resolves_to_nothing_is_named_rather_than_dropped()
    {
        using var payload = Load();

        var entry = Worn(payload).First(worn => worn.Entry.TryGetProperty("traits", out _)).Entry;

        using var broken = JsonDocument.Parse(
            entry.GetRawText().Replace("\"id\": 30000", "\"id\": 39999", StringComparison.Ordinal));

        FellowshipLogs.Read(broken.RootElement, export.Store, out _, out var unresolved);

        Assert.NotEmpty(unresolved);
    }

    [Fact]
    public void The_attribute_table_names_the_nine_an_item_can_carry()
    {
        Assert.Equal(9, FellowshipLogs.Attributes.Count);

        foreach (var (_, stat) in FellowshipLogs.Attributes)
        {
            Assert.NotNull(export.Store.StatDisplay(stat));
            Assert.Contains(stat, export.Store.Settings.StatCalculation.Attributes.Keys, StringComparer.Ordinal);
        }
    }
}
