namespace Fellowship.SDK.Documents;

/// <summary>A legendary's bonus stat line: Mote of Creation, Blood of the Undying.</summary>
public sealed class LegendaryStat : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
