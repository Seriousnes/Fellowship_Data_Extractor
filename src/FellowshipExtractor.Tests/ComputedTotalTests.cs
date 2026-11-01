using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

/// <summary>
/// Totals a description prints and the game computes from one document's own fields: a per-stack value at the
/// effect's maximum stacks, a per-tick value over the effect's ticks, and a set-by-caller magnitude at each count of a
/// spent resource.
/// </summary>
public sealed class ComputedTotalTests
{
    private static readonly Lazy<IReadOnlyList<TooltipNumber>> Audited = new(() => TooltipAudit.Run(Export.Loaded.Documents));

    private static JsonObject Document(EntityType type, int id) =>
        Assert.Single(Export.Loaded.Documents, document => document.Type == type && document.Id == id).Body;

    private static TooltipNumber Number(string type, int id, string number) =>
        Assert.Single(Audited.Value, found => found.Type == type && found.Id == id && found.Number == number);

    [Fact]
    public void MassacreMaximumIsItsSpiritAtOneHundredStacks()
    {
        var effect = Document(EntityType.Effect, 3211);

        Assert.Equal(100, effect["maxStacks"]!.GetValue<int>());
        Assert.Equal(0.0025, effect["modifiers"]![0]!["value"]!.GetValue<double>());
        Assert.Equal(0.25, effect["modifiers"]![0]!["atMaxStacks"]!.GetValue<double>());

        var maximum = Number("talent", 772, "25%");

        Assert.Equal(TooltipNumberStatus.Elsewhere, maximum.Status);
        Assert.Contains("effect 3211: Spirit Additive x100 stacks 0.25", maximum.Evidence);
    }

    [Fact]
    public void EntropicBurstPrintsSixTicksOfItsPerTickCoefficient()
    {
        var effect = Document(EntityType.Effect, 2819);

        Assert.Equal(9, effect["duration"]!.GetValue<double>());
        Assert.Equal(1.5, effect["period"]!.GetValue<double>());
        Assert.Equal(6, effect["ticks"]!.GetValue<int>());

        var total = Number("talent", 592, "246%");

        Assert.Equal(TooltipNumberStatus.Typed, total.Status);
        Assert.Contains("per tick x6 ticks 0.41", total.Evidence);
    }

    [Fact]
    public void HemorrhagingStrikeBleedLastsThreeSecondsMorePerComboPoint()
    {
        var ability = Document(EntityType.Ability, 1208);
        var scaling = Assert.Single(
            ability["setByCallerScaling"]!.AsArray(),
            entry => entry!["magnitude"]!.GetValue<string>() == "SetByCaller.Duration")!;

        Assert.Equal("ResourcesSecond", scaling["count"]!.GetValue<string>());
        Assert.Equal(12, scaling["base"]!.GetValue<double>());
        Assert.Equal(3, scaling["per"]!.GetValue<double>());
        Assert.Equal([15, 18, 21, 24, 27, 30], scaling["values"]!.AsArray().Select(value => value!.GetValue<double>()));

        foreach (var printed in new[] { "12", "21", "24", "27", "30" })
        {
            Assert.All(
                Audited.Value.Where(found => found is { Type: "ability", Id: 1208 } && found.Number == printed),
                found => Assert.NotEqual(TooltipNumberStatus.Missing, found.Status));
        }
    }

    [Fact]
    public void QueensFangDealsTwentyPercentMorePerComboPoint()
    {
        var scaling = Assert.Single(
            Document(EntityType.Ability, 1201)["setByCallerScaling"]!.AsArray(),
            entry => entry!["magnitude"]!.GetValue<string>() == "SetByCaller.DamageScale")!;

        Assert.Equal(1, scaling["base"]!.GetValue<double>());
        Assert.Equal(0.2, scaling["per"]!.GetValue<double>());
        Assert.Equal([1.2, 1.4, 1.6, 1.8, 2, 2.2], scaling["values"]!.AsArray().Select(value => value!.GetValue<double>()));
    }

    [Fact]
    public void ChronoTriggerReducesUnfoldingDoomCooldownToThirtySeconds()
    {
        var change = Assert.Single(
            Document(EntityType.LegendaryPower, 618)["changes"]!.AsArray(),
            entry => entry!["field"]!.GetValue<string>() == "cooldown")!;

        Assert.Equal(30, change["result"]!.GetValue<double>());
        Assert.Equal(TooltipNumberStatus.Typed, Number("legendary-power", 618, "30").Status);
    }
}
