namespace Fellowship.SDK.Records;

public sealed record CurrencyRecord : EntityRecord
{
    public string? Rarity { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
}
