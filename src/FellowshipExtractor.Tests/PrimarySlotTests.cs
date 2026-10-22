using Fellowship.Codex.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class PrimarySlotTests
{
    [Fact]
    public void APrimarySlotTakesTheShippedPrimaryMultiplier()
    {
        var settings = Export.Loaded.Settings.StatCalculation;
        var block = StatRows.Guaranteed(settings);

        var stated =
            (decimal)StatRows.BlockTerm(block, stamina: true, onPrimaryPool: true, itemLevel: 2000) /
            (decimal)StatRows.BlockTerm(block, stamina: false, onPrimaryPool: true, itemLevel: 2000);

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

        Assert.Equal(stated, stamina / strength, precision: 2);
    }
}
