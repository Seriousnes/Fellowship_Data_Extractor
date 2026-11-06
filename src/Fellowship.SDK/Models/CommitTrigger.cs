namespace Fellowship.SDK.Models;

/// <summary>
/// A wait an ability's Blueprint holds on abilities being committed, and what the wait applies. Converging Timelines
/// waits on three spenders and applies its buff, and a second wait on one spender stops at Lonesome Song.
/// </summary>
public sealed record CommitTrigger
{
    /// <summary>The ability holding the wait. An effect's document has it, because the effect names the wait from outside.</summary>
    public EntityReference? Ability { get; init; }

    /// <summary>The abilities holding the wait's tags.</summary>
    public IReadOnlyList<EntityReference>? Committing { get; init; }

    /// <summary>The wait's tags that stand for abilities at large, such as <c>AbilityType.Weapon</c>, or that no ability holds.</summary>
    public string? CommittingTag { get; init; }

    /// <summary>True when a committing ability holds every tag of <see cref="CommittingTag"/>, not any of them.</summary>
    public bool? CommittingHoldsAll { get; init; }

    /// <summary>The tags a committing ability cannot hold.</summary>
    public IReadOnlyList<string>? ExcludedTags { get; init; }

    /// <summary>The effects the wait applies.</summary>
    public IReadOnlyList<EntityReference>? Applies { get; init; }

    /// <summary>The talent or legendary power whose tag stops the application.</summary>
    public EntityReference? StoppedBy { get; init; }

    /// <summary>The talent or legendary power whose tag the application needs.</summary>
    public EntityReference? Talent { get; init; }

    /// <summary>Tags stopping the application that no talent or legendary power holds.</summary>
    public IReadOnlyList<string>? StoppedByTags { get; init; }

    /// <summary>Tags the application needs that no talent or legendary power holds.</summary>
    public IReadOnlyList<string>? RequiredTags { get; init; }
}

/// <summary>A commit wait as the Blueprint states it, before its references resolve.</summary>
/// <param name="Tags">The wait's tags. A committing ability holds any of them, and none means any ability.</param>
/// <param name="AllTags">True when a committing ability holds every tag.</param>
/// <param name="Excluded">The tags a committing ability cannot hold.</param>
/// <param name="Effects">The effects the wait applies.</param>
/// <param name="Stopping">Tags whose test leaves the application.</param>
/// <param name="Required">Tags whose test the application needs.</param>
public sealed record CommitTriggerSource(
    IReadOnlyList<string> Tags,
    bool AllTags,
    IReadOnlyList<string> Excluded,
    IReadOnlyList<IconSource> Effects,
    IReadOnlyList<string> Stopping,
    IReadOnlyList<string> Required);
