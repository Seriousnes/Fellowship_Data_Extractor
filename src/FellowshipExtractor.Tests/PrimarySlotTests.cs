using System.Text.Json;

using FellowshipExtractor.Api.Endpoints;
using FellowshipExtractor.Api.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class PrimarySlotTests
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

    private static decimal Line(string stat) =>
        Lines().Single(line => string.Equals(line.Stat, stat, StringComparison.OrdinalIgnoreCase)).Value;

    [Fact]
    public void SkandisBandsWeighsStaminaAndItsPrimaryTheSame()
    {
        var slots = Bands().Stats!;

        Assert.Equal(
            slots.Single(slot => slot.Stat == "Stamina").Weight,
            slots.Single(slot => slot.Kind == SlotKinds.Primary).Weight);
    }

    [Fact]
    public void APrimarySlotTakesTheShippedPrimaryMultiplier()
    {
        var settings = Export.Loaded.Settings.StatCalculation;

        var stated =
            (decimal)(settings.FixedSlot.StaminaMultiplierAtThreshold ?? 1) /
            (decimal)(settings.FixedSlot.PrimaryMultiplier ?? 1);

        var lines = StatMagnitudes.For(
            new ItemDocument
            {
                Id = 1,
                Slot = "Weapon",
                Stats =
                [
                    new StatSlot("Stamina", 50) { Kind = SlotKinds.Fixed },
                    new StatSlot("Strength", 50) { Kind = SlotKinds.Primary },
                ],
            },
            settings,
            new HashSet<string>(["Strength"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            itemLevel: 2000,
            new TooltipQuery());

        var stamina = lines.Single(line => line.Stat == "Stamina").Value;
        var strength = lines.Single(line => line.Stat == "Strength").Value;

        Assert.Equal(stated, stamina / strength, precision: 3);
    }

    [Fact]
    public void TheGameReadsThatSameRatio()
    {
        const decimal staminaPrinted = 15m;
        const decimal intellectPrinted = 5m;

        var ratio = Line("Stamina") / Line("Intellect");

        Assert.InRange(
            ratio,
            (staminaPrinted - 0.5m) / (intellectPrinted + 0.5m),
            (staminaPrinted + 0.5m) / (intellectPrinted - 0.5m));
    }
}
