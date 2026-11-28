using System.Text.Json;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

/// <summary>
/// A creature ability's routes to <c>K2_EndAbility</c>, its tags, the tags it blocks, its time scaling flags and its
/// interrupt cooldown.
/// </summary>
public sealed class EndAbilityPathTests
{
    private const int IceSpike = 720;
    private const int ColdheartPoison = 66;
    private const int RazorTrap = 748;
    private const int IceBreaker = 738;
    private const int RabidLeap = 2197;
    private const int CrushArmor = 609;
    private const int Maelstrom = 759;
    private const int DuelistAttack = 740;
    private const int CrusaderAttack = 389;
    private const int CultistDot = 833;

    private static AbilityDocument Ability(int id) =>
        Export.Loaded.Documents.Single(row => row.Type == EntityType.Ability && row.Id == id).Body.Deserialize<AbilityDocument>(EntityDocuments.Options)!;

    private static string[] Named(EndAbilityPath route) => [.. route.Tasks.Select(task => $"{task.Task}.{task.Delegate}")];

    private static IReadOnlyList<EndAbilityTask> Route(int ability, params string[] tasks) =>
        Assert.Single(Ability(ability).EndAbilityPaths!, route => Named(route).SequenceEqual(tasks)).Tasks;

    [Fact]
    public void IceSpikeEndsItsAbilityEndDelayAfterItsCast()
    {
        var ability = Ability(IceSpike);
        var route = Assert.Single(ability.EndAbilityPaths!);

        Assert.Equal(["ServerStartCasting.OnFinished", "WaitDelayCR.OnFinish"], Named(route));
        Assert.Null(route.Tasks[0].Seconds);
        Assert.Equal(1.3, route.Tasks[1].Seconds);
        Assert.Equal(["AbilityEndDelay"], route.Tasks[1].Variables!.Keys);
        Assert.True(route.Tasks[1].ScaleTimeWithAbilitySpeed);

        Assert.True(ability.CastTimeScalesWithHaste);
        Assert.Null(ability.ScaleTimeWithAttackSpeed);
        Assert.Equal(["AbilityType.AutoAttack"], ability.BlockAbilitiesWithTag);
        Assert.Contains("AbilityType.Skill", ability.AbilityTags!);
    }

    [Fact]
    public void ColdheartPoisonWaitsForItsProjectileAndThenInTheAbility()
    {
        var route = Route(ColdheartPoison, "ServerTargetedNPCCast.OnFinished", "WaitDelayCR.OnFinish", "SpawnActorFromAbility.Success", "WaitDelayCR.OnFinish");

        Assert.Equal(0.17, route[1].Seconds);
        Assert.Equal(["ProjectiletDelay"], route[1].Variables!.Keys);
        Assert.Equal(0.7, route[3].Seconds);
        Assert.Equal(["WaitInAbilityDelay"], route[3].Variables!.Keys);
    }

    [Fact]
    public void RazorTrapsSpawnWaitIsItsSpawnDelayLessALiteral()
    {
        var route = Route(RazorTrap, "WaitDelayCR.OnFinish", "SpawnActorFromAbility.Success", "WaitDelayCR.OnFinish");

        Assert.Equal(0.35, route[0].Seconds);
        Assert.Equal(0.4, route[0].Variables!["Spawn Delay"], 6);
        Assert.Equal(0.95, route[2].Seconds);
    }

    [Fact]
    public void IceBreakersLeapArrivesAfterItsLeapTime()
    {
        var route = Route(IceBreaker, "ServerTargetedNPCCast.OnFinished", "AILeapToLocation.OnArrived", "WaitDelayCR.OnFinish");

        Assert.Equal(0.25, route[1].Seconds);
        Assert.Equal(["Leap Time"], route[1].Variables!.Keys);
        Assert.Null(route[1].ScaleTimeWithAbilitySpeed);
        Assert.Equal(1, route[2].Seconds);

        var timeout = Route(IceBreaker, "ServerTargetedNPCCast.OnFinished", "AILeapToLocation.OnTimeout");

        Assert.Equal(60, timeout[1].Seconds);
    }

