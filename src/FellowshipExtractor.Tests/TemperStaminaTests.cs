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

    /// <summary>
    /// <c>UCRDynamicHeroItem</c>'s item stat body (0x14565A4DB-0x14565A55A, build 26162532) adds one per temper to
    /// every stat, and to Stamina the <c>TemperStaminaMultiplierAtItemLevel</c> entry at or below the item level,
    /// else <c>TemperDefaultStaminaMultiplier</c>.
    /// </summary>
    [Fact]
    public void StaminaGainsTheMultiplierPerTemperFromItemLevel315()
    {
        var items = Export.Loaded.Settings.Items;

        Assert.Equal(2f, Fellowship.Codex.Models.StatRows.PerTemper(items, stamina: true, itemLevel: 315));
        Assert.Equal(2f, Fellowship.Codex.Models.StatRows.PerTemper(items, stamina: true, itemLevel: 360));
        Assert.Equal(1f, Fellowship.Codex.Models.StatRows.PerTemper(items, stamina: true, itemLevel: 314));
        Assert.Equal(1f, Fellowship.Codex.Models.StatRows.PerTemper(items, stamina: false, itemLevel: 315));

        Assert.Equal(
            Fellowship.Codex.Models.StatRows.Magnitude(1f, 1f, 1f, 1f, tempers: 0, perTemper: 2f, guaranteed: true) + 6f,
            Fellowship.Codex.Models.StatRows.Magnitude(1f, 1f, 1f, 1f, tempers: 3, perTemper: 2f, guaranteed: true));
    }
}
