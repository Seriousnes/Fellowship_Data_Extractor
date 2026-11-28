using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record AbilityPresentation(string Table, string RowName, FStructFallback Row)
{
    public string? MatchedBy { get; init; }
}

public sealed class AbilityPresentationIndex
{
    public const string RowStructure = "/Script/fellowship.CRAbilityDataRow";

    private const string AbilityAssetField = "GameplayAbilityAsset";

    private const string AdditionalMatch = "AdditionalGameplayAbilityAssets";

    private readonly Dictionary<string, AbilityPresentation> _byRowName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityPresentation> _byAbilityAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityPresentation?> _byAdditionalAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityPresentation> _dataMap = new(StringComparer.OrdinalIgnoreCase);

    public int TableCount { get; }
    public int RowCount { get; }
    public int AdditionalAssetCount => _byAdditionalAsset.Values.Count(p => p is not null);

    public IReadOnlyCollection<string> AbilityNames => _names;

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
            catch (Exception)
            {
                continue;
            }

            TableCount++;

            foreach (var (rowName, row) in table.RowMap)
            {
                var presentation = new AbilityPresentation(table.Name, rowName.Text, row);
                RowCount++;

                _byRowName.TryAdd(rowName.Text, presentation);
                _byRowName.TryAdd(StripClassSuffix(rowName.Text), presentation);

                if (PropertyWalk.GetString(row, AbilityAssetField) is { } asset)
                {
                    _byAbilityAsset.TryAdd(StripClassSuffix(AssetName(asset)), presentation);
                }

                if (PropertyWalk.Get(row, "AdditionalGameplayAbilityAssets")?.GenericValue is UScriptArray additional)
                {
                    foreach (var element in additional.Properties)
                    {
                        if (PropertyWalk.AsString(element?.GenericValue) is not { } path) continue;

                        var key = StripClassSuffix(AssetName(path));
                        if (!_byAdditionalAsset.TryAdd(key, presentation)) _byAdditionalAsset[key] = null;
                    }
                }

                if (PropertyWalk.GetString(row, "Name") is { } shown) _names.Add(shown);

                MapData(presentation);
            }
        }
    }

    /// <summary>
    /// The row's entries of <c>CRAbilityDataSubsystem</c>'s <c>AbilityDataMap</c>, as its populator makes them (shipping
    /// exe 26320967, 0x1450C7FE0). A row with a <c>GameplayAbilityAsset</c> the map has is left out (0x1450C8158).
    /// Any other row is under its <c>GameplayAbilityAsset</c> (0x1450C8459) and under each of its
    /// <c>AdditionalGameplayAbilityAssets</c> (0x1450C84CD). The row's name is no key.
    /// </summary>
    private void MapData(AbilityPresentation presentation)
    {
        var asset = PropertyWalk.GetString(presentation.Row, AbilityAssetField);

        if (!_dataMap.TryAdd(DataKey(asset), presentation)) return;

        if (PropertyWalk.Get(presentation.Row, AdditionalMatch)?.GenericValue is not UScriptArray additional) return;

        foreach (var element in additional.Properties)
        {
            _dataMap[DataKey(PropertyWalk.AsString(element?.GenericValue))] = presentation;
        }
    }

    private static string DataKey(string? classPath) =>
        string.IsNullOrEmpty(classPath) || classPath == "None" ? string.Empty : StripClassSuffix(AssetName(classPath));

    /// <summary>
    /// The <c>Name</c> <c>AbilityDataMap</c> has for the ability's class, or null for a class with no entry. The
    /// subsystem returns the name <c>Invalid Ability Data</c> for such a class.
    /// </summary>
    public string? DataName(string abilityName) =>
        _dataMap.TryGetValue(StripClassSuffix(abilityName), out var data) ? PropertyWalk.GetString(data.Row, "Name") : null;

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

        if (_byAdditionalAsset.GetValueOrDefault(StripClassSuffix(abilityName)) is { } viaAdditional)
        {
            return viaAdditional with { MatchedBy = AdditionalMatch };
        }

        return null;
    }

    public string? HostOf(string abilityName) =>
        Find(abilityName) is { MatchedBy: AdditionalMatch } presentation &&
        PropertyWalk.GetString(presentation.Row, AbilityAssetField) is { } asset
            ? StripClassSuffix(AssetName(asset))
            : null;

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
