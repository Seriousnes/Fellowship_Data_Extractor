using System.Globalization;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The settings an enemy's actions read: the calculation presets, the threat values, the AI's global cooldowns, the party
/// scaling and the damage and scale rows of the dungeon modifiers.
/// </summary>
public sealed class EnemySettings
{
    private const string ThreatSection = "/Script/fellowship.CRThreatSettings";
    private const string ThreatStructure = "CRThreatSettings";
    private const string GlobalsSection = "/Script/fellowship.CRAbilitySystemGlobals";

    private const string HealThreatRangeKey = "HealThreatRange";
    private const string SecondaryThreatDistanceKey = "GlobalSecondaryThreatMaxDistanceKey";
    private const string CCPaddingKey = "GlobalCCAbilityPadding";
    private const string InterruptPaddingKey = "GlobalInterruptAbilityPadding";
    private const string LockoutKey = "DefaultAILockoutDurationForCCInterrupts";
    private const string HealthScale4pKey = "HealthScale4p";
    private const string HealthScale5pKey = "HealthScale5p";

    private const string DefaultCCCooldownRow = "Global.DefaultCCCooldown";
    private const string NonTankThreatRow = "Hero.Spirit.Buff.NonTankThreatGenerationScaler";

    private const string HolyTrinityNerf = "GA_DungeonModifier_HolyTrinityNerf";
    private const string CurveField = "Curve";

    /// <summary>The damage and scale rows of <c>CT_DungeonModifiers</c> an enemy's damage and health read.</summary>
    public static readonly IReadOnlyList<string> DungeonModifierRows =
    [
        "Challenge.EnemyBoost.HealthScaler",
        "Challenge.EnemyBoost.DamageScaler",
        "Ultimatum.BossBoost.HealthScaler",
        "Ultimatum.BossBoost.DamageScaler",
        "EmpoweredMinions.EnemyBuff.OutgoingDamageScale",
        "StormShield.BaseDamage",
        "MeteorRain.Meteor.FlatDamage",
        "AnomalousOrbs.Orb.Detonate.Damage",
        "AbilityUnlock.Level",
        "AbilityUnlock.Pinnacle.Hard",
        "AbilityUnlock.Pinnacle.Ultimate",
    ];

    public IReadOnlyDictionary<string, EffectCalculationPresetEntry> Presets { get; }

    public ThreatSettings Threat { get; }

    public AiGlobals AiGlobals { get; }

    public PartyScaling PartyScaling { get; }

    public IReadOnlyDictionary<string, double> DungeonModifiers { get; }

