namespace Fellowship.SDK.Models;

/// <summary>
/// The flags of an ability's <c>CRChannelingTaskData</c> in the tick count and the times of
/// <c>CRAbilityTask_ServerChanneling</c>. <c>docs/casting-and-channeling.md</c> has the count.
/// </summary>
/// <param name="ContinuousChannel">The channel has no duration and no tick count.</param>
/// <param name="TickImmediately">The first tick is 0.01 seconds after the channel's start, and the count has one more tick.</param>
/// <param name="ScaleDurationWithAbilityTimeRate">The duration is divided by the ability's time rate.</param>
/// <param name="EnablePartialTicks">The channel's end has a tick for the fraction of an interval left over.</param>
/// <param name="AllowSpellPushback">A change of the channeling effect's start time counts the ticks again.</param>
public sealed record ChannelingTaskData(
    bool ContinuousChannel,
    bool TickImmediately,
    bool ScaleDurationWithAbilityTimeRate,
    bool EnablePartialTicks,
    bool AllowSpellPushback);
