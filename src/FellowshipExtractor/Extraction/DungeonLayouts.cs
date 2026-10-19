using Fellowship.SDK.Models;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed record DungeonFloorLayout(string? Name, string? MapPath, WorldPoint Center, WorldPoint Extent);

public sealed record CreaturePlacement(string Creature, WorldPoint Position)
{
    public int? Floor { get; init; }

    public double? MapX { get; init; }

    public double? MapY { get; init; }

    public int? Patrol { get; init; }
}

public sealed record DungeonLayout(
    IReadOnlyList<DungeonFloorLayout> Floors,
    IReadOnlyList<CreaturePlacement> Placements,
    IReadOnlyList<IReadOnlyList<WorldPoint>> Patrols);

public sealed class DungeonLayouts
{
    private const string MapExtension = ".umap";
    private const string BlueprintSuffix = "_C";

    private const string AreaBoxClass = "BP_DungeonMapAreaBox_C";
    private const string AreaTextureField = "DungeonAreaTexture";
    private const string AreaNameField = "DungeonAreaName";
    private const string BoundaryField = "MapBoundaryBox";
    private const string ExtentField = "BoxExtent";

    private const string AlwaysLoadedClass = "LevelStreamingAlwaysLoaded";
    private const string WorldAssetField = "WorldAsset";

    private const string PatrolClass = "BP_PatrolPath_C";
    private const string ColumnsField = "Columns";
    private const string CharactersField = "Characters";
    private const string PathsField = "PatrolPaths";
    private const string PointsField = "PathPoints";

    private const string RootField = "RootComponent";
    private const string LocationField = "RelativeLocation";
    private const string ScaleField = "RelativeScale3D";

    private readonly Dictionary<string, DungeonLayout> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public int Placed => _byTag.Values.Sum(layout => layout.Placements.Count);

    public int Floors => _byTag.Values.Sum(layout => layout.Floors.Count);

    public int Patrols => _byTag.Values.Sum(layout => layout.Patrols.Count);

    public int Unfloored => _byTag.Values.Sum(layout => layout.Placements.Count(placed => placed.Floor is null));

    public DungeonLayouts(AbstractFileProvider provider, DungeonNames dungeons, IReadOnlyCollection<string> creatures)
    {
        var wanted = new HashSet<string>(creatures, StringComparer.OrdinalIgnoreCase);

        var maps = provider.Files.Values
            .Where(file => file.Path.EndsWith(MapExtension, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var dungeon in dungeons.All)
        {
            if (dungeon.LevelName is not { Length: > 0 } levelName) continue;

            var persistent = Array.Find(maps, file =>
                Path.GetFileNameWithoutExtension(file.Path).Equals(levelName, StringComparison.OrdinalIgnoreCase));

            if (persistent is null || !provider.TryLoadPackage(persistent, out var level)) continue;

            var exports = level.GetExports().ToArray();
            var floors = FloorsOf(exports);

            var placements = new List<CreaturePlacement>();
            var patrols = new List<IReadOnlyList<WorldPoint>>();

            foreach (var loaded in (IPackage[])[level, .. AlwaysLoaded(provider, exports)])
            {
                if (!Holds(loaded, wanted)) continue;

                Read(loaded, wanted, floors, placements, patrols);
            }

            if (floors.Count == 0 && placements.Count == 0) continue;

            _byTag[dungeon.Tag] = new DungeonLayout(floors, placements, patrols);
        }
    }

    public DungeonLayout? For(string dungeonTag) => _byTag.GetValueOrDefault(dungeonTag);

    public IReadOnlyList<string> CreaturesIn(string dungeonTag) =>
        _byTag.TryGetValue(dungeonTag, out var layout)
            ? [.. layout.Placements.Select(placed => placed.Creature).Distinct(StringComparer.OrdinalIgnoreCase)]
            : [];

    public static IEnumerable<string> Classes(IPackage package) => package switch
    {
        IoPackage io => io.ExportMap.Select(export => io.ResolveObjectIndex(export.ClassIndex)?.Name.Text)
            .OfType<string>(),
        Package legacy => legacy.ExportMap.Select(export => export.ClassName),
        _ => [],
    };

    public static string? CreatureOf(string className) =>
        className.EndsWith(BlueprintSuffix, StringComparison.Ordinal) ? className[..^BlueprintSuffix.Length] : null;

    private static bool Holds(IPackage package, IReadOnlySet<string> wanted) =>
        Classes(package).Any(type => CreatureOf(type) is { } creature && wanted.Contains(creature));

    private static IEnumerable<IPackage> AlwaysLoaded(AbstractFileProvider provider, IEnumerable<UObject> exports)
    {
        foreach (var streaming in exports)
        {
            if (streaming.ExportType != AlwaysLoadedClass) continue;
            if (PropertyWalk.GetString(streaming, WorldAssetField) is not { } world) continue;

            if (provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(world), out var sublevel))
            {
                yield return sublevel;
            }
        }
    }

