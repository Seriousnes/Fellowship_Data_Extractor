using System.Globalization;

using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;

namespace FellowshipExtractor.Extraction;

public sealed class ItemSettings
{
    public const string ConfigPath = "fellowship/Config/DefaultGame.ini";

    public const string SettingsSection = "/Script/fellowship.CRItemSettings";

    private const string PowerPotentialKey = "HeroItemPowerPotentialSettings";
    private const string SocketMultipliersKey = "GemSocketPowerMultipliers";
    private const string GemPowersKey = "ItemGemPowers";
    private const string MaxItemLevelKey = "MaxItemLevel";
    private const string LevelsPerUpgradeKey = "ItemLevelsPerUpgrade";

    private const string GemTierEnum = "ECRGemTier";
    private const string SocketTierEnum = "ECRGemSocketTier";
    private const string ItemTypeEnum = "ECRHeroItemType";

    public ItemSettingsFile Values { get; }

    public int FactorCount { get; }

    public ItemSettings(
        AbstractFileProvider provider, ItemSlotNames slots, GemDefinitions gems, RarityNames rarities)
    {
        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException(
                "No mappings are loaded, so the enum members the item settings key their maps by cannot be " +
                "checked against the tables that declare them.");

        var config = ConfigIni.Read(provider, ConfigPath, SettingsSection);
        var potential = config.RequiredList(PowerPotentialKey);

        var potentialSettings = new ItemPowerPotential
        {
            ItemLevelScoreFactor = Number(config, potential, "ItemLevelScoreFactor"),
            TemperScoreFactor = Number(config, potential, "TemperScoreFactor"),
            ModSlotScoreFactor = Number(config, potential, "ModSlotScoreFactor"),
            GemSocketScoreFactor = Number(config, potential, "GemSocketScoreFactor"),
            WeaponAbilityScoreFactor = Number(config, potential, "WeaponAbilityScoreFactor"),
            RelicAbilityScoreFactor = Number(config, potential, "RelicAbilityScoreFactor"),
            WeaponTraitTreeScoreFactor = Number(config, potential, "WeaponTraitTreeScoreFactor"),
            NecklaceTraitTreeScoreFactor = Number(config, potential, "NecklaceTraitTreeScoreFactor"),
            SetBonusScoreFactor = Number(config, potential, "SetBonusScoreFactor"),
            LegendaryScoreFactor = Number(config, potential, "LegendaryScoreFactor"),
            AllocatedTalentPointPowerPotential = Number(config, potential, "AllocatedTalentPointPowerPotential"),
            ScorePerGemType = GemRungs(
                config, mappings, List(config, potential, "ScorePerGemType"), gems.RungByTier(rarities)),
            ScorePerAllocatedWeaponTrait = (int)Number(config, potential, "ScorePerAllocatedWeaponTrait"),
            ScorePerAllocatedNecklaceTrait = (int)Number(config, potential, "ScorePerAllocatedNecklaceTrait"),
        };

        FactorCount = potential.Entries.Count;

        var declared = mappings.Types.TryGetValue("CRHeroItemPowerPotentialSettings", out var structure)
            ? structure.Properties.Count
            : 0;

        if (declared > 0 && FactorCount != declared)
        {
            throw new InvalidOperationException(
                $"[{SettingsSection}] in {ConfigPath} sets {FactorCount} of the {declared} fields the mappings " +
                $"declare on CRHeroItemPowerPotentialSettings. The rest would read back as C++ constructor " +
                "defaults that no shipped file states.");
        }

        Values = new ItemSettingsFile
        {
            MaxItemLevel = config.Whole(MaxItemLevelKey),
            ItemLevelsPerUpgrade = config.Whole(LevelsPerUpgradeKey),
            SlotNames = slots.Vocabulary.ToDictionary(
                slot => slot.Type, slot => slot.Shipped, StringComparer.OrdinalIgnoreCase),
            GemSocketPowerMultipliers = Rungs(
                config, mappings, config.RequiredList(SocketMultipliersKey), SocketTierEnum, Number),
            ItemGemPowers = Slots(config, mappings, config.RequiredList(GemPowersKey), slots),
            PowerPotential = potentialSettings,
        };
    }

