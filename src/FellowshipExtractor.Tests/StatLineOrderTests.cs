using System.Text.Json;

using FellowshipExtractor.Api.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class StatLineOrderTests
{
    private const int SkandisBands = 5226;

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static ItemDocument Bands()
    {
        var body = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Id == SkandisBands)
            .Select(document => document.Body)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Item {SkandisBands} is not in the export.");

        return new ItemDocument
        {
            Id = SkandisBands,
            Slot = body["slot"]?.GetValue<string>(),
            ArmorType = Enum.Parse<Fellowship.SDK.ArmorType>(body["armorType"]!.GetValue<string>()),
            Stats = body["stats"].Deserialize<IReadOnlyList<StatSlot>>(Reading),
        };
    }

    private static IReadOnlyList<StatLine> Lines() =>
        StatMagnitudes.For(
            Bands(),
            Export.Loaded.Settings.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            itemLevel: 360,
            new TooltipQuery());

    [Fact]
    public void SkandisBandsReadsInTheOrderTheGamePrints() =>
        Assert.Equal(
            ["Armor", "Stamina", "Intellect", "Haste"],
            Lines().Where(line => line.Guaranteed).Select(line => line.Stat));

    [Fact]
    public void ArmourLeadsAndTheDeclaredSlotsKeepTheirOwnOrder()
    {
        var declared = Bands().Stats!.Select(slot => slot.Stat);

        Assert.Equal(["Armor", .. declared], Lines().Where(line => line.Guaranteed).Select(line => line.Stat));
    }

    [Fact]
    public void ARolledLineFollowsEveryDeclaredOne()
    {
        var lines = Lines();

        Assert.Equal(lines.Count, lines.TakeWhile(line => line.Guaranteed).Count() + lines.Count(line => line.Rolled));
    }
}
