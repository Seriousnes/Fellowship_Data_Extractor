using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed class VisualCollections
{
    private const string SkinsField = "Skins";
    private const string SkinMeshesField = "SkinMeshes";
    private const string IconFieldPrefix = "Icon";

    private readonly AbstractFileProvider _provider;
    private readonly Dictionary<string, UScriptArray?> _skins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _icons = new(StringComparer.OrdinalIgnoreCase);

    public VisualCollections(AbstractFileProvider provider) => _provider = provider;

    public int RungCount(string collectionPath) => Skins(collectionPath)?.Properties.Count ?? 0;

    public string? Icon(string collectionPath, int rung, string slotType)
    {
        var key = $"{collectionPath}#{rung}#{slotType}";
        if (_icons.TryGetValue(key, out var cached)) return cached;

        return _icons[key] = Read();

        string? Read()
        {
            if (Skins(collectionPath) is not { } skins) return null;
            if (rung < 0 || rung >= skins.Properties.Count) return null;

            if (skins.Properties[rung]?.GenericValue is not FScriptStruct { StructType: IPropertyHolder skin })
            {
                return null;
            }

            return PropertyWalk.GetStruct(skin, SkinMeshesField) is { } meshes
                ? PropertyWalk.GetString(meshes, IconFieldPrefix + slotType)
                : null;
        }
    }

    private UScriptArray? Skins(string collectionPath)
    {
        if (_skins.TryGetValue(collectionPath, out var cached)) return cached;

        _provider.TryLoadPackageObject<UObject>(
            AbilityPresentationIndex.PackagePath(collectionPath), out var collection);

        return _skins[collectionPath] = collection is null
            ? null
            : PropertyWalk.Get(collection, SkinsField)?.GenericValue as UScriptArray;
    }
}
