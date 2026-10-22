using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class TemperStaminaTests
{
    [Fact]
    public void TheSettingsCarryTheTemperStaminaMultiplierTheConfigStates()
    {
        var items = Export.Loaded.Settings.Items;
        var config = ConfigIni.Read(Export.Provider, ItemSettings.ConfigPath, ItemSettings.SettingsSection);

        Assert.Equal(double.Parse(config.Text("TemperDefaultStaminaMultiplier"), System.Globalization.CultureInfo.InvariantCulture),
            items.TemperDefaultStaminaMultiplier);
        Assert.Equal([new TemperStaminaStep(315, 2)], items.TemperStaminaMultiplierAtItemLevel);
    }

    [Fact]
    public void NoItemDocumentCarriesOnlyTheSlotItsTagSpells() =>
        Assert.DoesNotContain(
            Export.Loaded.Documents,
            row => row.Type == EntityType.Item &&
                   row.Body.Select(pair => pair.Key).Order().SequenceEqual(["$type", "id", "slot"]));
}
