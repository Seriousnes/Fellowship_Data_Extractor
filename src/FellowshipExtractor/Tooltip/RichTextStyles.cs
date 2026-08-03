using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One rich text style the game draws a tagged run in: its colour, the typeface weight, and the
/// size. Weight arrives as the name of the face inside the font asset, which is a number for the
/// upright faces and a number with an Italic suffix for the slanted ones.
/// </summary>
public sealed record TextStyleEntry(string Color, string Weight, double Size);

/// <summary>
/// The style set a description's own markup names.
///
/// Description text tags its runs the way the game's rich text widget expects, writing
/// "&lt;rt.warning&gt;+3%&lt;/&gt;", and every one of those tags is the name of a row in the
/// shipped style set. So the colours a tooltip draws with are stated by the build rather than
/// chosen by whoever renders it, and a renderer that picks its own drifts the moment an artist
/// changes one.
///
/// The rows named as tags are what a description can reach. Several tables in the build share the
/// row structure - the chat log and the EULA have their own - and all of them define Default and
/// Bold, which a description never names. Reading only the rows whose name is a tag takes the ones
/// that can appear in text and leaves the per-widget defaults where they belong.
/// </summary>
public sealed class RichTextStyles
{
    private const string RowStructure = "/Script/UMG.RichTextStyleRow";

    /// <summary>The prefix every style a description names is filed under.</summary>
    private const string TagPrefix = "rt.";

    private const string StyleField = "TextStyle";
    private const string ColorField = "ColorAndOpacity";
    private const string ColorValueField = "SpecifiedColor";
    private const string FontField = "Font";
    private const string WeightField = "TypefaceFontName";
    private const string SizeField = "Size";

    public IReadOnlyDictionary<string, TextStyleEntry> Styles { get; }

    public int Count => Styles.Count;

    public RichTextStyles(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var found = new Dictionary<string, TextStyleEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                var name = rowName.Text;
                if (!name.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                if (Read(row) is { } style) found.TryAdd(name, style);
            }
        }

        Styles = found;
    }

    private static TextStyleEntry? Read(IPropertyHolder row)
    {
        if (PropertyWalk.GetStruct(row, StyleField) is not { } style) return null;

        var colour = PropertyWalk.GetStruct(style, ColorField) is { } holder
            ? PropertyWalk.GetString(holder, ColorValueField)
            : null;

        if (colour is null) return null;

        var font = PropertyWalk.GetStruct(style, FontField);
        var weight = font is null ? null : PropertyWalk.GetString(font, WeightField);
        var size = font is null ? null : PropertyWalk.AsNumber(PropertyWalk.Get(font, SizeField)?.GenericValue);

        return new TextStyleEntry(colour, weight ?? "400", size ?? 0);
    }
}
