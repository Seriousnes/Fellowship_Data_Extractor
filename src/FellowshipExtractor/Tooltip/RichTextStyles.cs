using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public sealed record TextStyleEntry(string Color, string Weight, double Size);

public sealed class RichTextStyles
{
    private const string RowStructure = "/Script/UMG.RichTextStyleRow";

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
