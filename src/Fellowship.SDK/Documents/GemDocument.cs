namespace Fellowship.SDK.Documents;

public sealed class GemDocument : EntityDocument
{
    public required GemType Type { get; init; }

    public required int Tier { get; init; }

    public required int Power { get; init; }

    public required Rarity Rarity { get; init; }
    public string? Icon { get; init; }
}
