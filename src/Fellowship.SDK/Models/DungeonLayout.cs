namespace Fellowship.SDK.Models;

public sealed record WorldPoint(double X, double Y, double Z);

public sealed record DungeonFloor(string? Name, string? Map, WorldPoint Center, WorldPoint Extent);

public sealed record PlacedCreature(IconSource Creature, WorldPoint Position)
{
    public int? Floor { get; init; }

    public double? MapX { get; init; }

    public double? MapY { get; init; }

    public int? Patrol { get; init; }
}

public sealed record DungeonPlacement(EntityReference Creature, WorldPoint Position)
{
    public int? Floor { get; init; }

    public double? MapX { get; init; }

    public double? MapY { get; init; }

    public int? Patrol { get; init; }
}
