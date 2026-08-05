namespace Fellowship.SDK.Documents;

/// <summary>A non-player character.</summary>
public sealed class Creature : EntityDocument
{
    public string? Icon { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
