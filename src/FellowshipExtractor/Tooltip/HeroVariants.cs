using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Items shared across heroes carry per-hero values in maps keyed by a hero gameplay tag, so one
/// item id covers several differently named things. The name is what the tooltip shows and the
/// ability the item grants is what it says; both are read per hero.
/// </summary>
public static class HeroVariants
{
    public const string HeroTagPrefix = "CharacterID.Hero.";
    private const string DisplayNameByHero = "DisplayNameByHero";

    public static List<Variant> Collect(UObject asset, HeroNames names)
    {
        var byHero = new Dictionary<string, Variant>(StringComparer.OrdinalIgnoreCase);

        // A hero the item is allowed on is a variant even when no field differs for them, because
        // the ability it grants still might.
        foreach (var hero in AllowedHeroes(asset))
        {
            byHero[hero] = new Variant(hero) { HeroName = names.Shipped(hero) };
        }

        foreach (var property in asset.Properties)
        {
            if (!property.Name.Text.Equals(DisplayNameByHero, StringComparison.OrdinalIgnoreCase)) continue;
            if (property.Tag?.GenericValue is not UScriptMap map) continue;

            foreach (var (key, value) in map.Properties)
            {
                if (HeroOf(key) is not { } hero) continue;
                if (PropertyWalk.AsString(value?.GenericValue) is not { } text) continue;

                if (!byHero.TryGetValue(hero, out var variant))
                {
                    byHero[hero] = variant = new Variant(hero) { HeroName = names.Shipped(hero) };
                }

                variant.DisplayName = text;
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
