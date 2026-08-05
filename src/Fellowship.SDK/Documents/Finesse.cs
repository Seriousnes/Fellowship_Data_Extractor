namespace Fellowship.SDK.Documents;

/// <summary>A finesse: The Herald, The Trickster, The Sinister. All 14 carry a name.</summary>
public sealed class Finesse : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
