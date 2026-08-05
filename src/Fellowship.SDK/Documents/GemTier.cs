namespace Fellowship.SDK.Documents;

/// <summary>
/// One of a gem type's four sizes. All 24 state all four of these, which is why none is optional,
/// and they are the only rows in the corpus stating a rarity.
/// </summary>
public sealed class GemTier : EntityDocument
{
    public required GemTypeName Gem { get; init; }

    /// <summary>Which of the four shipped sizes this is, 1 Small to 4 Flawless.</summary>
    public required int Tier { get; init; }

    /// <summary>The power a gem of this tier contributes to its type's total when socketed.</summary>
    public required int Power { get; init; }

    public required Rarity Rarity { get; init; }
    public string? Icon { get; init; }
}
