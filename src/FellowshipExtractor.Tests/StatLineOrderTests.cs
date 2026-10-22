using System.Text.Json;

using Fellowship.Codex.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class StatLineOrderTests
{
    private const int WarmasterGreaves = 5900;

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static ItemDocument Greaves()
    {
        var body = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Id == WarmasterGreaves)
            .Select(document => document.Body)
            .FirstOrDefault(body => body["armorType"] is not null)
            ?? throw new InvalidOperationException($"Item {WarmasterGreaves} is not in the export.");

        return new ItemDocument
        {
            Id = WarmasterGreaves,
            Slot = body["slot"]?.GetValue<string>(),
            ArmorType = Enum.Parse<Fellowship.SDK.ArmorType>(body["armorType"]!.GetValue<string>()),
            Stats = body["stats"].Deserialize<IReadOnlyList<StatSlot>>(Reading),
        };
    }

    private static IReadOnlyList<StatLine> Lines() =>
        StatMagnitudes.For(
            Greaves(),
            Export.Loaded.Settings.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            itemLevel: 360,
            new TooltipQuery());

    [Fact]
    public void ArmourLeadsAndTheDeclaredSlotsKeepTheirOwnOrder()
    {
        var declared = Greaves().Stats!.Select(slot => slot.Stat);

        Assert.Equal(["Armor", .. declared], Lines().Where(line => line.Guaranteed).Select(line => line.Stat));
    }
}
