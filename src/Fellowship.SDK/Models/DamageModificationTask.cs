namespace Fellowship.SDK.Models;

/// <summary>
/// One <c>CRAbilityTask_DamageModificationTask</c> an ability's graph starts. The task's <c>OnDamage</c> handler has
/// the hit's Health damage after the target formula, the absorption passes and <c>DamageMitigationScale</c>, and
/// <c>SetDamageValue</c> replaces it. A reduction is added to the hit's <c>AbsorbedDamage</c>.
/// </summary>
/// <param name="IncomingDamageCharacter">
/// The character whose incoming hits the task has: <see cref="OwningCharacter"/>, or null for none.
/// </param>
/// <param name="OutgoingDamageCharacter">
/// The character whose outgoing hits the task has: <see cref="OwningCharacter"/>, or null for none.
/// </param>
/// <param name="BlockOnTaskOwnerTag">The task skips a hit while the ability's owner has this tag.</param>
/// <param name="Prio">
/// The <c>EDamageExecutionPreApplicationPrioLevel</c> member of the task. The hooks of one hit run in ascending
/// order of it.
/// </param>
public sealed record DamageModificationTask(
    string? IncomingDamageCharacter,
    string? OutgoingDamageCharacter,
    string? BlockOnTaskOwnerTag,
    string Prio)
{
    /// <summary>The character of the ability's actor info, <c>GetOwningCRCharacterFromActorInfo</c>.</summary>
    public const string OwningCharacter = "OwningCharacter";
}
