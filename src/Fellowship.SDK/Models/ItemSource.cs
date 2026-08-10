namespace Fellowship.SDK.Models;

public enum SourceKind
{
    Dungeon,
    Vendor,
    Starmap,
    Character,
}

public sealed record ItemSource(SourceKind Kind, string Name);
