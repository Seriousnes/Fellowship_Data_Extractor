using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// A talent's document has the numbers of the abilities and effects linked to it, and an effect's document
/// has the changes gated on a tag it grants.
/// </summary>
public sealed class TalentReachTests
{
    private const int CoalescingFrost = 36;
    private const int SkylitGrace = 200;
    private const int SkylitGraceBuff = 2316;
    private const int SkystridersGrace = 1304;
    private const int KillEmAll = 3160;

    private const string DamageDivider = "Rime.ChanneledBeamSingleDamage.Talent.DelayedAoe.DamageDividerToInt";

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export.");

    private static JsonObject[] Changes(JsonObject document) =>
        [.. (document["changes"]?.AsArray() ?? []).OfType<JsonObject>()];

    [Fact]
    public void CoalescingFrostHasTheDamageOfTheAbilityItsEffectGrants()
    {
        var constants = Document(EntityType.Talent, CoalescingFrost)["constants"]!.AsObject();

        Assert.Equal(0.56, constants[DamageDivider]!.GetValue<double>());
    }

    [Theory]
    [InlineData("Rime.ChanneledBeamSingleDamage.Talent.DamageBonus.")]
    [InlineData("Rime.ChanneledBeamSingleDamage.BoostByCritProc.")]
    public void CoalescingFrostLeavesTheRowsOfOtherTalents(string prefix)
    {
        var constants = Document(EntityType.Talent, CoalescingFrost)["constants"]!.AsObject();

        Assert.DoesNotContain(constants, entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal));
    }

    [Fact]
    public void SkylitGraceAcceleratesSkystridersGraceWhileItsBuffIsHeld()
    {
        var change = Assert.Single(
            Changes(Document(EntityType.Talent, SkylitGrace)),
            change => change["field"]?.GetValue<string>() == "cooldownAcceleration");

        Assert.Equal(SkystridersGrace, change["target"]!["id"]!.GetValue<int>());
        Assert.Equal(1.2, change["value"]!.GetValue<double>());
        Assert.Equal(SkylitGraceBuff, Assert.Single(change["while"]!.AsArray())!["id"]!.GetValue<int>());
    }

    [Fact]
    public void KillEmAllMultipliesTheDamageOfHeavyStrike()
    {
        var damage = Changes(Document(EntityType.Effect, KillEmAll))
            .Where(change => change["field"]?.GetValue<string>() == "DamageScale")
            .ToArray();

        Assert.NotEmpty(damage);
        Assert.All(damage, change =>
        {
            Assert.Equal("MultiplyCompound", change["op"]!.GetValue<string>());
            Assert.Equal(2.5, change["value"]!.GetValue<double>());
            Assert.Equal("Heavy Strike", change["target"]!["name"]!.GetValue<string>());
        });
    }
}
