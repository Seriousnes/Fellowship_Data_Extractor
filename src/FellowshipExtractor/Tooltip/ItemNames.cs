using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The names a dynamic item can be shown under. Three fields hold them and none of them alone is
/// the answer:
///
/// <list type="bullet">
/// <item>DisplayNameByArmorType, on 225 of the 237 item definitions, is the name per armour type.
/// A helm reads as four different items, one per armour type, and the map is where all four are.
/// Its None key is the genuine name for the slots that have no armour type - neck, relic, ring,
/// weapon - and the codegen slug on the rest.</item>
/// <item>DisplayNameByHero, on the 11 legendaries, is the name per hero. A legendary has no single
/// name at all: the same ring is Ring of Boomtastic Explosions for one hero and Loop of the
/// Thundering Vortex for another.</item>
/// <item>The scalar DisplayName, which is a real name on exactly one item and the internal asset
/// name on the rest.</item>
/// </list>
///
/// A value that is the codegen slug rather than a name is dropped, because it is the asset's own
/// identifier leaking through and reads as Legendary_Ring_C_Haste_Haste on screen. Slugs are told
/// apart by shape: they are one run of word characters split by underscores or dots and carry no
/// space. Across the 237 definitions that splits the 785 distinct stored values into 193 slugs and
/// 592 names, and the names that survive are exactly the 589 distinct ones the locres ships plus
/// three the map repeats across dimensions.
/// </summary>
public sealed record ItemNaming
{
    /// <summary>The name per armour type, for the types that carry a real one.</summary>
    public required IReadOnlyDictionary<string, string> ByArmorType { get; init; }

    /// <summary>The name under the armour map's no-armour key, when it is real.</summary>
    public string? Unarmoured { get; init; }

    /// <summary>The name per hero codename, for the heroes that carry a real one.</summary>
    public required IReadOnlyDictionary<string, string> ByHero { get; init; }

    /// <summary>The item's own DisplayName, when it is a name rather than the asset's identifier.</summary>
    public string? Scalar { get; init; }

    /// <summary>
    /// The one name the armour map holds, when it holds only one. Two cases read this way and both
    /// mean the item is not named per armour type after all: a cloak, which every hero wears as
    /// cloth, and a weapon restricted to one hero, whose other armour keys hold the slug.
    /// </summary>
    public string? Unambiguous { get; private init; }

    /// <summary>The armour type <see cref="Unambiguous"/> is filed under, when it is not the no-armour key.</summary>
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

    /// <summary>
    /// What an item asset is named, or null when the asset is not an item. Mounts, visual
    /// collections and cosmetics name themselves through fields of their own - MountName,
    /// VariationName - and carry no ItemID, so gating on it keeps the slug filter off text that
    /// was never a slug to begin with.
    /// </summary>
    public static ItemNaming? Read(UObject asset)
    {
        if (PropertyWalk.Get(asset, ItemIdField) is null) return null;

        var byArmorType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byHero = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? unarmoured = null;

        foreach (var (key, value) in Entries(asset, ByArmorTypeField))
        {
            if (!IsRealName(value)) continue;

            // The armour map's keys arrive as the full enum name, so its no-armour key is told
            // from the four real types by name rather than by position.
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

    /// <summary>
    /// Whether a stored value is a name the game shows rather than the identifier the asset was
    /// generated under. Head_SetB_B_Crit and Wrists.SetA_B_Spirit are identifiers; Redeemer's
    /// Thorn-Crested Mask and Spidertreads are names.
    /// </summary>
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

    /// <summary>
    /// The armour type an enum name keys, without the table that decodes ordinals. The map's keys
    /// are spelled out rather than cooked down to numbers, so the four real types are the ones
    /// whose leaf is not the enum's own zero name.
    /// </summary>
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
