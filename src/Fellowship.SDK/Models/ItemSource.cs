namespace Fellowship.SDK.Models;

public enum SourceKind
{
    Dungeon,
    Vendor,
    Challenge,
    Character,
}

public sealed record ItemSource(SourceKind Kind, string Name)
{
    public EntityReference? Dungeon { get; init; }

    public string? Category { get; init; }
}
