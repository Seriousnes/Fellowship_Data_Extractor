using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record ItemNaming
{
    public required IReadOnlyDictionary<string, string> ByArmorType { get; init; }

    public string? Unarmoured { get; init; }

    public required IReadOnlyDictionary<string, string> ByHero { get; init; }

    public string? Scalar { get; init; }

    public string? Unambiguous { get; private init; }

    public string? UnambiguousArmorType { get; private init; }

    internal ItemNaming Settled()
    {
        var distinct = ByArmorType.Values
            .Append(Unarmoured)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinct.Length != 1) return this;

        return this with
        {
            Unambiguous = distinct[0],
            UnambiguousArmorType = ByArmorType
                .FirstOrDefault(pair => string.Equals(pair.Value, distinct[0], StringComparison.Ordinal)).Key,
        };
    }
}

public static class ItemNames
{
    private const string ItemIdField = "ItemID";
    private const string ByArmorTypeField = "DisplayNameByArmorType";
    private const string ByHeroField = "DisplayNameByHero";
    private const string ScalarField = "DisplayName";

    public static ItemNaming? Read(UObject asset)
    {
        if (PropertyWalk.Get(asset, ItemIdField) is null) return null;

        var byArmorType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byHero = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? unarmoured = null;

        foreach (var (key, value) in Entries(asset, ByArmorTypeField))
        {
            if (!IsRealName(value)) continue;

            var label = ArmorTypeLabel(key);
            if (label is null) unarmoured ??= value; else byArmorType.TryAdd(label, value);
        }

        foreach (var (key, value) in Entries(asset, ByHeroField))
        {
            if (!IsRealName(value)) continue;
            if (!key.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            byHero.TryAdd(HeroNames.LastSegment(key), value);
        }

        var scalar = PropertyWalk.GetString(asset, ScalarField);

        return new ItemNaming
        {
            ByArmorType = byArmorType,
            Unarmoured = unarmoured,
            ByHero = byHero,
            Scalar = IsRealName(scalar) ? scalar : null,
        }.Settled();
    }

    public static bool IsRealName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var separated = false;

        foreach (var character in value)
        {
            if (character is '_' or '.') { separated = true; continue; }
            if (!char.IsAsciiLetterOrDigit(character)) return true;
        }

        return !separated;
    }

    private static string? ArmorTypeLabel(string key)
    {
        var separator = key.LastIndexOf("::", StringComparison.Ordinal);
        var leaf = separator < 0 ? key : key[(separator + 2)..];
        return leaf.Equals("None", StringComparison.OrdinalIgnoreCase) ? null : leaf;
    }

    private static IEnumerable<(string Key, string Value)> Entries(UObject asset, string field)
    {
        if (PropertyWalk.Get(asset, field)?.GenericValue is not UScriptMap map) yield break;

        foreach (var (key, value) in map.Properties)
        {
            if (PropertyWalk.AsString(key?.GenericValue) is not { } name) continue;
            if (PropertyWalk.AsString(value?.GenericValue) is not { } text) continue;

            yield return (name, text);
        }
    }
}
