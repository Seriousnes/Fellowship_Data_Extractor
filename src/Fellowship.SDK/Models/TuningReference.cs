namespace Fellowship.SDK.Models;

public sealed record TuningReference(string Name)
{
    public EntityReference? Owner { get; init; }

    public EntityReference? Talent { get; init; }

    /// <summary>The attribute a gameplay effect modifier applies the row's value to.</summary>
    public string? Attribute { get; init; }

    /// <summary>The modifier op applying the row's value to <see cref="Attribute"/>.</summary>
    public string? Op { get; init; }

    public object? Value { get; init; }
}
