namespace Fellowship.SDK.Models;

/// <summary>
/// One route of an ability's graph from its activation to <c>K2_EndAbility</c>: the ability tasks, latent actions and
/// timers the route waits on, in order. Each task starts in the handler of the task before it, and the first starts at
/// activation. A route with no task ends in the activation itself. Ice Spike has
/// <c>ServerStartCasting.OnFinished</c>, then <c>WaitDelayCR.OnFinish</c> of 1.3 seconds.
/// </summary>
/// <param name="Tasks">The tasks of the route, in order.</param>
public sealed record EndAbilityPath(IReadOnlyList<EndAbilityTask> Tasks);

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
    /// The seconds from the task's start to <see cref="Delegate"/>: the <c>Time</c> of a <c>WaitDelayCR</c>,
    /// <c>WaitDelay</c> or <c>K2_SetTimerDelegate</c>, the <c>Duration</c> of a <c>Delay</c>, and for
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
}
