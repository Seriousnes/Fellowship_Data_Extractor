using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>Abilities state the commits that trigger them, effects state what applies them, and rolls state their gates.</summary>
public sealed class CommitTriggerTests
{
    private const int ConvergingTimelinesWatcher = 2311;
    private const int LonesomeSongWatcher = 2018;
    private const int InspiredArms = 173;
    private const int TopazWinds = 171;
    private const int LonesomeSong = 619;
    private const int Oblivion = 1970;
    private const int ConvergingBuff = 3266;
    private const int LonesomeBuff = 2820;
    private const int ArmsBuff = 106;

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents.Single(document => document.Type == type && document.Id == id).Body;

    private static JsonNode?[] Triggers(int ability) =>
        [.. Document(EntityType.Ability, ability)["commitTriggers"]!.AsArray()];

    private static int[] Ids(JsonNode? references) =>
        [.. references!.AsArray().Select(reference => reference!["id"]!.GetValue<int>())];

    private static string[] Strings(JsonNode? list) =>
        [.. list!.AsArray().Select(text => text!.GetValue<string>())];

    [Fact]
    public void ThreeSpendersApplyConvergingTimelines()
    {
        var trigger = Assert.Single(Triggers(ConvergingTimelinesWatcher), trigger => trigger!["committing"]!.AsArray().Count == 3)!;

        Assert.Equal([1964, Oblivion, 1973], Ids(trigger["committing"]));
        Assert.Equal([ConvergingBuff], Ids(trigger["applies"]));
        Assert.Null(trigger["stoppedBy"]);
    }

    [Fact]
    public void LonesomeSongStopsTheSecondWaitOnOblivion()
    {
        var trigger = Assert.Single(Triggers(ConvergingTimelinesWatcher), trigger => trigger!["committing"]!.AsArray().Count == 1)!;

        Assert.Equal([Oblivion], Ids(trigger["committing"]));
        Assert.Equal([ConvergingBuff], Ids(trigger["applies"]));
        Assert.Equal(LonesomeSong, trigger["stoppedBy"]!["id"]!.GetValue<int>());
    }

    [Fact]
    public void OblivionAppliesTheLonesomeSongBuff()
    {
        var trigger = Assert.Single(Triggers(LonesomeSongWatcher))!;

        Assert.Equal([Oblivion], Ids(trigger["committing"]));
        Assert.Equal([LonesomeBuff], Ids(trigger["applies"]));
        Assert.Null(trigger["stoppedBy"]);
    }

    [Fact]
    public void ABroadTagStaysATag()
    {
        var trigger = Assert.Single(Triggers(InspiredArms))!;

        Assert.Equal("AbilityType.Weapon", trigger["committingTag"]!.GetValue<string>());
        Assert.Null(trigger["committing"]);
        Assert.Equal([ArmsBuff], Ids(trigger["applies"]));
    }

    [Fact]
    public void TheEffectStatesEveryWaitApplyingIt()
    {
        var commits = Document(EntityType.Effect, ConvergingBuff)["appliedOnCommitOf"]!.AsArray();

        Assert.Equal(2, commits.Count);
        Assert.All(commits, commit => Assert.Equal(ConvergingTimelinesWatcher, commit!["ability"]!["id"]!.GetValue<int>()));
        Assert.Contains(commits, commit => commit!["stoppedBy"]?["id"]?.GetValue<int>() == LonesomeSong);
        Assert.Contains(commits, commit => commit!["stoppedBy"] is null);
    }

    [Fact]
    public void TopazWindsStatesItsThreeGates()
    {
        var roll = Assert.Single(Document(EntityType.Ability, TopazWinds)["procRolls"]!.AsArray())!;

        Assert.Equal(["CRGameplayAbilityTargeting"], Strings(roll["excludedAbilityClasses"]));
        Assert.Equal(["AbilityType.Skill.Secondary"], Strings(roll["excludedAbilityTags"]));
        Assert.Equal(15, roll["internalCooldown"]!.GetValue<double>());
    }

    [Fact]
    public void TheTraitHasTheRollItsAbilityMakes()
    {
        var roll = Assert.Single(Document(EntityType.ItemTrait, 30)["procRolls"]!.AsArray())!;

        Assert.Equal(15, roll["internalCooldown"]!.GetValue<double>());
    }
}
