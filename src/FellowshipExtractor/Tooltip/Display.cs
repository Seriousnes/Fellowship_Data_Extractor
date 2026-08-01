using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The presentation fields a tooltip shows. Abilities carry these in a CRAbilityDataRow; data
/// assets such as mounts carry equivalents on the asset itself, so both are searched by shape.
/// </summary>
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

        // Data assets have no presentation row, so fall back to their own text and icon fields.
        if (asset is not null)
        {
            name ??= FirstText(asset, "DisplayName", "Name", "Title");
            description ??= FirstText(asset, "Description", "Tooltip", "FlavorText");
            icon ??= FirstIcon(asset);

            if (name is null || name.Equals(asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                name = MappedName(asset) ?? name;
            }
        }

        return new Display(name, description, icon, detail, resource);
    }

    private static string? DescriptionOf(IPropertyHolder row)
    {
        if (PropertyWalk.GetStruct(row, "DynamicDescription") is { } dynamic &&
            PropertyWalk.GetString(dynamic, "Text") is { } text)
        {
            return text;
        }

        return PropertyWalk.GetString(row, "DetailedDescription");
    }

    /// <summary>
    /// The extra description lines a row may carry, in the order the row declares them.
    /// DetailedDescription3 is not one of them: designers use it for the resource an ability costs
    /// - Mana, Spirit, Fury, Burning Ember - so it is read as that instead.
    /// </summary>
    private static List<string> DetailOf(IPropertyHolder row)
    {
        var lines = new List<string>();
        foreach (var field in new[] { "DetailedDescription", "DetailedDescription2", "TutorialDescription" })
        {
            if (Text(row, field) is { } text) lines.Add(text);
        }
        return lines;
    }

    /// <summary>
    /// A row's text field, or null when what it holds is not text. Emptied fields are left as a
    /// space or a full stop rather than cleared, and neither says anything.
    /// </summary>
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

    /// <summary>
    /// The asset's own fields only. Descending would reach FGameplayTag's TagName, which ends in
    /// "Name" and would win over the display field that actually names the thing.
    /// </summary>
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

    /// <summary>
    /// A dynamic item's own DisplayName is the internal asset name. The name the game shows sits
    /// in a "DisplayNameBy&lt;dimension&gt;" map instead - by hero for items that read differently
    /// per hero, by armour type for the rest. One distinct value across the map means the item
    /// has a single name; several means the caller has to choose a dimension first.
    /// </summary>
    private static string? MappedName(UObject asset)
    {
        foreach (var property in asset.Properties)
        {
            if (!property.Name.Text.StartsWith("DisplayName", StringComparison.OrdinalIgnoreCase)) continue;
            if (property.Tag?.GenericValue is not UScriptMap map) continue;

            var values = map.Properties
                .Select(pair => PropertyWalk.AsString(pair.Value?.GenericValue))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (values.Length == 1) return values[0];
        }

        return null;
    }

    /// <summary>Any texture or brush reference on the asset, preferring fields named as icons.</summary>
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

            var value = property.Tag?.GenericValue is FScriptStruct { StructType: IPropertyHolder brush }
                ? PropertyWalk.GetString(brush, "ResourceObject")
                : PropertyWalk.AsString(property.Tag?.GenericValue);

            if (value is null) continue;
            if (name.Contains("Icon", StringComparison.OrdinalIgnoreCase)) return value;
            fallback ??= value;
        }

        return fallback;
    }
}
