using System.Globalization;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

public readonly record struct WalkedProperty(string Path, string Name, string? StructType, FPropertyTagType? Tag)
{
    public object? Value => Tag?.GenericValue;
}

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

    public static string? GetText(IPropertyHolder holder, string name)
    {
        if (GetStruct(holder, name) is { } formatted && GetString(formatted, "FormatString") is { } format)
        {
            return format;
        }

        return GetString(holder, name);
    }

    public static string? AsString(object? value) => value switch
    {
        null => null,
        FName name => name.IsNone ? null : name.Text,
        FGameplayTag tag => tag.TagName.IsNone ? null : tag.TagName.Text,
        FSoftObjectPath soft => soft.AssetPathName.IsNone || soft.ToString() is not { Length: > 0 } path
            ? null
            : path,
        FPackageIndex index => index.IsNull ? null : index.ResolvedObject?.GetPathName() ?? index.Name,
        string s => s.Length == 0 ? null : s,
        FScriptStruct scriptStruct => AsString(scriptStruct.StructType),
        IPropertyHolder holder when Get(holder, "TagName") is { } tagName => AsString(tagName.GenericValue),
        _ => value.ToString() is { Length: > 0 } text && text != "None" ? text : null,
    };

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

    public static int? AsOrdinal(object? value)
    {
        if (AsNumber(value) is { } number) return (int)number;
        if (AsString(value) is not { } text) return null;
        if (int.TryParse(text, out var parsed)) return parsed;

        return EnumOrdinals.For(text) is { } ordinal and >= int.MinValue and <= int.MaxValue
            ? (int)ordinal
            : null;
    }

    public static double? AsNumber(object? value) => value switch
    {
        float f => double.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
        double d => d,
        byte or sbyte or short or ushort or int or uint or long => Convert.ToDouble(value),
        _ => null,
    };
}
