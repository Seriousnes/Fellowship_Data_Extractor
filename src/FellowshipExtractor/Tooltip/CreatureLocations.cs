using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;

namespace FellowshipExtractor.Tooltip;

public sealed class CreatureLocations
{
    private const string MapExtension = ".umap";
    private const string BlueprintSuffix = "_C";

    private readonly Dictionary<string, List<string>> _byCreature = new(StringComparer.OrdinalIgnoreCase);

    public int Dungeons { get; }

    public int Maps { get; private set; }

    public int Placed => _byCreature.Count;

    public int Unplaced { get; }

    public CreatureLocations(
        AbstractFileProvider provider, DungeonNames dungeons, IReadOnlyCollection<string> creatures)
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

            if (persistent is null) continue;

            Dungeons++;

            var folder = persistent.Path[..(persistent.Path.LastIndexOf('/') + 1)];

            foreach (var map in maps)
            {
                if (!map.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                if (!provider.TryLoadPackage(map, out var package)) continue;

                Maps++;

                foreach (var placed in Placements(package))
                {
                    if (!wanted.Contains(placed)) continue;

                    if (!_byCreature.TryGetValue(placed, out var found))
                    {
                        _byCreature[placed] = found = [];
                    }

                    if (!found.Contains(dungeon.Tag, StringComparer.OrdinalIgnoreCase)) found.Add(dungeon.Tag);
                }
            }
        }

        Unplaced = wanted.Count - _byCreature.Count;
    }

    public IReadOnlyList<string> For(string creature) =>
        _byCreature.TryGetValue(creature, out var found) ? found : [];

    private static IEnumerable<string> Placements(IPackage package)
    {
        var names = package switch
        {
            IoPackage io => io.ExportMap.Select(export => io.CreateFNameFromMappedName(export.ObjectName).Text),
            Package legacy => legacy.ExportMap.Select(export => export.ObjectName.Text),
            _ => [],
        };

        return names.Select(PlacedFrom).OfType<string>();
    }

    public static string? PlacedFrom(string objectName)
    {
        var instance = objectName.LastIndexOf('_');
        if (instance <= 0) return null;

        for (var at = instance + 1; at < objectName.Length; at++)
        {
            if (!char.IsAsciiDigit(objectName[at])) return null;
        }

        if (instance + 1 == objectName.Length) return null;

        var placed = objectName[..instance];

        return placed.EndsWith(BlueprintSuffix, StringComparison.Ordinal)
            ? placed[..^BlueprintSuffix.Length]
            : null;
    }
}
