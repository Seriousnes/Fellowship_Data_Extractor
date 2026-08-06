namespace Fellowship.SDK.Documents;

public sealed class GemTierDocument : EntityDocument
{
    public required GemTypeName Gem { get; init; }

    public required int Tier { get; init; }

    public required int Power { get; init; }

    public required Rarity Rarity { get; init; }
    public string? Icon { get; init; }
}
