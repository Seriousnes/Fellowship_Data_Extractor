using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// An effect's duration and modifier value can sit behind a row, an attribute, the applying ability's Blueprint or a
/// custom calculation.
/// </summary>
public sealed class EffectMagnitudeTests
{
    private const int TopazWinds = 102;
    private const int CooldownRecoveryBonus = 106;
    private const int MartialInitiative = 2174;
    private const int InvictusSelfBuff = 2719;

    private static JsonObject Effect(int id) =>
        Export.Loaded.Documents.Single(document => document.Type == EntityType.Effect && document.Id == id).Body;

    private static JsonNode Modifier(int id) => Effect(id)["modifiers"]![0]!;

    private static double[] Numbers(JsonNode? list) => [.. list!.AsArray().Select(number => number!.GetValue<double>())];

    [Fact]
    public void TopazWindsReadsItsDurationFromItsRow() =>
        Assert.Equal(15, Effect(TopazWinds)["duration"]!.GetValue<double>());

    [Fact]
    public void TopazWindsHasTheValueItsAbilityAssignsAtEachRank()
    {
        var modifier = Modifier(TopazWinds);

        Assert.Equal(1.3, modifier["value"]!.GetValue<double>());
        Assert.Equal([1.3, 1.36, 1.42, 1.48], Numbers(modifier["ranks"]));
    }

    [Theory]
    [InlineData(CooldownRecoveryBonus, new[] { 0.1, 0.12, 0.14, 0.16 })]
    [InlineData(MartialInitiative, new[] { 0.1, 0.18, 0.25, 0.32 })]
    public void AnAttributeBasedDurationNamesItsAttributeAndCoefficients(int id, double[] coefficients)
    {
        var scaling = Effect(id)["durationScaling"]!;

        Assert.Equal("WeaponAbilityCooldown", scaling["attribute"]!.GetValue<string>());
        Assert.Equal(coefficients, Numbers(scaling["coefficients"]));
        Assert.Null(Effect(id)["duration"]);
    }

    [Fact]
    public void CooldownRecoveryBonusKeepsItsModifierValue() =>
        Assert.Equal(1.2, Modifier(CooldownRecoveryBonus)["value"]!.GetValue<double>());

    [Fact]
    public void InvictusWritesOutItsCustomCalculation()
    {
        var modifier = Modifier(InvictusSelfBuff);

        Assert.Equal("AddFinal", modifier["op"]!.GetValue<string>());
        Assert.Equal(0.33334, modifier["coefficient"]!.GetValue<double>());
        Assert.Equal(["Haste"], modifier["scalesOff"]!.AsArray().Select(attribute => attribute!.GetValue<string>()));
        Assert.Equal("0.33334 × (Haste - 1) / Stacks", modifier["formula"]!.GetValue<string>());
    }

    [Fact]
    public void AnEffectStatesADurationOrADurationScalingOrNeither() =>
        Assert.DoesNotContain(
            Export.Loaded.Documents.Where(document => document.Type == EntityType.Effect),
            document => document.Body["duration"] is not null && document.Body["durationScaling"] is not null);
}
