using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

/// <summary>One property reached while walking an object, with the dotted path that got there.</summary>
public readonly record struct WalkedProperty(string Path, string Name, string? StructType, FPropertyTagType? Tag)
{
    public object? Value => Tag?.GenericValue;
}

/// <summary>
/// Depth-first walk over an object's properties, descending through structs, arrays, sets and
/// maps. Everything the tooltip needs - curve bindings, display text, icons - is found by
/// pattern rather than by knowing each asset type's layout, so one walk serves every table.
/// </summary>
public static class PropertyWalk
{
    private const int MaxDepth = 12;

    public static IEnumerable<WalkedProperty> Walk(IPropertyHolder holder) => Walk(holder, "", 0);

    private static IEnumerable<WalkedProperty> Walk(IPropertyHolder holder, string prefix, int depth)
    {
        if (depth > MaxDepth) yield break;

        foreach (var property in holder.Properties)
        {
            var name = property.Name.Text;
            var path = prefix.Length == 0 ? name : $"{prefix}.{name}";
            var structType = property.TagData?.StructType;

            yield return new WalkedProperty(path, name, structType, property.Tag);

            foreach (var nested in Descend(property.Tag, path, name, depth))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Everything reachable below a property. Container elements are yielded in their own right
    /// as well as descended into, because an array of asset references has nothing underneath it
    /// and would otherwise never be seen.
    /// </summary>
    private static IEnumerable<WalkedProperty> Descend(
        FPropertyTagType? tag, string path, string name, int depth)
    {
        switch (tag?.GenericValue)
        {
            case UScriptArray array:
                for (var i = 0; i < array.Properties.Count; i++)
                {
                    var elementPath = $"{path}[{i}]";
                    yield return new WalkedProperty(elementPath, name, null, array.Properties[i]);
                    foreach (var nested in Descend(array.Properties[i], elementPath, name, depth)) yield return nested;
                }
                yield break;

            case UScriptMap map:
                var index = 0;
                foreach (var pair in map.Properties)
                {
                    var keyPath = $"{path}[{index}].key";
                    var valuePath = $"{path}[{index}].value";
                    yield return new WalkedProperty(keyPath, name, null, pair.Key);
                    foreach (var nested in Descend(pair.Key, keyPath, name, depth)) yield return nested;
                    yield return new WalkedProperty(valuePath, name, null, pair.Value);
                    foreach (var nested in Descend(pair.Value, valuePath, name, depth)) yield return nested;
                    index++;
                }
                yield break;

            case FScriptStruct { StructType: IPropertyHolder inner }:
                foreach (var nested in Walk(inner, path, depth + 1)) yield return nested;
                yield break;

            case IPropertyHolder holder:
                foreach (var nested in Walk(holder, path, depth + 1)) yield return nested;
                yield break;
        }
    }

    /// <summary>A named property of a holder, or null when it is absent.</summary>
    public static FPropertyTagType? Get(IPropertyHolder holder, string name) =>
        holder.Properties.FirstOrDefault(p => p.Name.Text.Equals(name, StringComparison.OrdinalIgnoreCase))?.Tag;

    public static IPropertyHolder? GetStruct(IPropertyHolder holder, string name) =>
        Get(holder, name)?.GenericValue switch
        {
            FScriptStruct { StructType: IPropertyHolder inner } => inner,
            IPropertyHolder direct => direct,
            _ => null,
        };

    public static string? GetString(IPropertyHolder holder, string name) => AsString(Get(holder, name)?.GenericValue);

    public static string? AsString(object? value) => value switch
    {
        null => null,
        FName name => name.IsNone ? null : name.Text,
        FGameplayTag tag => tag.TagName.IsNone ? null : tag.TagName.Text,
        FSoftObjectPath soft => soft.AssetPathName.IsNone ? null : soft.ToString(),
        FPackageIndex index => index.IsNull ? null : index.ResolvedObject?.GetPathName() ?? index.Name,
        string s => s.Length == 0 ? null : s,
        // Native structs stringify with their type appended, so unwrap before falling back.
        FScriptStruct scriptStruct => AsString(scriptStruct.StructType),
        // A gameplay tag only reads as the native FGameplayTag when CUE4Parse recognises the
        // struct; from mappings alone it arrives as a plain holder wrapping its TagName.
        IPropertyHolder holder when Get(holder, "TagName") is { } tagName => AsString(tagName.GenericValue),
        _ => value.ToString() is { Length: > 0 } text && text != "None" ? text : null,
    };

    /// <summary>
    /// The tags in a gameplay tag container. CUE4Parse reads it natively when the mappings name
    /// the struct, and as a plain holder otherwise, so both shapes are handled.
    /// </summary>
    public static string[]? AsTags(object? value)
    {
        switch (value)
        {
            case FScriptStruct scriptStruct:
                return AsTags(scriptStruct.StructType);
            case FGameplayTagContainer container:
                return [.. container.GameplayTags.Select(t => t.TagName.Text)];
            case IPropertyHolder holder when Get(holder, "GameplayTags")?.GenericValue is UScriptArray array:
                return [.. array.Properties.Select(p => AsString(p?.GenericValue)).OfType<string>()];
            default:
                return null;
        }
    }

    public static string[]? GetTags(IPropertyHolder holder, string name) =>
        AsTags(Get(holder, name)?.GenericValue);

    public static double? AsNumber(object? value) => value switch
    {
        float f => f,
        double d => d,
        byte or sbyte or short or ushort or int or uint or long => Convert.ToDouble(value),
        _ => null,
    };
}
