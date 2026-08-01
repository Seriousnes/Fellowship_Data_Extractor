using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;

namespace FellowshipExtractor.Tooltip;

/// <summary>One hero's version of an item that reads differently per hero.</summary>
public sealed record HeroVariant(string Hero, string? HeroName)
{
    public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);

    /// <summary>The variant's own display name, when it carries one.</summary>
    public string? DisplayName => Fields.GetValueOrDefault("DisplayName");

    /// <summary>The ability this item grants this hero. Different heroes get different ones.</summary>
    public Augmentation? GrantedAbility { get; set; }
}

/// <summary>
/// Items shared across heroes carry per-hero values in maps keyed by a hero gameplay tag, so one
/// item id covers several differently named things. Any such map is picked up by shape rather
/// than by naming the fields, so a new per-hero field appears without a code change.
/// </summary>
public static class HeroVariants
{
    public const string HeroTagPrefix = "CharacterID.Hero.";
    private const string ByHeroSuffix = "ByHero";

    public static List<HeroVariant> Collect(UObject asset, HeroNames names)
    {
        var byHero = new Dictionary<string, HeroVariant>(StringComparer.OrdinalIgnoreCase);

        // A hero the item is allowed on is a variant even when no field differs for them, because
        // the ability it grants still might.
        foreach (var hero in AllowedHeroes(asset))
        {
            byHero[hero] = new HeroVariant(hero, names.Shipped(hero));
        }

        foreach (var property in asset.Properties)
        {
            if (property.Tag?.GenericValue is not UScriptMap map) continue;

            // "DisplayNameByHero" describes a DisplayName; keep the meaningful half.
            var field = property.Name.Text.EndsWith(ByHeroSuffix, StringComparison.OrdinalIgnoreCase)
                ? property.Name.Text[..^ByHeroSuffix.Length]
                : property.Name.Text;

            foreach (var (key, value) in map.Properties)
            {
                if (HeroOf(key) is not { } hero) continue;
                if (PropertyWalk.AsString(value?.GenericValue) is not { } text) continue;

                if (!byHero.TryGetValue(hero, out var variant))
                {
                    byHero[hero] = variant = new HeroVariant(hero, names.Shipped(hero));
                }
                variant.Fields[field] = text;
            }
        }

        return [.. byHero.Values.OrderBy(v => v.HeroName ?? v.Hero, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The hero codenames an item is allowed on.</summary>
    private static IEnumerable<string> AllowedHeroes(UObject asset) =>
        PropertyWalk.Get(asset, "AllowedHeroes")?.GenericValue is UScriptArray allowed
            ? allowed.Properties.Select(HeroOf).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
            : [];

    /// <summary>The hero codename a gameplay tag names, or null if the tag is not a hero.</summary>
    private static string? HeroOf(FPropertyTagType? tag)
    {
        var text = PropertyWalk.AsString(tag?.GenericValue);
        return text is not null && text.StartsWith(HeroTagPrefix, StringComparison.OrdinalIgnoreCase)
            ? HeroNames.LastSegment(text)
            : null;
    }
}
