using System.Text;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed record HeroProfile(string Codename, string ShippedName)
{
    public string? ArmorType { get; init; }
    public required string PrimaryStat { get; init; }

    public int PrimaryAttribute { get; init; }

    public string? ColorRow { get; init; }

    public string? Mechanics { get; init; }

    public string? PortraitPath { get; init; }

    public string? Title { get; init; }

    public string? Biography { get; init; }

    public string? Playstyle { get; init; }

    public string? SpiritAbility { get; init; }

    public string? Role { get; init; }

    public string? RoleIconPath { get; init; }

    public int? OverallDifficulty { get; init; }

    public double? Offense { get; init; }

    public double? Defense { get; init; }

    public double? Control { get; init; }
}

public sealed class HeroNames
{
    public const string CharacterInfoRowStructure = "/Script/fellowship.CRCharacterInfoDataRow";
    public const string Table = "CharacterID";

    private const string MetaDataClass = "BP_HeroMetaDataAsset_C";
    private const string RowPrefix = "hero-";
    private const string ArmorTypeField = "ArmorType";
    private const string MainStatField = "MainStat";
    private const string PrimaryAttributeField = "PrimaryAttribute";
    private const string HeroIdField = "HeroID";
    private const string MechanicsField = "HeroMechanics";
    private const string PortraitField = "PortraitTexture";
    private const string TitleField = "HeroTitle";
    private const string BiographyField = "Biography";
    private const string PlaystyleField = "HeroDescription";
    private const string SpiritAbilityField = "SpiritDescription";
    private const string RoleField = "CharacterRole";
    private const string RoleEnum = "EPlayerRole";
    private const string DifficultyField = "OverallDifficulty";
    private const string OffenseField = "Offense";
    private const string DefenseField = "Defense";
    private const string ControlField = "Control";

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

        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException(
                $"No mappings are loaded, so nothing states which member of {RoleEnum} a hero left at its " +
                "default holds.");
        var roles = EnumOrdinals.Members(mappings, RoleEnum);
        var roleIcons = new RoleIcons(provider, mappings);

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

            var role = RoleIcons.Titled(
                EnumOrdinals.Member(mappings, RoleEnum, PropertyWalk.AsString(Declared(metadata, RoleField, provider)))
                ?? roles[roles.Keys.Min()]);

            if (!roleIcons.ByRole.TryGetValue(role, out var roleIcon))
            {
                throw new InvalidOperationException(
                    $"Hero {codename} has role {role}, and {RoleIcons.Widget}.{RoleIcons.Function} draws no " +
                    "badge for it.");
            }

            _byCodename[codename] = new HeroProfile(codename, shipped)
            {
                ArmorType = armor.Label(PropertyWalk.Get(metadata, ArmorTypeField)?.GenericValue),
                PrimaryStat = stat,
                PrimaryAttribute = PropertyWalk.AsOrdinal(Declared(metadata, PrimaryAttributeField, provider)) ?? 0,
                ColorRow = colourRow,
                Mechanics = PropertyWalk.GetString(metadata, MechanicsField),
                PortraitPath = PropertyWalk.GetString(metadata, PortraitField),
                Title = PropertyWalk.GetText(metadata, TitleField),
                Biography = PropertyWalk.GetText(metadata, BiographyField),
                Playstyle = PropertyWalk.GetText(metadata, PlaystyleField),
                SpiritAbility = PropertyWalk.GetText(metadata, SpiritAbilityField),
                Role = role,
                RoleIconPath = roleIcon,
                OverallDifficulty = PropertyWalk.AsOrdinal(Declared(metadata, DifficultyField, provider)),
                Offense = PropertyWalk.AsNumber(Declared(metadata, OffenseField, provider)),
                Defense = PropertyWalk.AsNumber(Declared(metadata, DefenseField, provider)),
                Control = PropertyWalk.AsNumber(Declared(metadata, ControlField, provider)),
            };
        }
    }

    private static object? Declared(UObject metadata, string field, AbstractFileProvider provider)
    {
        if (PropertyWalk.Get(metadata, field) is { } own) return own.GenericValue;

        return metadata.Class?.GetPathName() is { } classPath && ClassDefaults.Load(classPath, provider) is { } cdo
            ? ClassDefaults.Inherited(cdo, field, provider)?.GenericValue
            : null;
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

    public HeroProfile? ForTag(string tagName)
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

    private Dictionary<string, string> Exact =>
        field ??= _byCodename.Values.ToDictionary(
            hero => hero.Codename, hero => hero.ShippedName, StringComparer.Ordinal);

    public string Renamed(string key)
    {
        var segments = key.Split('.');
        var changed = false;

        for (var at = 0; at < segments.Length; at++)
        {
            var rewritten = Segment(segments[at]);
            if (ReferenceEquals(rewritten, segments[at])) continue;

            segments[at] = rewritten;
            changed = true;
        }

        return changed ? string.Join('.', segments) : key;
    }

    private string Segment(string segment)
    {
        var built = new StringBuilder(segment.Length + 8);
        var changed = false;
        var start = 0;

        for (var at = 1; at <= segment.Length; at++)
        {
            if (at < segment.Length && !(char.IsUpper(segment[at]) && !char.IsUpper(segment[at - 1]))) continue;

            var word = segment[start..at];
            start = at;

            if (Exact.TryGetValue(word, out var shipped) && !shipped.Equals(word, StringComparison.Ordinal))
            {
                built.Append(shipped);
                changed = true;
            }
            else
            {
                built.Append(word);
            }
        }

        return changed ? built.ToString() : segment;
    }
}
