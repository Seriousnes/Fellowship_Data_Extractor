namespace Fellowship.SDK.Models;

public sealed record LegendaryStatText(int Position, string Name)
{
    public string? Description { get; init; }
}
