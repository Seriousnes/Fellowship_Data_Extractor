namespace Fellowship.SDK.Records;

public sealed record HeroRecord : EntityRecord
{
    public string? Title { get; init; }

    public PrimaryStat? MainStat { get; init; }

    public string? Color { get; init; }

    public string? Biography { get; init; }

    public string? Playstyle { get; init; }

    public string? SpiritAbility { get; init; }

    public string? Portrait { get; init; }
}
