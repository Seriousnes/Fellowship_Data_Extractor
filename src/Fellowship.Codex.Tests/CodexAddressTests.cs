using System.Reflection;
using System.Web;

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
    };

    private static IReadOnlySet<string> Declared() =>
        typeof(ItemTooltipRequest).Assembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("TooltipRequest", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<KeyValuePair<string, string?>> Written(TooltipRequest asked)
    {
        var address = CodexAddresses.Tooltip(EntityType.Item, 5218, asked);
        var query = HttpUtility.ParseQueryString(address[(address.IndexOf('?') + 1)..]);

        return [.. query.AllKeys.SelectMany(key =>
            (query.GetValues(key) ?? []).Select(value => KeyValuePair.Create(key!, (string?)value)))];
    }

    [Fact]
    public void Every_parameter_the_address_writes_is_one_a_tooltip_request_declares()
    {
        var declared = Declared();

        foreach (var (key, _) in Written(Everything)) Assert.Contains(key, declared);
    }

    [Fact]
    public void Every_property_of_the_request_reaches_the_address()
    {
        var written = Written(Everything).Select(pair => pair.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var carried = typeof(TooltipRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name is not (nameof(TooltipRequest.Stats) or nameof(TooltipRequest.Parameters)))
            .Select(property => property.Name);

        foreach (var property in carried) Assert.Contains(property, written);
    }

    [Fact]
    public void A_stat_is_written_once_per_magnitude_it_stands_at()
    {
        var written = Written(new TooltipRequest
        {
            Stats = new Dictionary<string, IReadOnlyList<decimal>>
            {
                ["Strength"] = [400m, 134m],
                ["Haste"] = [100m],
            },
        });

        Assert.Equal(
            ["Strength=400", "Strength=134", "Haste=100"],
            written.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    [Fact]
    public void A_request_asking_nothing_writes_no_query()
    {
        Assert.Equal("api/item/5218/tooltip", CodexAddresses.Tooltip(EntityType.Item, 5218));
        Assert.Equal("api/item/5218/tooltip", CodexAddresses.Tooltip(EntityType.Item, 5218, new TooltipRequest()));
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