    [Fact]
    public void RabidLeapStaysInTheAbilityThroughADelayNode()
    {
        var route = Route(RabidLeap, "MoveCharacterAttached.OnArrived", "DelayUntilNextTick.", "Delay.");

        Assert.Equal(1, route[2].Seconds);
        Assert.Equal(["StayInAbilityDelay"], route[2].Variables!.Keys);
        Assert.Null(route[2].ScaleTimeWithAbilitySpeed);
    }

    [Fact]
    public void CrushArmorWaitsInTheAbilityAfterItsCast()
    {
        var wait = Assert.Single(
            Ability(CrushArmor).EndAbilityPaths!.SelectMany(route => route.Tasks),
            task => task.Variables?.ContainsKey("WaitInAbilityDelay") is true);

        Assert.Equal("WaitDelayCR", wait.Task);
        Assert.Equal(0.8, wait.Seconds);
        Assert.False(wait.ScaleTimeWithAbilitySpeed);
    }

    [Fact]
    public void MaelstromsLastWaitReadsItsAnimationLengthAndTheTimeSinceActivation()
    {
        var route = Route(Maelstrom, "WaitDelayCR.OnFinish", "BP_FK_BossElfDragon_CharacterSucker.DestinationReached", "WaitDelayCR.OnFinish");

        Assert.Equal(1, route[0].Seconds);
        Assert.Null(route[2].Seconds);
        Assert.Equal(2.3, route[2].Variables!["SuckAnimationLength"], 6);
    }

    [Fact]
    public void AnAutoAttackHasBothTimeScalingFlags()
    {
        var duelist = Ability(DuelistAttack);

        Assert.True(duelist.CastTimeScalesWithHaste);
        Assert.True(duelist.ScaleTimeWithAttackSpeed);
        Assert.Equal(["AbilityType.Skill"], duelist.BlockAbilitiesWithTag);
        Assert.Equal(1, Assert.Single(Assert.Single(duelist.EndAbilityPaths!).Tasks).Seconds);
    }

    [Fact]
    public void ADeclaredVariableWithNoClassDefaultIsZeroSeconds()
    {
        var wait = Assert.Single(Assert.Single(Ability(CrusaderAttack).EndAbilityPaths!).Tasks);

        Assert.Equal("WaitDelayCR", wait.Task);
        Assert.Equal(0, wait.Seconds);
        Assert.Equal(0, wait.Variables!["StayInAbilityDelay"]);
    }

    [Fact]
    public void AnAbilityHasItsOwnInterruptCooldownDuration()
    {
        var ability = Ability(CultistDot);

        Assert.Equal(15, ability.InterruptCooldownDuration);
        Assert.Equal(
            ["DR_CultistCaster.CastedRangedProjectileDOT.InterruptCooldown"],
            ability.FieldSources!["interruptCooldownDuration"].Rows);
    }

    [Fact]
    public void EveryRouteTaskHasItsFactoryVariableOrClassName()
    {
        var tasks = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Ability && row.Body["endAbilityPaths"] is not null)
            .Select(row => row.Body.Deserialize<AbilityDocument>(EntityDocuments.Options)!)
            .SelectMany(ability => ability.EndAbilityPaths!)
            .SelectMany(route => route.Tasks)
            .ToArray();

        Assert.NotEmpty(tasks);
        Assert.DoesNotContain(tasks, task => task.Task.StartsWith("K2Node_", StringComparison.Ordinal));
        Assert.DoesNotContain(tasks, task => task.Task.StartsWith("CallFunc_", StringComparison.Ordinal));
        Assert.DoesNotContain(tasks, task => task.Task.StartsWith("Temp_", StringComparison.Ordinal));
    }
}
