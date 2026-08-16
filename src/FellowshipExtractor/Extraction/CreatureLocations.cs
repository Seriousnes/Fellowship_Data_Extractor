using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;

namespace FellowshipExtractor.Extraction;

public sealed class CreatureLocations
{
    private const string MapExtension = ".umap";
    private const string BlueprintSuffix = "_C";

    private readonly Dictionary<string, List<string>> _byCreature = new(StringComparer.OrdinalIgnoreCase);

    public int Dungeons { get; }

    public int Maps { get; private set; }

    public int Placed => _byCreature.Count;

    public int Housed { get; }

    public int Unplaced { get; }

    public CreatureLocations(
        AbstractFileProvider provider, DungeonNames dungeons, IReadOnlyCollection<string> creatures)
    {
        var wanted = new HashSet<string>(creatures, StringComparer.OrdinalIgnoreCase);

        var maps = provider.Files.Values
            .Where(file => file.Path.EndsWith(MapExtension, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dungeon in dungeons.All)
        {
            if (dungeon.LevelName is not { Length: > 0 } levelName) continue;

            var persistent = Array.Find(maps, file =>
                Path.GetFileNameWithoutExtension(file.Path).Equals(levelName, StringComparison.OrdinalIgnoreCase));

            if (persistent is null) continue;

            Dungeons++;

            var folder = persistent.Path[..(persistent.Path.LastIndexOf('/') + 1)];

            folders[dungeon.Tag] = folder;

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

        Housed = Housing(provider, wanted, Naming(folders));

        Unplaced = wanted.Count - _byCreature.Count;
    }

    private int Housing(
        AbstractFileProvider provider,
        IReadOnlyCollection<string> wanted,
        IReadOnlyDictionary<string, string> naming)
    {
        var homeless = wanted.Where(creature => !_byCreature.ContainsKey(creature)).ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        if (homeless.Count == 0 || naming.Count == 0) return 0;

        var housed = 0;

        foreach (var file in provider.Files.Values)
        {
            var named = Path.GetFileNameWithoutExtension(file.Path);

            if (!homeless.Contains(named)) continue;
            if (Housing(file.Path, naming) is not { } tag) continue;

            _byCreature[named] = [tag];
            homeless.Remove(named);
            housed++;
        }

        return housed;
    }

    public static string? Housing(string path, IReadOnlyDictionary<string, string> naming)
    {
        string? found = null;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!naming.TryGetValue(segment, out var tag)) continue;
            if (found is not null && !found.Equals(tag, StringComparison.OrdinalIgnoreCase)) return null;

            found = tag;
        }

        return found;
    }

    public static IReadOnlyDictionary<string, string> Naming(IReadOnlyDictionary<string, string> folders)
    {
        var under = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tag, folder) in folders)
        {
            foreach (var segment in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!under.TryGetValue(segment, out var dungeons)) under[segment] = dungeons = [];
                if (!dungeons.Contains(tag, StringComparer.OrdinalIgnoreCase)) dungeons.Add(tag);
            }
        }

        return under
            .Where(segment => segment.Value.Count == 1)
            .ToDictionary(segment => segment.Key, segment => segment.Value[0], StringComparer.OrdinalIgnoreCase);
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
