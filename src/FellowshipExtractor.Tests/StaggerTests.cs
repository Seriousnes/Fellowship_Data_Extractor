using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class StaggerTests
{
    private const int AmendFate = 1964;
    private const int RestoreContinuity = 1973;
    private const int TemporalBarrage = 1926;
    private const int HealToStagger = 1972;

    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents.First(document => document.Type == EntityType.Ability && document.Id == id).Body;

    private static IEnumerable<JsonObject> Effects =>
        Export.Loaded.Documents.Where(document => document.Type == EntityType.Effect).Select(document => document.Body);

    private static JsonObject Delta(int ability) => Assert.Single(Ability(ability)["staggerDeltas"]!.AsArray())!.AsObject();

    [Fact]
    public void TheSettingsStateTheCapAndTheRemovals()
    {
        var stagger = Export.Loaded.Settings.Stagger!;

        Assert.Equal("MaxHealth", stagger.MaxHealthAttribute);
        Assert.Equal("StaggerDamageAdditionFactor", stagger.DamageAdditionFactorAttribute);
        Assert.Equal("StaggerHealReductionFactor", stagger.HealReductionFactorAttribute);
        Assert.Equal("EffectLogic.HealAffectStagger", stagger.HealAffectStaggerTag);
        Assert.Equal(new[] { "EffectType.Environmental", "EffectLogic.DamageIgnoreStagger" }, stagger.DamageIgnoreStaggerTags);
        Assert.Equal("CRModifyStaggerExecution", stagger.ModifyExecution);
        Assert.Equal("SetByCaller.Stagger", stagger.ModifySetByCallerTag);
        Assert.Equal(7, stagger.LoggedResourceType);
        Assert.Contains("min(part, cap − pool)", stagger.CapText);
        Assert.Contains("clamp(heal × healReductionFactorAttribute, 0, pool)", stagger.RemovalText);
        Assert.Contains("RESOURCE_CHANGED", stagger.LoggedChangeText);
    }

    [Fact]
    public void TheNamesOfTheSettingsAreTheNamesTheEffectsHave()
    {
        var stagger = Export.Loaded.Settings.Stagger!;

        Assert.Contains(Effects, effect => Tagged(effect, stagger.HealAffectStaggerTag));
        Assert.Contains(Effects, effect => Tagged(effect, stagger.DamageIgnoreStaggerTags[1]));
        Assert.Contains(Effects, effect => Modifies(effect, stagger.DamageAdditionFactorAttribute));
        Assert.Contains(Effects, effect => Modifies(effect, stagger.HealReductionFactorAttribute));

        var modifying = Effects
            .SelectMany(effect => effect["setByCallerModifiers"]?.AsArray() ?? [])
            .Where(modifier => (string?)modifier!["execution"] == stagger.ModifyExecution)
            .ToArray();

        Assert.NotEmpty(modifying);
        Assert.All(modifying, modifier => Assert.Equal(stagger.ModifySetByCallerTag, (string?)modifier!["dataTag"]));
    }

    [Fact]
    public void AHealRemovesNoStaggerAtTheFactorOfTheAura()
    {
        var stagger = Export.Loaded.Settings.Stagger!;

        var factors = Effects
            .SelectMany(effect => effect["modifiers"]?.AsArray() ?? [])
            .Where(modifier => (string?)modifier!["attribute"] == stagger.HealReductionFactorAttribute)
            .Select(modifier => (double?)modifier!["value"])
            .ToArray();

        Assert.Equal(new double?[] { 0d }, factors);
    }

    [Theory]
    [InlineData(AmendFate, 2826, 7.875)]
    [InlineData(RestoreContinuity, 2827, 5.727)]
    public void ACleanseIsTheLesserOfItsIntellectProductAndTheStaggerOfTheTarget(int ability, int effect, double coefficient)
    {
        var delta = Delta(ability);

        Assert.Equal(effect, (int?)delta["effect"]!["id"]);
        Assert.Equal(Export.Loaded.Settings.Stagger!.ModifySetByCallerTag, (string?)delta["dataTag"]);

        Assert.Equal(
            "min(StaggerRemovalIntCoefficient × Source.Intellect × (Source.Expertise + 1) × " +
            "RandomFloatInRange(StaggerRemovalSpreadMin, StaggerRemovalSpreadMax), Target.GetStagger) × -1",
            (string?)delta["formula"]);

        var product = delta["expression"]!["operands"]![0]!;
        Assert.Equal("Min", (string?)product["op"]);
        Assert.Equal("GetStagger", (string?)product["operands"]![1]!["read"]);
        Assert.Equal("Target", (string?)product["operands"]![1]!["of"]);

        var rows = Rows(delta["expression"]!).ToArray();
        Assert.Contains(rows, row => row.Row.EndsWith(".StaggerRemovalIntCoefficient", StringComparison.Ordinal) && row.Value == coefficient);
        Assert.Contains(rows, row => row.Row.EndsWith(".StaggerRemovalSpreadMin", StringComparison.Ordinal) && row.Value == 0.9);
        Assert.Contains(rows, row => row.Row.EndsWith(".StaggerRemovalSpreadMax", StringComparison.Ordinal) && row.Value == 1.1);
    }

    [Fact]
    public void AnEmpoweredBoltCleansesAShareOfTheMaxHealthOfTheTarget()
    {
        var delta = Delta(TemporalBarrage);

        Assert.Equal("Target.GetMaxHealth × StaggerRemovalInPercentage × -1", (string?)delta["formula"]);
        Assert.Contains(Rows(delta["expression"]!), row => row.Value == 0.03);
    }

    [Fact]
    public void TheCastedHealAddsAShareOfItsHealthChange()
    {
        var delta = Delta(HealToStagger);

        Assert.Equal("HealthChangedEvent.TotalHealthChange × HealToStaggerMultiplier", (string?)delta["formula"]);
        Assert.Contains(Rows(delta["expression"]!), row => row.Value == 0.3);
    }

    [Fact]
    public void EveryDeltaIsOnAnEffectRunningTheExecution()
    {
        var stagger = Export.Loaded.Settings.Stagger!;

        var running = Effects
            .Where(effect => (effect["setByCallerModifiers"]?.AsArray() ?? [])
                .Any(modifier => (string?)modifier!["execution"] == stagger.ModifyExecution))
            .Select(effect => (int?)effect["id"])
            .ToHashSet();

        var deltas = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability)
            .SelectMany(document => document.Body["staggerDeltas"]?.AsArray() ?? [])
            .ToArray();

        Assert.NotEmpty(deltas);
        Assert.All(deltas, delta => Assert.Contains((int?)delta!["effect"]!["id"], running));
    }

    private static bool Tagged(JsonObject effect, string tag) =>
        (effect["effectTags"]?.AsArray() ?? []).Any(held => (string?)held == tag);

    private static bool Modifies(JsonObject effect, string attribute) =>
        (effect["modifiers"]?.AsArray() ?? []).Any(modifier => (string?)modifier!["attribute"] == attribute);

    private static IEnumerable<(string Row, double? Value)> Rows(JsonNode term)
    {
        if ((string?)term["row"] is { } row) yield return (row, (double?)term["value"]);

        foreach (var operand in term["operands"]?.AsArray() ?? [])
        {
            foreach (var nested in Rows(operand!)) yield return nested;
        }
    }
}
