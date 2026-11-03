using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class CalculationModifierTests
{
    private const string CreepingDeath = "Mara.HasteToPoisonAndBleedDamageBonus.Scaler";
    private const int SeethingPoison = 1716;
    private const int Hemorrhage = 1724;

    private static JsonObject Effect(int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    private static IEnumerable<JsonObject> Modifiers(JsonObject document) =>
        (document["calculationModifiers"]?.AsArray() ?? []).Select(modifier => modifier!.AsObject());

    [Theory]
    [InlineData(SeethingPoison)]
    [InlineData(Hemorrhage)]
    public void Creeping_Death_multiplies_damage_scale_by_half_of_haste_over_one_plus_one(int effect)
    {
        var modifier = Assert.Single(Modifiers(Effect(effect)), modifier => modifier["row"]?.GetValue<string>() == CreepingDeath);

        Assert.Equal("CRAgilityDamageCalculation", modifier["execution"]!.GetValue<string>());
        Assert.Equal("DamageScale", modifier["attribute"]!.GetValue<string>());
        Assert.Equal("MultiplyCompound", modifier["op"]!.GetValue<string>());
        Assert.Equal("Haste", modifier["backingAttribute"]!.GetValue<string>());
        Assert.Equal("Source", modifier["backingSource"]!.GetValue<string>());
        Assert.Null(modifier["backingSnapshot"]);
        Assert.Equal(0.5, modifier["coefficient"]!.GetValue<double>());
        Assert.Equal(-1, modifier["preMultiplyAdditiveValue"]!.GetValue<double>());
        Assert.Equal(1, modifier["postMultiplyAdditiveValue"]!.GetValue<double>());
    }

    [Fact]
    public void Every_calculation_modifier_names_an_attribute_or_a_transient_aggregator_and_not_both()
    {
        var modifiers = Export.Loaded.Documents.SelectMany(document => Modifiers(document.Body)).ToArray();

        Assert.NotEmpty(modifiers);
        Assert.All(modifiers, modifier => Assert.True((modifier["attribute"] is null) != (modifier["transient"] is null)));
    }
}
