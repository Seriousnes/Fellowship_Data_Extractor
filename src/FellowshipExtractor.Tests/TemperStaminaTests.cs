using Fellowship.Codex.Models;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class TemperStaminaTests
{
    private const int Tempers = 4;

    [Fact]
    public void TheSettingsCarryTheTemperStaminaMultiplierTheConfigStates()
    {
        var items = Export.Loaded.Settings.Items;
        var config = ConfigIni.Read(Export.Provider, ItemSettings.ConfigPath, ItemSettings.SettingsSection);

        Assert.Equal(double.Parse(config.Text("TemperDefaultStaminaMultiplier"), System.Globalization.CultureInfo.InvariantCulture),
            items.TemperDefaultStaminaMultiplier);
        Assert.Equal([new TemperStaminaStep(315, 2)], items.TemperStaminaMultiplierAtItemLevel);
    }

    [Theory]
    [InlineData(314)]
    [InlineData(315)]
    [InlineData(360)]
    public void EachTemperAddsTheStaminaMultiplierAtTheItemLevelStep(int itemLevel)
    {
        var items = Export.Loaded.Settings.Items;
        var multiplier = items.TemperStaminaMultiplierAtItemLevel
            .Where(step => step.ItemLevel <= itemLevel)
            .Select(step => step.TemperValue)
            .DefaultIfEmpty(items.TemperDefaultStaminaMultiplier)
            .Last();

        var untempered = Lines(itemLevel, tempers: 0);
        var tempered = Lines(itemLevel, Tempers);

        Assert.Equal((decimal)(Tempers * multiplier), Line(tempered, "Stamina") - Line(untempered, "Stamina"));
        Assert.Equal(Tempers, Line(tempered, "Strength") - Line(untempered, "Strength"));
    }

    private static IReadOnlyList<StatLine> Lines(int itemLevel, int tempers) =>
        StatMagnitudes.For(
            new ItemDocument
            {
                Id = 1,
                Slot = "Chest",
                Stats =
                [
                    new StatSlot("Stamina", 50) { Kind = SlotKinds.Fixed },
                    new StatSlot("Strength", 50) { Kind = SlotKinds.Primary },
                ],
            },
            Export.Loaded.Settings.StatCalculation,
            Export.Loaded.Settings.Items,
            new HashSet<string>(["Strength"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            itemLevel,
            new ItemScaling { Upgrades = tempers });

    private static decimal Line(IReadOnlyList<StatLine> lines, string stat) =>
        lines.Single(line => string.Equals(line.Stat, stat, StringComparison.OrdinalIgnoreCase)).Value;
}
