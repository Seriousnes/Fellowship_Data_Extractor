using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Maps a hero's internal codename to the name the game shows. Assets refer to heroes only by
/// codename - "CharacterID.Hero.Mosse" - while the metadata asset holds the shipped name.
///
/// The link is the hero's named colour row, "game.game.hero-mosse", which is a data field rather
/// than a path convention and holds even for the hero whose content lives in its own plugin root.
/// </summary>
public sealed class HeroNames
{
    private const string MetaDataClass = "BP_HeroMetaDataAsset_C";
    private const string RowPrefix = "hero-";

    private readonly Dictionary<string, string> _shippedByCodename = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _shippedByCodename.Count;

    /// <summary>Metadata assets whose codename could not be derived, reported rather than dropped.</summary>
    public List<string> Unresolved { get; } = [];

    public HeroNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
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

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(MetaDataClass, StringComparison.Ordinal)) continue;

            var path = AbilityPresentationIndex.PackagePath(asset.ObjectPath);
            if (!provider.TryLoadPackage(path, out var package)) { Unresolved.Add(asset.ObjectPath); continue; }

            var metadata = package.GetExports()
                .FirstOrDefault(e => e.Class?.Name.Text == MetaDataClass || e.ExportType == MetaDataClass);
            if (metadata is null) { Unresolved.Add(asset.ObjectPath); continue; }

            var codename = ColourRowCodename(metadata);
            var shipped = PropertyWalk.GetString(metadata, "HeroName");

            if (codename is null || shipped is null) { Unresolved.Add(asset.ObjectPath); continue; }
            if (canonical.TryGetValue(codename, out var spelling)) codename = spelling;

            _shippedByCodename[codename] = shipped;
        }
    }

    private static string? ColourRowCodename(UObject metadata)
    {
        if (PropertyWalk.GetStruct(metadata, "HeroColor") is not { } colour) return null;
        if (PropertyWalk.GetStruct(colour, "NamedOverrideColor") is not { } named) return null;

        var row = PropertyWalk.GetString(named, "RowName");
        var marker = row?.LastIndexOf(RowPrefix, StringComparison.OrdinalIgnoreCase) ?? -1;
        return marker < 0 ? null : row![(marker + RowPrefix.Length)..];
    }

    /// <summary>The shipped name for a codename or a "CharacterID.Hero.X" tag.</summary>
    public string? Shipped(string codenameOrTag) =>
        _shippedByCodename.GetValueOrDefault(LastSegment(codenameOrTag));

    public static string LastSegment(string tag)
    {
        var dot = tag.LastIndexOf('.');
        return dot >= 0 ? tag[(dot + 1)..] : tag;
    }
}