    private static IReadOnlyList<DungeonFloorLayout> FloorsOf(IEnumerable<UObject> exports)
    {
        var floors = new List<DungeonFloorLayout>();

        foreach (var area in exports)
        {
            if (area.ExportType != AreaBoxClass) continue;
            if (Component(area, BoundaryField) is not { } box) continue;
            if (Vector(box, LocationField) is not { } center) continue;
            if (Vector(box, ExtentField) is not { } extent) continue;

            var scale = Vector(box, ScaleField) ?? new WorldPoint(1, 1, 1);

            floors.Add(new DungeonFloorLayout(
                PropertyWalk.GetText(area, AreaNameField),
                PropertyWalk.GetString(area, AreaTextureField),
                center,
                new WorldPoint(
                    Math.Abs(extent.X * scale.X), Math.Abs(extent.Y * scale.Y), Math.Abs(extent.Z * scale.Z))));
        }

        return floors;
    }

    private static void Read(
        IPackage package,
        IReadOnlySet<string> wanted,
        IReadOnlyList<DungeonFloorLayout> floors,
        List<CreaturePlacement> placements,
        List<IReadOnlyList<WorldPoint>> patrols)
    {
        var exports = package.GetExports().ToArray();
        var patrolByActor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var patrol in exports)
        {
            if (patrol.ExportType != PatrolClass) continue;

            var points = PatrolPoints(patrol);
            if (points.Count < 2) continue;

            var index = patrols.Count;
            patrols.Add(points);

            foreach (var member in PatrolMembers(patrol)) patrolByActor[member] = index;
        }

        foreach (var actor in exports)
        {
            if (CreatureOf(actor.ExportType) is not { } creature || !wanted.Contains(creature)) continue;
            if (Component(actor, RootField) is not { } root) continue;
            if (Vector(root, LocationField) is not { } position) continue;

            var floor = FloorAt(floors, position);

            placements.Add(new CreaturePlacement(creature, position)
            {
                Floor = floor,
                MapX = floor is { } at ? MapX(floors[at], position) : null,
                MapY = floor is { } on ? MapY(floors[on], position) : null,
                Patrol = patrolByActor.TryGetValue(actor.GetPathName(), out var patrolled) ? patrolled : null,
            });
        }
    }

    private static int? FloorAt(IReadOnlyList<DungeonFloorLayout> floors, WorldPoint position)
    {
        int? smallest = null;

        for (var at = 0; at < floors.Count; at++)
        {
            var (center, extent) = (floors[at].Center, floors[at].Extent);

            if (Math.Abs(position.X - center.X) > extent.X) continue;
            if (Math.Abs(position.Y - center.Y) > extent.Y) continue;
            if (Math.Abs(position.Z - center.Z) > extent.Z) continue;

            if (smallest is not { } best || Volume(floors[at]) < Volume(floors[best])) smallest = at;
        }

        return smallest;
    }

    private static double Volume(DungeonFloorLayout floor) => floor.Extent.X * floor.Extent.Y * floor.Extent.Z;

    private static double MapX(DungeonFloorLayout floor, WorldPoint position) =>
        Math.Round((position.Y - (floor.Center.Y - floor.Extent.Y)) / (2 * floor.Extent.Y), 6);

    private static double MapY(DungeonFloorLayout floor, WorldPoint position) =>
        Math.Round(((floor.Center.X + floor.Extent.X) - position.X) / (2 * floor.Extent.X), 6);

    private static IReadOnlyList<WorldPoint> PatrolPoints(UObject patrol)
    {
        if (PropertyWalk.Get(patrol, PathsField)?.GenericValue is not UScriptArray paths) return [];
        if (paths.Properties.FirstOrDefault()?.GenericValue is not FScriptStruct { StructType: IPropertyHolder path })
        {
            return [];
        }

        if (PropertyWalk.Get(path, PointsField)?.GenericValue is not UScriptArray listed) return [];

        var points = new List<WorldPoint>(listed.Properties.Count);

        foreach (var entry in listed.Properties)
        {
            if (Point(entry?.GenericValue) is not { } point) continue;
            if (point is { X: 0, Y: 0, Z: 0 }) continue;
            if (points.Count > 0 && points[^1] == point) continue;

            points.Add(point);
        }

        return points;
    }

    private static IEnumerable<string> PatrolMembers(UObject patrol)
    {
        if (PropertyWalk.Get(patrol, ColumnsField)?.GenericValue is not UScriptArray columns) yield break;

        foreach (var column in columns.Properties)
        {
            if (column?.GenericValue is not FScriptStruct { StructType: IPropertyHolder held }) continue;
            if (PropertyWalk.Get(held, CharactersField)?.GenericValue is not UScriptArray characters) continue;

            foreach (var character in characters.Properties)
            {
                if (PropertyWalk.AsString(character?.GenericValue) is { } path) yield return path;
            }
        }
    }

    private static UObject? Component(UObject actor, string field) =>
        PropertyWalk.Get(actor, field)?.GenericValue is FPackageIndex { IsNull: false } index
            ? index.Load<UObject>()
            : null;

    private static WorldPoint? Vector(UObject holder, string field) => Point(PropertyWalk.Get(holder, field)?.GenericValue);

    private static WorldPoint? Point(object? value) => value switch
    {
        FScriptStruct { StructType: FVector vector } => Rounded(vector.X, vector.Y, vector.Z),
        FVector vector => Rounded(vector.X, vector.Y, vector.Z),
        _ => null,
    };

    private static WorldPoint Rounded(double x, double y, double z) =>
        new(Math.Round(x, 3), Math.Round(y, 3), Math.Round(z, 3));
}
