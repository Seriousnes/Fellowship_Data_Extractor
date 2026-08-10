using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public sealed class DungeonNames
{
    public const string RowStructure = "/Script/fellowship.CRLevelInfoRow";

    private const string LevelInfoClass = "CRLevelInfo";
    private const string DisplayNameField = "DisplayName";
    private const string LevelIdField = "LevelID";
    private const string InfoField = "Info";

    private readonly Dictionary<string, string> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byTag.Count;

    public DungeonNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.GetText(row, DisplayNameField) is { Length: > 0 } printed)
                {
                    _byTag.TryAdd(rowName.Text, printed);
                }
            }
        }

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(LevelInfoClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (Load(provider, asset.ObjectPath) is not { } level) continue;
            if (PropertyWalk.GetString(level, LevelIdField) is not { } tag) continue;
            if (PropertyWalk.GetStruct(level, InfoField) is not { } info) continue;
            if (PropertyWalk.GetText(info, DisplayNameField) is not { Length: > 0 } printed) continue;

            _byTag.TryAdd(tag, printed);
        }
    }

    public string? For(string levelTag) => _byTag.GetValueOrDefault(levelTag);

    public static UObject? Load(AbstractFileProvider provider, string objectPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return null;
        }

        var exports = package.GetExports().ToArray();

        return exports.FirstOrDefault(export => export.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();
    }
}