    public EnemySettings(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ConstantsTable constants,
        EffectCalculationPresets presets)
    {
        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException(
                "No mappings are loaded, so the threat settings cannot be checked against the fields they declare.");

        Presets = presets.Rows;

        var threat = ConfigIni.Read(provider, ItemSettings.ConfigPath, ThreatSection);
        var globals = ConfigIni.Read(provider, ItemSettings.ConfigPath, GlobalsSection);

        var declared = mappings.Types.TryGetValue(ThreatStructure, out var structure)
            ? structure.Properties.Values.Select(property => property.Name).ToArray()
            : throw new InvalidOperationException($"The mappings declare no {ThreatStructure}.");

        var unset = declared.Where(field => !threat.Keys.Contains(field, StringComparer.OrdinalIgnoreCase)).ToArray();

        if (unset.Length > 0)
        {
            throw new InvalidOperationException(
                $"[{ThreatSection}] in {ItemSettings.ConfigPath} sets none of {string.Join(", ", unset)}. Each would " +
                "read back as a C++ constructor default that no shipped file states.");
        }

        Threat = new ThreatSettings
        {
            ThreatRelevancyPercentage = Number(threat, nameof(ThreatSettings.ThreatRelevancyPercentage)),
            ThreatSurpassPercentageMelee = Number(threat, nameof(ThreatSettings.ThreatSurpassPercentageMelee)),
            ThreatSurpassPercentageRange = Number(threat, nameof(ThreatSettings.ThreatSurpassPercentageRange)),
            ThreatSurpassEnabledForTanks = Boolean(threat, nameof(ThreatSettings.ThreatSurpassEnabledForTanks)),
            AggroMeleeRange = Number(threat, nameof(ThreatSettings.AggroMeleeRange)),
            InitiationThreatMultiplier = Number(threat, nameof(ThreatSettings.InitiationThreatMultiplier)),
            ForcedTargetExtraThreatBuffer = Number(threat, nameof(ThreatSettings.ForcedTargetExtraThreatBuffer)),
            ThreatDecayTickTime = Number(threat, nameof(ThreatSettings.ThreatDecayTickTime)),
            ThreatDecayMultiplierPerTick = Number(threat, nameof(ThreatSettings.ThreatDecayMultiplierPerTick)),
            HealThreatRange = Number(globals, HealThreatRangeKey),
            SecondaryThreatMaxDistance = Constant(constants, Named(globals, SecondaryThreatDistanceKey)),
            NonTankThreatGenerationScaler = Constant(constants, NonTankThreatRow),
        };

        AiGlobals = new AiGlobals
        {
            DefaultCCCooldown = Constant(constants, DefaultCCCooldownRow),
            CCAbilityPadding = Constant(constants, Named(globals, CCPaddingKey)),
            InterruptAbilityPadding = Constant(constants, Named(globals, InterruptPaddingKey)),
            DefaultAILockoutDurationForCCInterrupts = Number(globals, LockoutKey),
        };

        PartyScaling = new PartyScaling
        {
            HealthScale4p = Number(globals, HealthScale4pKey),
            HealthScale5p = Number(globals, HealthScale5pKey),
            HolyTrinityNerf = Scalables(provider, assets, constants, HolyTrinityNerf),
        };

        var modifiers = CurveRows.Load(provider, DungeonScoring.ModifierTable, "the damage and scale of the dungeon modifiers");
        var rows = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var name in DungeonModifierRows)
        {
            var row = modifiers.RowMap.FirstOrDefault(entry => entry.Key.Text.Equals(name, StringComparison.Ordinal)).Value
                ?? throw new InvalidOperationException($"{DungeonScoring.ModifierTable} has no {name} row.");

            rows[name] = CurveRows.Keys(row) switch
            {
                [var only] => only.Value,
                var keys => throw new InvalidOperationException(
                    $"{DungeonScoring.ModifierTable}'s {name} row has {keys.Count} keys, not the one value it is read as."),
            };
        }

        DungeonModifiers = rows;
    }

    private static double Number(ConfigIni config, string key) =>
        double.TryParse(config.Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException(
                $"[{config.Section}] in {config.Path} sets {key} to '{config.Text(key)}', not a number.");

    private static bool Boolean(ConfigIni config, string key) =>
        bool.TryParse(config.Text(key), out var value)
            ? value
            : throw new InvalidOperationException(
                $"[{config.Section}] in {config.Path} sets {key} to '{config.Text(key)}', not a bool.");

    /// <summary>The name a key holds, without the quotes the ini writes it in.</summary>
    private static string Named(ConfigIni config, string key) => config.Text(key).Trim('"');

    private static double Constant(ConstantsTable constants, string row) =>
        constants.Lookup(row).Record?.Value
        ?? throw new InvalidOperationException($"No constants table has the row {row}.");

    private static Dictionary<string, double> Scalables(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, ConstantsTable constants, string assetName)
    {
        var asset = assets.FirstOrDefault(entry => entry.AssetName.Text.Equals(assetName, StringComparison.OrdinalIgnoreCase));

        if (asset.ObjectPath is not { Length: > 0 } path || ClassDefaults.Load(path, provider) is not { } cdo)
        {
            throw new InvalidOperationException($"No container has {assetName}.");
        }

        var values = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var property in cdo.Properties)
        {
            if (property.Tag?.GenericValue is not FScriptStruct { StructType: IPropertyHolder scalable }) continue;
            if (PropertyWalk.GetStruct(scalable, CurveField) is null) continue;

            var (value, row) = MagnitudeCalculations.Read(scalable, constants);

            if (row is null) continue;

            values[row] = value ?? throw new InvalidOperationException(
                $"{assetName}'s {property.Name.Text} reads the row {row}, which no constants table has.");
        }

        return values;
    }
}
