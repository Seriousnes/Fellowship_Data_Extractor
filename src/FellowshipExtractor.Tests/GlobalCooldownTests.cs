using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class GlobalCooldownTests
{
    private const int ColdSnap = 1020;
    private const int IceDash = 1024;
    private const int SkullCrusher = 1229;
    private const int Incinerate = 1270;

    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"Ability {id} is not in the export. Either the id moved or the entity is gone.");

    private static string[] ScalesOff(JsonObject ability) =>
        ability["globalCooldownScalesOff"]?.AsArray().Select(node => node!.GetValue<string>()).ToArray() ?? [];

    [Fact]
    public void ColdSnapStartsTheSharedHastedGlobalCooldown()
    {
        var ability = Ability(ColdSnap);

        Assert.Equal(1.5, ability["globalCooldown"]?.GetValue<double>());
        Assert.Equal(["Haste"], ScalesOff(ability));
    }

    [Fact]
    public void IceDashStartsNoGlobalCooldown() => Assert.Null(Ability(IceDash)["globalCooldown"]);

    [Fact]
    public void SkullCrusherTakesItsCalculationFromTheParentEffect()
    {
        var ability = Ability(SkullCrusher);

        Assert.Equal(1, ability["globalCooldown"]?.GetValue<double>());
        Assert.Equal(["Haste"], ScalesOff(ability));
    }

    [Fact]
    public void IncinerateEvaluatesCooldownReductionFromZero()
    {
        var ability = Ability(Incinerate);

        Assert.Equal(1, ability["globalCooldown"]?.GetValue<double>());
        Assert.Equal(["CooldownReduction", "Haste"], ScalesOff(ability));
    }
}
