namespace Fellowship.SDK.Models;

public sealed record TuningReference(string Name)
{
    public EntityReference? Owner { get; init; }

    public EntityReference? Talent { get; init; }

    public object? Value { get; init; }
}
