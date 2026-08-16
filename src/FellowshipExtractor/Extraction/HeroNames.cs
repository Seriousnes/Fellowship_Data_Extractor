using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed record HeroProfile(string Codename, string ShippedName)
{
    public string? ArmorType { get; init; }
    public required string PrimaryStat { get; init; }

    public string? ColorRow { get; init; }
}

public sealed class HeroNames
{
    public const string CharacterInfoRowStructure = "/Script/fellowship.CRCharacterInfoDataRow";
    public const string Table = "CharacterID";

    private const string MetaDataClass = "BP_HeroMetaDataAsset_C";
    private const string RowPrefix = "hero-";
    private const string ArmorTypeField = "ArmorType";
    private const string MainStatField = "MainStat";
    private const string HeroIdField = "HeroID";

    private readonly Dictionary<string, HeroProfile> _byCodename = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byCodename.Count;

    public IReadOnlyCollection<string> Codenames => _byCodename.Keys;

    public IEnumerable<HeroProfile> Profiles => _byCodename.Values.OrderBy(p => p.Codename, StringComparer.Ordinal);

    public List<string> Unresolved { get; } = [];

    public HeroNames(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ArmorTypes armor,
        IReadOnlyDictionary<string, string> primaryStatByHero)
    {
        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in CsvTables.ReadTags(provider))
        {
            if (!tag.Name.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var segment = tag.Name[HeroVariants.HeroTagPrefix.Length..];
            if (segment.Contains('.')) continue;
            canonical.TryAdd(segment, segment);
        }

        var declared = MainStats(provider, assets, primaryStatByHero);

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(MetaDataClass, StringComparison.Ordinal)) continue;

            var path = AbilityPresentationIndex.PackagePath(asset.ObjectPath);
            if (!provider.TryLoadPackage(path, out var package)) { Unresolved.Add(asset.ObjectPath); continue; }

            var metadata = package.GetExports()
                .FirstOrDefault(e => e.Class?.Name.Text == MetaDataClass || e.ExportType == MetaDataClass);
            if (metadata is null) { Unresolved.Add(asset.ObjectPath); continue; }

            var colourRow = ColourRow(metadata);
            var codename = Codename(colourRow);
            var shipped = PropertyWalk.GetString(metadata, "HeroName");

            if (codename is null || shipped is null) { Unresolved.Add(asset.ObjectPath); continue; }
            if (canonical.TryGetValue(codename, out var spelling)) codename = spelling;

            var stat = declared.GetValueOrDefault(codename) ?? primaryStatByHero.GetValueOrDefault(codename);
            if (stat is null)
            {
                throw new InvalidOperationException(
                    $"Hero {codename} has no primary stat: no row in DT_CharacterInfoTable and nothing derivable " +
                    "from the weight templates their own items name. Every HeroRelativePrimaryStat slot would " +
                    "export unresolved for them.");
            }

            _byCodename[codename] = new HeroProfile(codename, shipped)
            {
                ArmorType = armor.Label(PropertyWalk.Get(metadata, ArmorTypeField)?.GenericValue),
                PrimaryStat = stat,
                ColorRow = colourRow,
            };
        }
    }

    private static Dictionary<string, string> MainStats(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        IReadOnlyDictionary<string, string> primaryStatByHero)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var objectPath in TableFinder.ByRowStructure(assets, CharacterInfoRowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, HeroIdField) is not { } tag) continue;
                if (!tag.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var codename = tag[HeroVariants.HeroTagPrefix.Length..];
                if (codename.Contains('.')) continue;

                if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MainStatField)?.GenericValue) is { } ordinal)
                {
                    ordinals.TryAdd(codename, ordinal);
                }
            }
        }

        var statByOrdinal = new Dictionary<int, string>();

        foreach (var (codename, ordinal) in ordinals)
        {
            if (primaryStatByHero.GetValueOrDefault(codename) is not { } stat) continue;

            if (statByOrdinal.TryGetValue(ordinal, out var existing) &&
                !existing.Equals(stat, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"MainStat {ordinal} reads as {existing} for one hero and {stat} for {codename}. The decode " +
                    "comes from the weight templates each hero's gear names and no longer agrees with " +
                    "DT_CharacterInfoTable.");
            }

            statByOrdinal[ordinal] = stat;
        }

        return ordinals
            .Where(pair => statByOrdinal.ContainsKey(pair.Value))
            .ToDictionary(pair => pair.Key, pair => statByOrdinal[pair.Value], StringComparer.OrdinalIgnoreCase);
    }

    private static string? ColourRow(UObject metadata)
    {
        if (PropertyWalk.GetStruct(metadata, "HeroColor") is not { } colour) return null;
        if (PropertyWalk.GetStruct(colour, "NamedOverrideColor") is not { } named) return null;

        return PropertyWalk.GetString(named, "RowName");
    }

    private static string? Codename(string? colourRow)
    {
        var marker = colourRow?.LastIndexOf(RowPrefix, StringComparison.OrdinalIgnoreCase) ?? -1;
        return marker < 0 ? null : colourRow![(marker + RowPrefix.Length)..];
    }

    public HeroProfile? Rostered(string tagName)
    {
        if (!tagName.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) return null;

        var codename = tagName[HeroVariants.HeroTagPrefix.Length..];

        return codename.Contains('.') ? null : _byCodename.GetValueOrDefault(codename);
    }

    public string? Shipped(string codenameOrTag) => Profile(codenameOrTag)?.ShippedName;

    public HeroProfile? Profile(string codenameOrTag) =>
        _byCodename.GetValueOrDefault(LastSegment(codenameOrTag));

    public static string LastSegment(string tag)
    {
        var dot = tag.LastIndexOf('.');
        return dot >= 0 ? tag[(dot + 1)..] : tag;
    }
}
