namespace Fellowship.SDK.Models;

/// <summary>
/// One Select node with a gameplay effect for each index value, in an ability's graph or in the graph of an actor the
/// ability spawns. The node's index chooses the effect of one application, so one application has the effect of one
/// case. Shatterstrike's cone applies <c>NormalEffect</c> to a target without the tag
/// <c>Status.FK_BossFreezingWaterLord.WaterSlow</c> and <c>WetEffect</c> to a target with it.
/// </summary>
/// <param name="Cases">The node's index values, each with its effect.</param>
public sealed record EffectSelect(IReadOnlyList<EffectSelectCase> Cases)
{
    /// <summary>
    /// The tag of a node whose index is one <c>HasMatchingGameplayTag</c> call. The case <c>true</c> is for a
    /// <see cref="Character"/> with the tag or a tag under it, and the case <c>false</c> for one without.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// The character <c>HasMatchingGameplayTag</c> is called on: <see cref="Target"/> for the character the effect is
    /// applied to, <see cref="Owner"/> for the ability's owner.
    /// </summary>
    public string? Character { get; init; }

    /// <summary>The character the node's effect is applied to.</summary>
    public const string Target = "Target";

    /// <summary>The character of the ability's actor info, and the instigator of an actor the ability spawns.</summary>
    public const string Owner = "Owner";
}

/// <summary>One index value of a Select node and the effect the node gives for it.</summary>
/// <param name="Value">
/// The index value: <c>true</c> or <c>false</c> for a boolean index, and the member's name for an enum index.
/// </param>
public sealed record EffectSelectCase(string Value)
{
    /// <summary>The effect of the case. A case with an empty effect spec has neither member, and applies nothing.</summary>
    public EntityReference? Effect { get; init; }

    /// <summary>
    /// The Select node the case's spec is made from. The Forest Meadow combat buff has a case for each hero role, and
    /// each is a node on the owner's tag <c>ActorTypeTags.FM.ZoneEventElite</c>.
    /// </summary>
    public EffectSelect? Select { get; init; }
}

/// <summary>A Select node as the graph states it, before its effects resolve.</summary>
public sealed record EffectSelectSource(IReadOnlyList<EffectSelectCaseSource> Cases)
{
    public string? Tag { get; init; }

    public string? Character { get; init; }
}

/// <summary>A case of a Select node as the graph states it.</summary>
public sealed record EffectSelectCaseSource(string Value)
{
    public IconSource? Effect { get; init; }

    public EffectSelectSource? Select { get; init; }
}
