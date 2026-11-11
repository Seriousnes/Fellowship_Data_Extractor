using Fellowship.SDK.Documents;

namespace Fellowship.SDK.Calculations;

/// <summary>Whether a reading of a document is the one a hero reads.</summary>
public static class EntityStore
{
    public static bool Reads(EntityDocument document, HeroEntry hero) =>
        document switch
        {
            ItemDocument item => item.Heroes is { } heroes
                ? heroes.Contains(ParseEnum<Hero>(hero.Name))
                : (item.ArmorType is null || item.ArmorType == ParseEnumOrNull<ArmorType>(hero.ArmorType)) &&
                  (item.PrimaryStat is null || item.PrimaryStat == ParseEnum<PrimaryStat>(hero.PrimaryStat)),
            TalentSlotDocument slot => slot.Hero == ParseEnum<Hero>(hero.Name),
            EnchantDocument enchant => ReadsStat(enchant.PrimaryStat, hero),
            EffectDocument effect => ReadsStat(effect.PrimaryStat, hero),
            _ => true,
        };

    private static bool ReadsStat(PrimaryStat? stat, HeroEntry hero) =>
        stat is null || stat == ParseEnum<PrimaryStat>(hero.PrimaryStat);

    private static T ParseEnum<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, out var value) ? value : throw new FormatException($"'{name}' names no {typeof(T).Name}.");

    private static T? ParseEnumOrNull<T>(string? name) where T : struct, Enum =>
        name is null ? null : ParseEnum<T>(name);
}
