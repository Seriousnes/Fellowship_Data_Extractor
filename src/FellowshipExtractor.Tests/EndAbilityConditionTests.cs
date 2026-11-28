using System.Text.Json;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

/// <summary>The branch conditions and the loops of a creature ability's routes to <c>K2_EndAbility</c>.</summary>
public sealed class EndAbilityConditionTests
{
    private const int ReturnHome = 16;
    private const int Maelstrom = 759;
    private const int Bloodhunt = 761;
    private const int CollapsingStar = 772;
    private const int Charge = 780;
    private const int HuntPrey = 2088;

    private const string TargetsLeft = "Greater_IntInt(Array_Length(TargetsLeft), 0)";
    private const string TargetRemoved = "Array_RemoveItem(TargetsLeft, CurrentDashTarget)";

    private static AbilityDocument Ability(int id) =>
        Export.Loaded.Documents.Single(row => row.Type == EntityType.Ability && row.Id == id).Body.Deserialize<AbilityDocument>(EntityDocuments.Options)!;

    private static EndAbilityPath Route(int ability, params string[] tasks) =>
        Assert.Single(
            Ability(ability).EndAbilityPaths!,
            route => route.Tasks.Select(task => $"{task.Task}.{task.Delegate}").SequenceEqual(tasks));

    private static string[] Written(IEnumerable<BranchCondition>? conditions) =>
        [.. (conditions ?? []).Select(condition => $"{condition.Condition} {condition.Value}")];

    [Fact]
    public void CollapsingStarsRoutesPartAtTheValidityOfTheSpawnedActor()
    {
        var ended = Route(CollapsingStar, "ServerTargetedNPCCast.OnFinished", "SpawnActorFromAbility.Success");
        var waited = Route(CollapsingStar, "ServerTargetedNPCCast.OnFinished", "SpawnActorFromAbility.Success", "WaitDelayCR.OnFinish");

        Assert.Equal(["IsValid(SpawnedActor) False"], Written(ended.EndConditions));
        Assert.Equal(["IsValid(SpawnedActor) True"], Written(waited.Tasks[2].Conditions));
        Assert.Null(waited.EndConditions);
        Assert.Null(waited.Tasks[0].Conditions);
    }

    [Fact]
    public void BloodhuntEndsInItsActivationWithNoMarkedTarget()
    {
        Assert.Equal([$"{TargetsLeft} False"], Written(Route(Bloodhunt).EndConditions));
    }

    [Fact]
    public void BloodhuntRepeatsItsDashForEachTargetLeft()
    {
        var route = Route(
            Bloodhunt,
            "WaitDelayCR.OnFinish",
            "MonitorTargetValidityWithFilters.OnActivate",
            "MoveToActorUsingPathFixedSpeed.OnTargetLocationReached",
            "WaitDelayCR.OnFinish",
            "WaitDelayCR.OnFinish");

        Assert.Equal([1.5, 0.5, 0.7], route.Tasks.Where(task => task.Task == "WaitDelayCR").Select(task => task.Seconds!.Value));
        Assert.All(route.Tasks.Where(task => task.Task == "WaitDelayCR"), task => Assert.True(task.ScaleTimeWithAbilitySpeed));

        Assert.Equal([$"{TargetsLeft} True"], Written(route.Tasks[0].Conditions));
        Assert.Equal([$"{TargetsLeft} True"], Written(route.Tasks[1].Conditions));
        Assert.Equal([$"{TargetRemoved} True", $"{TargetsLeft} False"], Written(route.Tasks[4].Conditions));

        var loop = Assert.Single(route.Loops!);

        Assert.Equal(3, loop.From);
        Assert.Equal(1, loop.To);
        Assert.Equal([$"{TargetRemoved} True", $"{TargetsLeft} True"], Written(loop.Conditions));
    }

    [Fact]
    public void ReturnHomeTakesThePatrolPathWhereTheOwnerHasItsComponent()
    {
        const string cast = "DynamicCast<CRAICharacter>(GetOwningCRCharacterFromActorInfo())";
        const string follower = "IsValid(GetOwningCRCharacterFromActorInfo().GetComponentByClass(CRPatrolPathFollowerComponent))";

        Assert.Equal(
            [$"{cast} True", $"{follower} True"],
            Written(Route(ReturnHome, "ReturnToPatrolPath.Done").Tasks[0].Conditions));

        Assert.Equal(
            [$"{cast} True", $"{follower} False", "OwnerHasMoved() True"],
            Written(Route(ReturnHome, "FellowshipMoveToLocation.OnArrived").Tasks[0].Conditions));
    }

