using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record Display(
    string? Name, string? Description, string? Icon, IReadOnlyList<string> Detail, string? Resource)
{
    public static Display From(IPropertyHolder? row, UObject? asset)
    {
        var name = row is null ? null : PropertyWalk.GetString(row, "Name");
        var description = row is null ? null : DescriptionOf(row);
        var icon = row is null ? null : IconOf(row);
        var detail = row is null ? [] : DetailOf(row);
        var resource = row is null ? null : Text(row, "DetailedDescription3");

        if (asset is not null)
        {
            name ??= FirstText(asset, "DisplayName", "Name", "Title");
            description ??= FirstText(asset, "Description", "Tooltip", "FlavorText") ?? DynamicText(asset);
            icon ??= FirstIcon(asset);

            if (ItemNames.Read(asset) is { } naming)
            {
                name = naming.Unambiguous ?? naming.Scalar;
            }
        }

        return new Display(name, description, icon, detail, resource);
    }

    private static string? DescriptionOf(IPropertyHolder row) =>
        DynamicText(row) ?? PropertyWalk.GetString(row, "DetailedDescription");

    private static string? DynamicText(IPropertyHolder holder) =>
        PropertyWalk.GetStruct(holder, "DynamicDescription") is { } dynamic
            ? PropertyWalk.GetString(dynamic, "Text")
            : null;

    private static List<string> DetailOf(IPropertyHolder row)
    {
        var lines = new List<string>();
        foreach (var field in new[] { "DetailedDescription", "DetailedDescription2", "TutorialDescription" })
        {
            if (Text(row, field) is { } text) lines.Add(text);
        }
        return lines;
    }

    private static string? Text(IPropertyHolder row, string field)
    {
        var text = PropertyWalk.GetString(row, field)?.Trim(' ', '.', '\r', '\n', '\t');
        return string.IsNullOrEmpty(text) ? null : PropertyWalk.GetString(row, field);
    }

    private static string? IconOf(IPropertyHolder row)
    {
        if (PropertyWalk.GetStruct(row, "UIIconTexture") is { } brush)
        {
            return PropertyWalk.GetString(brush, "ResourceObject");
        }
        return null;
    }

    private static string? FirstText(UObject asset, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            foreach (var property in asset.Properties)
            {
                if (!property.Name.Text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Tag?.GenericValue is FScriptStruct or UScriptArray or UScriptMap) continue;
                if (PropertyWalk.AsString(property.Tag?.GenericValue) is { } text) return text;
            }
        }
        return null;
    }

    private static string? FirstIcon(UObject asset)
    {
        string? fallback = null;

        foreach (var property in asset.Properties)
        {
            var name = property.Name.Text;
            var looksLikeIcon = name.Contains("Icon", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Image", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Texture", StringComparison.OrdinalIgnoreCase);
            if (!looksLikeIcon) continue;

            if (Referenced(property) is not { } value) continue;
            if (name.Contains("Icon", StringComparison.OrdinalIgnoreCase)) return value;
            fallback ??= value;
        }

        return fallback;
    }

    private static string? Referenced(FPropertyTag property)
    {
        if (property.Tag?.GenericValue is FScriptStruct { StructType: IPropertyHolder brush })
        {
            return PropertyWalk.GetString(brush, "ResourceObject");
        }

        return property.TagData?.Type is "ObjectProperty" or "SoftObjectProperty"
            ? PropertyWalk.AsString(property.Tag?.GenericValue)
            : null;
    }
}
