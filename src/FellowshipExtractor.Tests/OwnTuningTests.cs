using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// An ability's or effect's document has the value of every row its own asset binds, and a talent lengthening a
/// computed duration by a ratio its text prints has that ratio as a change.
/// </summary>
public sealed class OwnTuningTests
{
    private const int ToughnessMonitor = 970;
    private const int ToughnessRankOne = 1247;
    private const int StoneShieldBuff = 1574;
    private const int StoneShieldDamageTaken = 1571;
    private const int RunicRevelations = 71;

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export.");

    private static readonly Lazy<IReadOnlyList<TooltipNumber>> Audited = new(() => TooltipAudit.Run(Export.Loaded.Documents));

    private static TooltipNumber Audit(string type, int id, string number) =>
        Assert.Single(Audited.Value, found => found.Type == type && found.Id == id && found.Number == number);

    [Fact]
    public void ToughnessHasTheDamageReductionOfEachLevel()
    {
        var tuning = Document(EntityType.Ability, ToughnessMonitor)["tuning"]!.AsObject();

        Assert.Equal(0.05, tuning["PhysicalDamageReductionLevel1"]!["value"]!.GetValue<double>());
        Assert.Equal(0.35, tuning["PhysicalDamageReductionLevel4"]!["value"]!.GetValue<double>());
    }

    [Fact]
    public void ToughnessRankOnesFivePercentIsOnTheAbilityApplyingIt()
    {
        var number = Audit("effect", ToughnessRankOne, "5%");

        Assert.Equal(TooltipNumberStatus.Elsewhere, number.Status);
        Assert.Contains($"ability {ToughnessMonitor}: 0.05", number.Evidence);
    }

    /// <summary>
    /// Stone Shield's text prints 25% reduced damage taken. The effect its ability applies multiplies incoming damage
    /// by 0.65, which is 35%, so the text is older than the row.
    /// </summary>
    [Fact]
    public void StoneShieldMultipliesIncomingDamageByItsRow()
    {
        var modifier = Assert.Single(
            Document(EntityType.Effect, StoneShieldDamageTaken)["modifiers"]!.AsArray().OfType<JsonObject>(),
            held => held["attribute"]!.GetValue<string>() == "IncomingDamageMultiplier");

        Assert.Equal("MultiplyCompound", modifier["op"]!.GetValue<string>());
        Assert.Equal(0.65, modifier["value"]!.GetValue<double>());
        Assert.NotEqual(TooltipNumberStatus.Contradicted, Audit("effect", StoneShieldBuff, "25%").Status);
    }

    [Theory]
    [InlineData(1315)]
    [InlineData(1316)]
    [InlineData(1321)]
    public void RunicRevelationsMultipliesEachRuneDurationByItsRatio(int effect)
    {
        var changes = Document(EntityType.Talent, RunicRevelations)["changes"]!.AsArray().OfType<JsonObject>();

        Assert.Contains(changes, change =>
            change["target"]!["id"]!.GetValue<int>() == effect &&
            change["field"]!.GetValue<string>() == "duration" &&
            change["op"]!.GetValue<string>() == "Multiply" &&
            change["value"]!.GetValue<double>() == 1.25);
    }

    [Fact]
    public void RunicRevelationsTwentyFivePercentReadsAsItsChange()
    {
        Assert.Equal(TooltipNumberStatus.Typed, Audit("talent", RunicRevelations, "25%").Status);
    }
}
