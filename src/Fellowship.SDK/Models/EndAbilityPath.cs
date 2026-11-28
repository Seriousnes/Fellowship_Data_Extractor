namespace Fellowship.SDK.Models;

/// <summary>
/// One route of an ability's graph from its activation to <c>K2_EndAbility</c>: the ability tasks, latent actions and
/// timers the route waits on, in order. Each task starts in the handler of the task before it, and the first starts at
/// activation. A route with no task ends in the activation itself. Ice Spike has
/// <c>ServerStartCasting.OnFinished</c>, then <c>WaitDelayCR.OnFinish</c> of 1.3 seconds.
/// </summary>
/// <param name="Tasks">The tasks of the route, in order.</param>
public sealed record EndAbilityPath(IReadOnlyList<EndAbilityTask> Tasks)
{
    /// <summary>
    /// The branches between the entry of the route's last handler and <c>K2_EndAbility</c>, each with the side taken.
    /// The last handler is the handler of the last task's <see cref="EndAbilityTask.Delegate"/>, and the activation for a
    /// route with no task. Collapsing Star's route ending in its <c>SpawnActorFromAbility.Success</c> handler has
    /// <c>IsValid(SpawnedActor)</c> false.
    /// </summary>
    public IReadOnlyList<BranchCondition>? EndConditions { get; init; }

    /// <summary>The handlers of the route starting an earlier task of it again.</summary>
    public IReadOnlyList<EndAbilityLoop>? Loops { get; init; }
}

/// <summary>
/// A branch of an ability's graph and the side a route takes: a <c>Branch</c> node's <c>Condition</c>, the validity
/// test of an <c>Is Valid</c> node, the success of a cast node or the comparison of a switch node.
/// </summary>
/// <param name="Condition">
/// The boolean expression in the graph's names, with each local replaced by the expression assigned to it:
/// <c>Greater_IntInt(Array_Length(TargetsLeft), 0)</c>. A function has its name without its library class, a variable
/// of the ability its name, a parameter of the handler's delegate its name, and a gameplay tag its name. A
/// <c>Not_PreBool</c> is left out and reverses <paramref name="Value"/>.
/// </param>
/// <param name="Value">The value of <paramref name="Condition"/> on the route.</param>
public sealed record BranchCondition(string Condition, bool Value);

/// <summary>
/// A return of a route to an earlier task: the handler of the task at <paramref name="From"/> starts the task at
/// <paramref name="To"/> again, so the tasks from <paramref name="To"/> to <paramref name="From"/> repeat. Bloodhunt's
/// handler of its <c>ChargeArriveDelay</c> wait starts <c>MonitorTargetValidityWithFilters</c> again for each target
/// left in <c>TargetsLeft</c>.
/// </summary>
/// <param name="From">The index in <see cref="EndAbilityPath.Tasks"/> of the task whose handler starts the earlier task.</param>
/// <param name="To">The index in <see cref="EndAbilityPath.Tasks"/> of the task started again.</param>
public sealed record EndAbilityLoop(int From, int To)
{
    /// <summary>The branches between the handler's entry and the start of the task, each with the side taken.</summary>
    public IReadOnlyList<BranchCondition>? Conditions { get; init; }
}

/// <summary>A task on a route to <c>K2_EndAbility</c>, and the delegate of it the route continues from.</summary>
/// <param name="Task">
/// The function starting the wait: an ability task's factory such as <c>WaitDelayCR</c> or <c>ServerStartCasting</c>, a
/// latent action such as <c>Delay</c>, or <c>K2_SetTimerDelegate</c>. For a delegate of another object, the ability's
/// variable holding the object, or the class declaring the delegate.
/// </param>
public sealed record EndAbilityTask(string Task)
{
    /// <summary>The task's delegate the route continues from, such as <c>OnFinish</c>. A latent action and a timer have none.</summary>
    public string? Delegate { get; init; }

    /// <summary>
    /// The seconds from the task's start to <see cref="Delegate"/>: the <c>Time</c> of a <c>WaitDelayCR</c> or
    /// <c>WaitDelay</c>, the <c>Time</c> plus <c>InitialStartDelay</c> of a <c>K2_SetTimerDelegate</c> with no
    /// <c>InitialStartDelayVariance</c>, the <c>Duration</c> of a <c>Delay</c>, and for
    /// <c>AILeapToLocation</c> and <c>AILeapToTarget</c> the <c>LeapTime</c> of <c>OnArrived</c> and the <c>Timeout</c>
    /// of <c>OnTimeout</c>. Present where the graph computes the argument from class defaults, constants rows and
    /// literals alone. A wait without it computes its time from a value of the run, such as a distance or the time
    /// since activation. A leap starting within its <c>AcceptanceRadius</c> of the goal has <c>OnArrived</c> at once.
    /// </summary>
    public double? Seconds { get; init; }

    /// <summary>
    /// The ability's variables the wait's time reads, such as <c>AbilityEndDelay</c>, each with its class default. An
    /// <c>FScalableFloat</c> variable has its constants row's value.
    /// </summary>
    public IReadOnlyDictionary<string, double>? Variables { get; init; }

    /// <summary>
    /// <c>WaitDelayCR</c>'s <c>ScaleTimeWithAbilitySpeed</c> argument. True divides <see cref="Seconds"/> by the
    /// ability's time factor when the task starts: the owner's <c>Haste</c> for an ability with
    /// <c>ScaleTimeWithHaste</c>, plus <c>AttackSpeed</c> minus 1 for an ability with <c>ScaleTimeWithAttackSpeed</c>,
    /// and 1 for an ability with neither. False waits <see cref="Seconds"/> itself.
    /// </summary>
    public bool? ScaleTimeWithAbilitySpeed { get; init; }

    /// <summary>
    /// The branches between the entry of the handler the task starts in and the task's start, each with the side
    /// taken. The handler is the one of the task before it on the route, and the activation for the first task. The
    /// validity test of the task itself is left out. Two routes parting in one handler have the same condition with
    /// opposite values on the tasks they part at, or on a task and the other route's
    /// <see cref="EndAbilityPath.EndConditions"/>.
    /// </summary>
    public IReadOnlyList<BranchCondition>? Conditions { get; init; }
}