    [Fact]
    public void MaelstromsLastWaitStartsWithAnimationTimeLeft()
    {
        const string left =
            "Greater_DoubleDouble(Subtract_DoubleDouble(SuckAnimationLength, Subtract_DoubleDouble(GetServerWorldTime(self), SuckAnimationStartTime)), 0)";

        var ended = Route(Maelstrom, "WaitDelayCR.OnFinish", "BP_FK_BossElfDragon_CharacterSucker.DestinationReached");
        var waited = Route(Maelstrom, "WaitDelayCR.OnFinish", "BP_FK_BossElfDragon_CharacterSucker.DestinationReached", "WaitDelayCR.OnFinish");

        Assert.Contains($"{left} False", Written(ended.EndConditions));
        Assert.Contains($"{left} True", Written(waited.Tasks[2].Conditions));
        Assert.Contains("LessEqual_IntInt(Array_Length(CurrentSuckTargets), 0) True", Written(waited.Tasks[2].Conditions));
    }

    [Fact]
    public void ChargeCastsWithATargetAndGroundUnderItsOwner()
    {
        var route = Route(Charge, "ServerStartCasting.OnFinished", "MoveToLocationUsingPathFixedSpeed.OnTargetLocationReached", "WaitDelayCR.OnFinish");

        Assert.Contains(Written(route.Tasks[0].Conditions), written => written.StartsWith("CalculateChargeTargetInfo(", StringComparison.Ordinal) && written.EndsWith(".Success True", StringComparison.Ordinal));
        Assert.Contains(Written(route.Tasks[0].Conditions), written => written.StartsWith("CrFindGroundLocation(", StringComparison.Ordinal) && written.EndsWith(".Success True", StringComparison.Ordinal));
    }

    [Fact]
    public void HuntPreyAttacksAfterATimeoutWhereItsTeleportSucceeds()
    {
        var route = Route(
            HuntPrey,
            "ServerTargetedNPCCast.OnFinished",
            "ServerStartChanneling.OnActivate",
            "FellowshipMoveTo.OnTimeout",
            "ServerStartChanneling.OnFinished");

        var conditions = Written(route.Tasks[3].Conditions);

        Assert.Equal(2, conditions.Length);
        Assert.StartsWith("Less_DoubleDouble(Subtract_DoubleDouble(Vector_Distance(", conditions[0]);
        Assert.StartsWith("AuthTeleportCharacterToTarget(", conditions[1]);
        Assert.All(conditions, written => Assert.EndsWith(" True", written));
    }

    [Fact]
    public void EveryConditionIsWrittenWithoutACompilerLocal()
    {
        var conditions = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Ability && row.Body["endAbilityPaths"] is not null)
            .Select(row => row.Body.Deserialize<AbilityDocument>(EntityDocuments.Options)!)
            .SelectMany(ability => ability.EndAbilityPaths!)
            .SelectMany(route => route.Tasks.SelectMany(task => task.Conditions ?? [])
                .Concat(route.EndConditions ?? [])
                .Concat((route.Loops ?? []).SelectMany(loop => loop.Conditions ?? [])))
            .Select(condition => condition.Condition)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(conditions);
        Assert.DoesNotContain(conditions, written => written.Contains("CallFunc_", StringComparison.Ordinal));
        Assert.DoesNotContain(conditions, written => written.Contains("K2Node_", StringComparison.Ordinal));
        Assert.DoesNotContain(conditions, written => written.Contains("<unhandled", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryLoopReturnsToAnEarlierTaskOfItsRoute()
    {
        var routes = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Ability && row.Body["endAbilityPaths"] is not null)
            .Select(row => row.Body.Deserialize<AbilityDocument>(EntityDocuments.Options)!)
            .SelectMany(ability => ability.EndAbilityPaths!)
            .Where(route => route.Loops is not null)
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.All(routes, route => Assert.All(route.Loops!, loop =>
        {
            Assert.InRange(loop.To, 0, loop.From);
            Assert.InRange(loop.From, 0, route.Tasks.Count - 1);
        }));
    }
}
