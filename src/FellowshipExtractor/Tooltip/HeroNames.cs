using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What a hero is, for the three things an item needs to know. The shipped name is what a tooltip
/// prints; the armour type decides which of an item's names the hero sees; the primary stat
/// decides what the item's HeroRelativePrimaryStat slot actually holds.
/// </summary>
public sealed record HeroProfile(string Codename, string ShippedName)
{
    public string? ArmorType { get; init; }
    public required string PrimaryStat { get; init; }

    /// <summary>
    /// The design system colour row the hero's metadata points at. Followed rather than the
    /// ClassColor literal sitting beside it, because the two disagree: Aeona's asset says BC00FF
    /// while the row it names is FC9FEC, and Mara's says 6F2EF1 against the row's 4519A3. The
    /// pointer is the asset deferring to the shared palette, so the palette is the answer.
    /// </summary>
    public string? ColorRow { get; init; }
}

/// <summary>
/// Maps a hero's internal codename to the name the game shows. Assets refer to heroes only by
/// codename - "CharacterID.Hero.Mosse" - while the metadata asset holds the shipped name and the
/// armour type.
///
/// The link is the hero's named colour row, "game.game.hero-mosse", which is a data field rather
/// than a path convention and holds even for the hero whose content lives in its own plugin root.
/// </summary>
public sealed class HeroNames
{
    public const string CharacterInfoRowStructure = "/Script/fellowship.CRCharacterInfoDataRow";

    private const string MetaDataClass = "BP_HeroMetaDataAsset_C";
    private const string RowPrefix = "hero-";
    private const string ArmorTypeField = "ArmorType";
    private const string MainStatField = "MainStat";
    private const string HeroIdField = "HeroID";

    private readonly Dictionary<string, HeroProfile> _byCodename = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byCodename.Count;

    /// <summary>Every hero the build ships a metadata asset for, spelled as assets refer to them.</summary>
    public IReadOnlyCollection<string> Codenames => _byCodename.Keys;

    /// <summary>The whole roster, in codename order.</summary>
    public IEnumerable<HeroProfile> Profiles => _byCodename.Values.OrderBy(p => p.Codename, StringComparer.Ordinal);

    /// <summary>Metadata assets whose codename could not be derived, reported rather than dropped.</summary>
    public List<string> Unresolved { get; } = [];

    public HeroNames(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ArmorTypes armor,
        IReadOnlyDictionary<string, string> primaryStatByHero)
    {
        // The colour row spells the codename in lower case; the gameplay tag table is where its
        // canonical spelling lives, and that is what every other asset refers to a hero by.
        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in CsvExportCommands.ReadTags(provider))
        {
            if (!tag.Name.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var segment = tag.Name[HeroVariants.HeroTagPrefix.Length..];
            if (segment.Contains('.')) continue;   // "Mara.Cat" and the tutorial variants
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

    /// <summary>
    /// The primary stat DT_CharacterInfoTable declares per hero. MainStat is an enum the usmap
    /// does not carry, so the table holds a bare ordinal and says nothing about what it means. The
    /// decode is not guessed: each ordinal is matched against the stat that hero's own gear names
    /// through its weight templates, and an ordinal two heroes read as two different stats throws
    /// rather than picking one.
    ///
    /// The table answers for eleven of the twelve live heroes and has no row for Gunde at all, so
    /// the templates are what covers them.
    /// </summary>
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

                // "CharacterID.Hero.Rime.Tutorial" is the tutorial's stand-in, not a hero.
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

    /// <summary>The shipped name for a codename or a "CharacterID.Hero.X" tag.</summary>
    public string? Shipped(string codenameOrTag) => Profile(codenameOrTag)?.ShippedName;

    /// <summary>What is known about a hero, or null when the codename names no shipped hero.</summary>
    public HeroProfile? Profile(string codenameOrTag) =>
        _byCodename.GetValueOrDefault(LastSegment(codenameOrTag));

    public static string LastSegment(string tag)
    {
        var dot = tag.LastIndexOf('.');
        return dot >= 0 ? tag[(dot + 1)..] : tag;
    }
}
