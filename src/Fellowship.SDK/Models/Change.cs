namespace Fellowship.SDK.Models;

public sealed record Change(EntityReference Target, string Field, string Op, double Value)
{
    public IReadOnlyList<EntityReference>? While { get; init; }

    public IReadOnlyList<EntityReference>? WhileTarget { get; init; }

    /// <summary>True when the game prints the change's additive value as a percentage.</summary>
    public bool? Percent { get; init; }

    /// <summary>
    /// The target field's value with the change applied: the target's own value of the field multiplied by a factor
    /// or added to by a term.
    /// </summary>
    public double? Result { get; init; }
}
