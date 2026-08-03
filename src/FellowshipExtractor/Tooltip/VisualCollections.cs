using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The armour art a CRVisualCollection holds. A collection is a hero's set in every rung it exists
/// at: Skins[n] is one rung, carrying the meshes the hero wears and, beside each, the icon a bag
/// square shows for that slot. Gunde's progression set has three, whose meshes are the T0, T1 and
/// T2 folders and whose icons are the R0, R1 and R2 files, so the R in an icon's name is the rung
/// and not a variation the icons have on their own.
///
/// Reading is cached per collection, because one collection answers for every slot of every item
/// that dresses a hero in it.
/// </summary>
public sealed class VisualCollections
{
    private const string SkinsField = "Skins";
    private const string SkinMeshesField = "SkinMeshes";
    private const string IconFieldPrefix = "Icon";

    private readonly AbstractFileProvider _provider;
    private readonly Dictionary<string, UScriptArray?> _skins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _icons = new(StringComparer.OrdinalIgnoreCase);

    public VisualCollections(AbstractFileProvider provider) => _provider = provider;

    /// <summary>How many rungs a collection ships, or zero when it is not a collection at all.</summary>
    public int RungCount(string collectionPath) => Skins(collectionPath)?.Properties.Count ?? 0;

    /// <summary>
    /// The icon one rung of a collection shows for one slot, or null where it has none. The slot
    /// arrives under its ECRHeroItemType name rather than the word a player reads, because the
    /// field is named for the enum: IconWaist and not IconLegs.
    /// </summary>
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
