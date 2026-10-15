namespace Fellowship.SDK.Documents;

public sealed class CurrencyDocument : EntityDocument
{
    public Rarity? Rarity { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
}
