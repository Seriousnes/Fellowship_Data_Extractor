using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;

namespace FellowshipExtractor.Extraction;

public sealed class CreatureLocations
{
    private const string MapExtension = ".umap";

    private readonly Dictionary<string, List<string>> _byCreature = new(StringComparer.OrdinalIgnoreCase);

    public int Dungeons { get; }

    public int Maps { get; private set; }

    public int Placed => _byCreature.Count;

    public int PlacedByFolder { get; }

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

        PlacedByFolder = ByFolder(provider, wanted, TagsByFolder(folders));

        Unplaced = wanted.Count - _byCreature.Count;
    }

    private int ByFolder(
        AbstractFileProvider provider,
        IReadOnlyCollection<string> wanted,
        IReadOnlyDictionary<string, string> tagsByFolder)
    {
        var unplaced = wanted.Where(creature => !_byCreature.ContainsKey(creature)).ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        if (unplaced.Count == 0 || tagsByFolder.Count == 0) return 0;

        var placed = 0;

        foreach (var file in provider.Files.Values)
        {
            var name = Path.GetFileNameWithoutExtension(file.Path);

            if (!unplaced.Contains(name)) continue;
            if (DungeonOf(file.Path, tagsByFolder) is not { } tag) continue;

            _byCreature[name] = [tag];
            unplaced.Remove(name);
            placed++;
        }

        return placed;
    }

    public static string? DungeonOf(string path, IReadOnlyDictionary<string, string> tagsByFolder)
    {
        string? dungeon = null;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!tagsByFolder.TryGetValue(segment, out var tag)) continue;
            if (dungeon is not null && !dungeon.Equals(tag, StringComparison.OrdinalIgnoreCase)) return null;

            dungeon = tag;
        }

        return dungeon;
    }

    public static IReadOnlyDictionary<string, string> TagsByFolder(IReadOnlyDictionary<string, string> folders)
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

    private static IEnumerable<string> Placements(IPackage package) =>
        DungeonLayouts.Classes(package).Select(DungeonLayouts.CreatureOf).OfType<string>();
}
