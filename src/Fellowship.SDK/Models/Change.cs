namespace Fellowship.SDK.Models;

public sealed record Change(EntityReference Target, string Field, string Op, double Value)
{
    public IReadOnlyList<EntityReference>? While { get; init; }

    public IReadOnlyList<EntityReference>? WhileTarget { get; init; }
}
