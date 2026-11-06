using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class GlobalCooldownTests
{
    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"Ability {id} is not in the export. Either the id moved or the entity is gone.");

    [Theory]
    [InlineData(1229, 1, true)]
    [InlineData(1234, 1.5, true)]
    [InlineData(1100, 1, false)]
    [InlineData(1275, 1, false)]
    public void AnAbilityStatesItsGlobalCooldownAndWhetherHasteShortensIt(int id, double seconds, bool hasted)
    {
        var ability = Ability(id);

        Assert.Equal(seconds, ability["globalCooldown"]?.GetValue<double>());
        Assert.Equal(hasted, ability["globalCooldownScalesWithHaste"]?.GetValue<bool>());
    }

    [Fact]
    public void ACooldownShorterThanTheGlobalCooldownIsNotWritten() =>
        Assert.Null(Ability(1229)["cooldown"]);

    [Theory]
    [InlineData(1409)]
    [InlineData(1541)]
    [InlineData(1000)]
    public void AZeroCooldownIsNotWritten(int id) =>
        Assert.Null(Ability(id)["cooldown"]);

    [Theory]
    [InlineData(171, 15)]
    [InlineData(1410, 12)]
    public void AnAbilitysOwnCooldownDurationOutranksANameMatchedRow(int id, double seconds) =>
        Assert.Equal(seconds, Ability(id)["cooldown"]?.GetValue<double>());

    // These CooldownDuration structs have a RowName with neither a CurveTable nor a RegistryType, so
    // FScalableFloat::IsValid is false and ApplyCooldown (0x1450D9A4B) starts no cooldown.
    [Theory]
    [InlineData(207)]
    [InlineData(1208)]
    [InlineData(1242)]
    public void ARowReachedThroughNeitherATableNorARegistryIsNoCooldown(int id) =>
        Assert.Null(Ability(id)["cooldown"]);

    // CooldownDuration Value 1 is valid, and the ability has no CooldownTags (0x1450D9A89) and neither effect class
    // (0x1450D9BE8).
    [Fact]
    public void ACooldownDurationWithNoCooldownTagsIsNoCooldown() =>
        Assert.Null(Ability(554)["cooldown"]);

    // CooldownDuration names the registry AbilityConstants_Gunde. That registry scans the Gunde plugin's
    // AC_Hero_Gunde.csv (16), and the file of that name under RawDataTables/AbilityConstants has 12.
    [Fact]
    public void ARegistryReadsTheCopyOfARowInTheFilesItScans() =>
        Assert.Equal(16, Ability(1244)["cooldown"]?.GetValue<double>());

    [Fact]
    public void AnAbilityClearingItsParentsGlobalCooldownHasNone()
    {
        var ability = Ability(1541);

        Assert.Null(ability["globalCooldown"]);
        Assert.Null(ability["globalCooldownScalesWithHaste"]);
    }
}
