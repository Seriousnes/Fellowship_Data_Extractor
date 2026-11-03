using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class ProcRollTests
{
    private const int DragonboneDice = 778;
    private const string PowerBonusOnCritRate = "ArmorSetBonus.PowerBonusOnCrit.PPM";

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    private static IEnumerable<JsonObject> Rolls(JsonObject document) =>
        (document["procRolls"]?.AsArray() ?? []).Select(roll => roll!.AsObject());

    [Fact]
    public void DragonboneDiceRollsItsRateTimesOnePlusCritChanceWithHasteOff()
    {
        var roll = Assert.Single(Rolls(Document(EntityType.RelicPassive, DragonboneDice)));

        Assert.Equal(PowerBonusOnCritRate, roll["row"]!.GetValue<string>());
        Assert.Equal(1, roll["procsPerMinute"]!.GetValue<double>());
        Assert.Equal("PPM × (1 + CritChance)", roll["formula"]!.GetValue<string>());
        Assert.Equal(["CritChance"], roll["scalesOff"]!.AsArray().Select(attribute => attribute!.GetValue<string>()));
        Assert.False(roll["scalesWithHaste"]!.GetValue<bool>());
    }

    [Fact]
    public void AHastedRollEndsItsFormulaInOnePlusHaste()
    {
        var hasted = Export.Loaded.Documents
            .SelectMany(document => Rolls(document.Body))
            .Where(roll => roll["scalesWithHaste"]!.GetValue<bool>())
            .ToArray();

        Assert.NotEmpty(hasted);
        Assert.All(hasted, roll => Assert.EndsWith(" × (1 + Haste)", roll["formula"]!.GetValue<string>()));
        Assert.All(hasted, roll => Assert.Equal("Haste", roll["scalesOff"]!.AsArray().Last()!.GetValue<string>()));
    }

    [Fact]
    public void EveryRollNamesTheRateAndOnlyItsOwnAttributes()
    {
        var rolls = Export.Loaded.Documents.SelectMany(document => Rolls(document.Body)).ToArray();

        Assert.NotEmpty(rolls);

        Assert.All(rolls, roll =>
        {
            var formula = roll["formula"]!.GetValue<string>();
            Assert.Contains("PPM", formula);

            foreach (var attribute in roll["scalesOff"]!.AsArray().Select(attribute => attribute!.GetValue<string>()))
            {
                Assert.Contains(attribute, formula);
            }
        });
    }

    [Fact]
    public void TheSettingsStateTheChanceOneRollGives()
    {
        var roll = Export.Loaded.Settings.ProcPerMinuteRoll;

        Assert.Equal("Haste", roll.HasteAttribute);
        Assert.Equal(8, roll.FirstProcChanceFloorDivisor);
        Assert.Equal(1000, roll.LongestDrought);
        Assert.Contains("R × t ÷ 60", roll.DisplayText);
    }
}
