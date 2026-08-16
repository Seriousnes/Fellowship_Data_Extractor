namespace Fellowship.SDK.Documents;

public sealed class HeroDocument : EntityDocument
{
    public string? Codename { get; init; }

    public string? Title { get; init; }

    public PrimaryStat? MainStat { get; init; }

    public string? Color { get; init; }

    public string? Biography { get; init; }

    public string? Playstyle { get; init; }

    public string? SpiritAbility { get; init; }
}
