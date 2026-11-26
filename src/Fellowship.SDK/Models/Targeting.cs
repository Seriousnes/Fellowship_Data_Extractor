namespace Fellowship.SDK.Models;

/// <summary>
/// An ability's <c>TargetSelectionPolicy</c>: its class without the <c>CRAbilityTargetSelectionPolicy_</c> prefix, and the
/// parameters that class declares.
/// </summary>
/// <param name="Type">
/// <c>Threat</c>, <c>RandomActorSet</c>, <c>FurthestActorSet</c>, <c>ClosestActorSet</c>, <c>ActorSet</c>,
/// <c>RemainingHealthActorSet</c>, <c>RandomPreferRoleActorSet</c>, <c>RandomPreferTagActorSet</c>, <c>Blueprint</c> and
/// the other members of the class family.
/// </param>
public sealed record TargetSelection(string Type)
{
    /// <summary>A <c>Threat</c> policy's <c>SelectionType</c>, a member of <c>ECRAbilityTargetSelectionPolicy_ThreatSelectionType</c>.</summary>
    public string? ThreatMode { get; init; }

    /// <summary>A <c>RemainingHealthActorSet</c> policy's <c>SelectionType</c>: <c>LeastHealthLeft</c> or <c>MostHealthLeft</c>.</summary>
    public string? HealthOrder { get; init; }

    /// <summary>A <c>RandomPreferRoleActorSet</c> policy's <c>PreferredRole</c>, a member of <c>EPlayerRole</c>.</summary>
    public string? PreferredRole { get; init; }

    /// <summary>A <c>RandomPreferTagActorSet</c> policy's <c>ShouldHaveTags</c>.</summary>
    public IReadOnlyList<string>? PreferredTags { get; init; }

    /// <summary>A <c>RandomPreferTagActorSet</c> policy's <c>ShouldNotHaveTags</c>.</summary>
    public IReadOnlyList<string>? AvoidedTags { get; init; }

    /// <summary>An actor set policy's <c>MinNumberOfTargets</c>.</summary>
    public double? MinNumberOfTargets { get; init; }

    /// <summary>An actor set policy's <c>MaxNumberOfTargets</c>.</summary>
    public double? MaxNumberOfTargets { get; init; }

    /// <summary>A <c>Blueprint</c> policy's Blueprint class, without its <c>_C</c>.</summary>
    public string? Blueprint { get; init; }
}
