using System.Text.Json;

using Fellowship.SDK;
using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public class CodexAddressTests
{
    private static readonly TooltipRequest Everything = new()
    {
        ShowIcon = true,
        Mobile = true,
        Hero = "Gunde",
        Difficulty = 3,
        State = "equipped",
        EquippedTier = 5,
        Rarity = "6",
        ItemLevel = 360,
        Upgrades = 4,
        MaxUpgrades = 6,
        NoSocket = true,
        Gem = 35,
        PotentialPower = 12.5m,
        Armor = 968m,
        AllocatedTraits = 2,
        HasImbuedTraits = true,
        RandomStats = ["Stamina", "Haste"],
        Modifiers = [new ItemModifier(ModifierKind.Blessing, 12, 3)],
        ImbuedTraits = [new ImbuedTrait(456, 2)],
        Stats = new Dictionary<string, StatRolls>
        {
            ["Strength"] = new() { Rating = [400m, 134m] },
            ["Haste"] = new() { Rating = [100m], Percentage = [10m] },
        },
    };

    private static IReadOnlySet<string> Declared() => RequestProperties.Of(typeof(TooltipRequest));

    private static JsonDocument Body(TooltipRequest request) =>
        JsonDocument.Parse(JsonSerializer.Serialize(request, CodexJson.Options));

    private static IReadOnlyList<string> Names(TooltipRequest request)
    {
        using var body = Body(request);

        return [.. body.RootElement.EnumerateObject().Select(property => property.Name)];
    }

    [Fact]
    public void Every_parameter_the_body_writes_is_one_a_tooltip_request_declares()
    {
        var declared = Declared();

        foreach (var name in Names(Everything)) Assert.True(declared.Contains(name), name);
    }

    [Fact]
    public void Every_property_of_the_request_reaches_the_body()
    {
        var written = Names(Everything).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in Declared()) Assert.True(written.Contains(name), name);
    }

    [Fact]
    public void A_stat_is_written_once_per_magnitude_it_stands_at()
    {
        using var body = Body(new TooltipRequest
        {
            Stats = new Dictionary<string, StatRolls>
            {
                ["Strength"] = new() { Rating = [400m, 134m] },
                ["Haste"] = new() { Rating = [100m] },
            },
        });

        var stats = body.RootElement.GetProperty("stats");

        Assert.Equal(
            ["Strength=400", "Strength=134", "Haste=100"],
            stats.EnumerateObject().SelectMany(stat => stat.Value
                .GetProperty("rating")
                .EnumerateArray()
                .Select(value => $"{stat.Name}={value.GetDecimal()}")));
    }

    [Fact]
    public void A_tooltip_is_addressed_by_its_type_and_id_alone()
    {
        Assert.Equal("api/item/5218/tooltip", CodexAddresses.Tooltip(EntityType.Item, 5218));
    }

    [Fact]
    public void A_modifier_and_a_trait_are_written_as_the_tooltip_reads_them_back()
    {
        Assert.True(ItemModifiers.TryParse("blessing:12:3", out var modifiers));
        Assert.Equal("blessing:12:3", modifiers.Values.Single().ToString());

        Assert.True(ImbuedTraits.TryParse("456:2", out var traits));
        Assert.Equal("456:2", traits.Values.Single().ToString());
    }

    [Fact]
    public void A_page_and_a_record_address_the_same_entity()
    {
        Assert.Equal("item/5218?hero=Gunde", CodexAddresses.Page(EntityType.Item, 5218, "Gunde"));
        Assert.Equal("api/item/5218?hero=Gunde", CodexAddresses.Record(EntityType.Item, 5218, "Gunde"));
        Assert.Equal("ability/1964", CodexAddresses.Page(EntityType.Ability, 1964));
    }
}
