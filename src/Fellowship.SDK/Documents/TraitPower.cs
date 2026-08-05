namespace Fellowship.SDK.Documents;

/// <summary>The mechanics behind a trait.</summary>
public sealed class TraitPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
