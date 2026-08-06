using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record AbilityPresentation(string Table, string RowName, FStructFallback Row)
{
    public string? MatchedBy { get; init; }
}

public sealed class AbilityPresentationIndex
{
    public const string RowStructure = "/Script/fellowship.CRAbilityDataRow";

    private readonly Dictionary<string, AbilityPresentation> _byRowName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityPresentation> _byAbilityAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    public int TableCount { get; }
    public int RowCount { get; }

    public IReadOnlyCollection<string> AbilityNames => _names;

    public List<(string Path, string Reason)> FailedTables { get; } = [];

    public AbilityPresentationIndex(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var tables = assets
            .Where(a => a.TagsAndValues.Any(kv =>
                kv.Key.Text.Equals("RowStructure", StringComparison.OrdinalIgnoreCase) &&
                kv.Value == RowStructure))
            .Select(a => a.ObjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var objectPath in tables)
        {
            UDataTable table;
            try
            {
                table = provider.LoadPackageObject<UDataTable>(PackagePath(objectPath));
            }
            catch (Exception ex)
            {
                FailedTables.Add((objectPath, $"{ex.GetType().Name}: {ex.Message}"));
                continue;
            }

            TableCount++;

            foreach (var (rowName, row) in table.RowMap)
            {
                var presentation = new AbilityPresentation(table.Name, rowName.Text, row);
                RowCount++;

                _byRowName.TryAdd(rowName.Text, presentation);
                _byRowName.TryAdd(StripClassSuffix(rowName.Text), presentation);

                if (PropertyWalk.GetString(row, "GameplayAbilityAsset") is { } asset)
                {
                    _byAbilityAsset.TryAdd(StripClassSuffix(AssetName(asset)), presentation);
                }

                if (PropertyWalk.GetString(row, "Name") is { } shown) _names.Add(shown);
            }
        }
    }

    public AbilityPresentation? Find(string abilityName)
    {
        if (_byRowName.TryGetValue(abilityName, out var direct))
        {
            return direct with { MatchedBy = "RowName" };
        }

        if (_byAbilityAsset.TryGetValue(StripClassSuffix(abilityName), out var viaAsset))
        {
            return viaAsset with { MatchedBy = "GameplayAbilityAsset" };
        }

        return null;
    }

    private static string StripClassSuffix(string name) =>
        name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name;

    public static string AssetName(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot >= 0 ? path[(dot + 1)..] : path;
    }

    public static string PackagePath(string objectPath)
    {
        var dot = objectPath.LastIndexOf('.');
        return dot >= 0 ? objectPath[..dot] : objectPath;
    }
}