    private static ConfigValue Field(ConfigIni config, ConfigList block, string field) =>
        block.Field(field)
        ?? throw new InvalidOperationException(
            $"[{SettingsSection}] in {config.Path} writes {PowerPotentialKey} without a {field}, so that factor " +
            "would come from a C++ constructor default that no shipped file states.");

    private static double Number(ConfigIni config, ConfigList block, string field) =>
        Number(Field(config, block, field).Text, $"{PowerPotentialKey}.{field}", config.Path);

    private static ConfigList List(ConfigIni config, ConfigList block, string field) =>
        Field(config, block, field) as ConfigList
        ?? throw new InvalidOperationException(
            $"[{SettingsSection}] in {config.Path} writes {PowerPotentialKey}.{field} as a scalar, not the " +
            "parenthesised literal a map is written as.");

    private static double Number(string written, string what, string path) =>
        double.TryParse(written, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"{what} in {path} reads '{written}', which is not a number.");

    private static int Whole(string written, string what, string path) =>
        int.TryParse(written, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException(
                $"{what} in {path} reads '{written}', which is not a whole number.");

    private static Dictionary<int, TValue> Rungs<TValue>(
        ConfigIni config,
        TypeMappings mappings,
        ConfigList map,
        string enumName,
        Func<string, string, string, TValue> read)
    {
        var members = EnumOrdinals.Members(mappings, enumName);
        var rungs = new Dictionary<int, TValue>();

        foreach (var (key, value) in map.Pairs())
        {
            if (!members.Values.Contains(key, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} keys a map by '{key}', which {enumName} does not " +
                    "declare. The rung it names cannot be read back.");
            }

            rungs[Rung(key, enumName)] = read(value.Text, $"{enumName}::{key}", config.Path);
        }

        if (rungs.Count == 0)
        {
            throw new InvalidOperationException(
                $"[{SettingsSection}] in {config.Path} writes a map keyed by {enumName} that reads back with no " +
                "entries at all.");
        }

        return rungs;
    }

    private static Dictionary<string, int> GemRungs(
        ConfigIni config, TypeMappings mappings, ConfigList map, IReadOnlyDictionary<int, string> rungByTier)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tier, score) in Rungs(config, mappings, map, GemTierEnum, Whole))
        {
            if (!rungByTier.TryGetValue(tier, out var rung))
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} scores gem tier {tier}, which no shipped gem sits at, " +
                    "so the score belongs to no rung a gem prints.");
            }

            scores[rung] = score;
        }

        return scores;
    }

    private static int Rung(string member, string enumName)
    {
        var digits = member.Length;
        while (digits > 0 && char.IsAsciiDigit(member[digits - 1])) digits--;

        if (digits == member.Length) return 0;

        return int.TryParse(member[digits..], CultureInfo.InvariantCulture, out var rung)
            ? rung
            : throw new InvalidOperationException(
                $"{enumName} declares '{member}', whose trailing digits are not the rung it names.");
    }

    private static Dictionary<string, double> Slots(
        ConfigIni config, TypeMappings mappings, ConfigList map, ItemSlotNames slots)
    {
        var members = EnumOrdinals.Members(mappings, ItemTypeEnum);
        var powers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in map.Pairs())
        {
            if (!members.Values.Contains(key, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} keys {GemPowersKey} by '{key}', which {ItemTypeEnum} " +
                    "does not declare.");
            }

            if (slots.Named(key) is not { } slot)
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} keys {GemPowersKey} by '{key}', which the shipped slot " +
                    "names do not cover, so the entry would be filed under a slot no item is in.");
            }

            powers[slot.Shipped] = Number(value.Text, $"{GemPowersKey}.{key}", config.Path);
        }

        if (powers.Count == 0)
        {
            throw new InvalidOperationException(
                $"[{SettingsSection}] in {config.Path} writes {GemPowersKey} with no entries at all.");
        }

        return powers;
    }
}
