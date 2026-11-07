namespace Fellowship.SDK.Models;

/// <summary>
/// An effect applying this one through its <c>CRLinkedEffectComponent</c>. Solar Shield applies Invictus's buffs to the
/// caster and the target while the caster holds the talent's tag.
/// </summary>
/// <param name="Parent">The effect applying this one.</param>
/// <param name="ApplyTo"><c>Source</c> for the parent's caster, <c>Target</c> for the parent's target.</param>
public sealed record EffectLink(EntityReference Parent, string ApplyTo)
{
    /// <summary>The talent or legendary power granting the tag the caster needs. A link with none is unconditional.</summary>
    public EntityReference? Talent { get; init; }

    /// <summary>The tags the caster needs. On a document the talent holding them is <see cref="Talent"/>.</summary>
    public IReadOnlyList<string>? RequiredSourceTags { get; init; }

    /// <summary>The tags the target needs.</summary>
    public IReadOnlyList<string>? RequiredTargetTags { get; init; }

    /// <summary>The tags on the caster that stop the application.</summary>
    public IReadOnlyList<string>? BannedSourceTags { get; init; }
}

/// <summary>A linked effect as the asset states it, before its references resolve.</summary>
public sealed record LinkedEffectSource(IconSource Parent, string ApplyTo)
{
    public IReadOnlyList<string>? RequiredSourceTags { get; init; }

    public IReadOnlyList<string>? RequiredTargetTags { get; init; }

    public IReadOnlyList<string>? BannedSourceTags { get; init; }
}
