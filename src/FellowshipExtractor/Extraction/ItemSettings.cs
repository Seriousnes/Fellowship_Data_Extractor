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
    private const string DefaultStaminaKey = "TemperDefaultStaminaMultiplier";
    private const string StaminaStepsKey = "TemperStaminaMultiplierAtItemLevel";
    private const string ItemLevelField = "ItemLevel";
    private const string TemperValueField = "TemperValue";

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
            WeaponAttunementTreeScoreFactor = Number(config, potential, "WeaponTraitTreeScoreFactor"),
            NecklaceAttunementTreeScoreFactor = Number(config, potential, "NecklaceTraitTreeScoreFactor"),
            SetBonusScoreFactor = Number(config, potential, "SetBonusScoreFactor"),
            LegendaryScoreFactor = Number(config, potential, "LegendaryScoreFactor"),
            AllocatedTalentPointPowerPotential = Number(config, potential, "AllocatedTalentPointPowerPotential"),
            ScorePerGemType = GemScores(
                config, mappings, List(config, potential, "ScorePerGemType"), gems.RarityByTier(rarities)),
            ScorePerAllocatedWeaponAttunement = (int)Number(config, potential, "ScorePerAllocatedWeaponTrait"),
            ScorePerAllocatedNecklaceAttunement = (int)Number(config, potential, "ScorePerAllocatedNecklaceTrait"),
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
            MaxItemLevel = config.Integer(MaxItemLevelKey),
            ItemLevelsPerUpgrade = config.Integer(LevelsPerUpgradeKey),
            SlotNames = slots.Vocabulary.ToDictionary(
                slot => slot.Type, slot => slot.Shipped, StringComparer.OrdinalIgnoreCase),
            GemSocketPowerMultipliers = ByTier(
                config, mappings, config.RequiredList(SocketMultipliersKey), SocketTierEnum, Number),
            ItemGemPowers = Slots(config, mappings, config.RequiredList(GemPowersKey), slots),
            PowerPotential = potentialSettings,
            TemperDefaultStaminaMultiplier = Number(
                config.Text(DefaultStaminaKey), DefaultStaminaKey, config.Path),
            TemperStaminaMultiplierAtItemLevel =
            [
                .. config.Each(StaminaStepsKey).Select(step => step as ConfigList
                    ?? throw new InvalidOperationException(
                        $"[{SettingsSection}] in {config.Path} writes a {StaminaStepsKey} entry as a scalar."))
                    .Select(step => new TemperStaminaStep(
                        (int)Number(StepField(config, step, ItemLevelField), $"{StaminaStepsKey}.{ItemLevelField}", config.Path),
                        Number(StepField(config, step, TemperValueField), $"{StaminaStepsKey}.{TemperValueField}", config.Path)))
                    .OrderBy(step => step.ItemLevel),
            ],
        };
    }

    private static string StepField(ConfigIni config, ConfigList step, string field) =>
        step.Field(field)?.Text
        ?? throw new InvalidOperationException(
            $"[{SettingsSection}] in {config.Path} writes a {StaminaStepsKey} entry without a {field}.");

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

    private static double Number(string text, string what, string path) =>
        double.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"{what} in {path} reads '{text}', not a number.");

    private static int Integer(string text, string what, string path) =>
        int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException(
                $"{what} in {path} reads '{text}', not a whole number.");

    private static Dictionary<int, TValue> ByTier<TValue>(
        ConfigIni config,
        TypeMappings mappings,
        ConfigList map,
        string enumName,
        Func<string, string, string, TValue> read)
    {
        var members = EnumOrdinals.Members(mappings, enumName);
        var byTier = new Dictionary<int, TValue>();

        foreach (var (key, value) in map.Pairs())
        {
            if (!members.Values.Contains(key, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} keys a map by '{key}', and {enumName} does not " +
                    "declare it. The tier it names cannot be read back.");
            }

            byTier[Tier(key, enumName)] = read(value.Text, $"{enumName}::{key}", config.Path);
        }

        if (byTier.Count == 0)
        {
            throw new InvalidOperationException(
                $"[{SettingsSection}] in {config.Path} writes a map keyed by {enumName} that reads back with no " +
                "entries at all.");
        }

        return byTier;
    }

    private static Dictionary<string, int> GemScores(
        ConfigIni config, TypeMappings mappings, ConfigList map, IReadOnlyDictionary<int, string> rarityByTier)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tier, score) in ByTier(config, mappings, map, GemTierEnum, Integer))
        {
            if (!rarityByTier.TryGetValue(tier, out var rarity))
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} scores gem tier {tier}, and no shipped gem is at " +
                    "that tier, so the score belongs to no rarity a gem prints.");
            }

            scores[rarity] = score;
        }

        return scores;
    }

    private static int Tier(string member, string enumName)
    {
        var digits = member.Length;
        while (digits > 0 && char.IsAsciiDigit(member[digits - 1])) digits--;

        if (digits == member.Length) return 0;

        return int.TryParse(member[digits..], CultureInfo.InvariantCulture, out var tier)
            ? tier
            : throw new InvalidOperationException(
                $"{enumName} declares '{member}', and its trailing digits are not the tier it names.");
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
                    $"[{SettingsSection}] in {config.Path} keys {GemPowersKey} by '{key}', and {ItemTypeEnum} " +
                    "does not declare it.");
            }

            if (slots.Find(key) is not { } slot)
            {
                throw new InvalidOperationException(
                    $"[{SettingsSection}] in {config.Path} keys {GemPowersKey} by '{key}', and the shipped " +
                    "slot names do not cover it, so the entry would belong to a slot no item is in.");
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
