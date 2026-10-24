using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class EffectTriggerTests
{
    private const int Kindling = 172;
    private const int KindlingBurn = 104;

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    private static string[] Strings(JsonNode? node) =>
        node?.AsArray().Select(element => element!.GetValue<string>()).ToArray() ?? [];

    private static JsonObject[] Triggers(string triggerEvent) =>
        Document(EntityType.Ability, Kindling)["effectTriggers"]!.AsArray()
            .Select(node => node!.AsObject())
            .Where(trigger => trigger["event"]?.GetValue<string>() == triggerEvent)
            .ToArray();

    [Fact]
    public void KindlingListensForHarmfulEffectsAppliedWithoutTheirPeriodicTicks()
    {
        var harmful = Triggers("EffectApplied")
            .Single(trigger => Strings(trigger["assetTagsRequired"]).SequenceEqual(["EffectType.Harmful"]));

        Assert.False(harmful["periodicExecutions"]?.GetValue<bool>());
        Assert.Equal(
            ["EffectType.Healing", "CustomAssetTags.Traits.ExtraDotHotOnEffectApplicationProc", "EffectType.Environmental"],
            Strings(harmful["assetTagsIgnored"]));
        Assert.Equal(["AbilityType.Skill.Secondary"], Strings(harmful["sourceTagsIgnored"]));
    }

    [Fact]
    public void KindlingListensForHarmfulDurationRefreshes() =>
        Assert.Single(Triggers("EffectDurationRefreshed"),
            trigger => Strings(trigger["assetTagsRequired"]).SequenceEqual(["EffectType.Harmful"]));

    [Fact]
    public void KindlingRollsTwoPointOneProcsPerMinuteScaledByHaste()
    {
        var roll = Document(EntityType.Ability, Kindling)["procRolls"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(roll => roll["stream"]?.GetValue<string>() == "RandomStream.Traits.ExtraDotHotOnEffectApplicationProc.Dot");

        Assert.Equal(2.1, roll["procsPerMinute"]?.GetValue<double>());
        Assert.True(roll["scalesWithHaste"]?.GetValue<bool>());
    }

    [Fact]
    public void KindlingBurnRestartsItsDurationAndKeepsItsTickTimer()
    {
        var burn = Document(EntityType.Effect, KindlingBurn);
        var stacking = burn["stacking"]!.AsObject();

        Assert.Equal("AggregateBySource", stacking["type"]?.GetValue<string>());
        Assert.Equal("RefreshOnSuccessfulApplication", stacking["durationRefresh"]?.GetValue<string>());
        Assert.Equal("NeverReset", stacking["periodReset"]?.GetValue<string>());
        Assert.Equal(9, burn["duration"]?.GetValue<double>());
        Assert.Equal(1.5, burn["period"]?.GetValue<double>());
    }
}
