using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class CooldownAccelerationTests
{
    private const int KillEmAllTalent = 671;
    private const int KillEmAll = 3160;
    private const int HeavyStrike = 1929;
    private const string Field = "cooldownAcceleration";

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export.");

    private static JsonObject[] Accelerations(JsonObject document) =>
        [
            .. (document["changes"]?.AsArray() ?? [])
                .OfType<JsonObject>()
                .Where(change => change["field"]?.GetValue<string>() == Field),
        ];

    [Theory]
    [InlineData(KillEmAll, HeavyStrike, 1.6)]
    [InlineData(3216, 2295, 2)]
    [InlineData(2315, 1031, 1)]
    [InlineData(2316, 1304, 1.2)]
    public void EffectAcceleratesTheAbilityCoolingDownUnderItsTag(int effect, int ability, double value)
    {
        var change = Assert.Single(Accelerations(Document(EntityType.Effect, effect)));

        Assert.Equal(ability, change["target"]!["id"]!.GetValue<int>());
        Assert.Equal(value, change["value"]!.GetValue<double>());
        Assert.Equal("Additive", change["op"]!.GetValue<string>());
        Assert.True(change["percent"]!.GetValue<bool>());
    }

    [Fact]
    public void TalentAcceleratesWhileItsEffectIsHeld()
    {
        var change = Assert.Single(Accelerations(Document(EntityType.Talent, KillEmAllTalent)));

        Assert.Equal(HeavyStrike, change["target"]!["id"]!.GetValue<int>());
        Assert.Equal(1.6, change["value"]!.GetValue<double>());
        Assert.Equal(KillEmAll, Assert.Single(change["while"]!.AsArray())!["id"]!.GetValue<int>());
    }
}
