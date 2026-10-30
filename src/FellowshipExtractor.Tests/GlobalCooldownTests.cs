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
    [InlineData(554, 1)]
    [InlineData(1410, 12)]
    public void AnAbilitysOwnCooldownDurationOutranksANameMatchedRow(int id, double seconds) =>
        Assert.Equal(seconds, Ability(id)["cooldown"]?.GetValue<double>());

    [Fact]
    public void AnAbilityClearingItsParentsGlobalCooldownHasNone()
    {
        var ability = Ability(1541);

        Assert.Null(ability["globalCooldown"]);
        Assert.Null(ability["globalCooldownScalesWithHaste"]);
    }
}
