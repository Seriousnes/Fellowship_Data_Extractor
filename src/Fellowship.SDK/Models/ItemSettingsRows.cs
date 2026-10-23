using Fellowship.SDK.Models;

namespace Fellowship.SDK;

public sealed record ItemPowerPotential
{
    public double ItemLevelScoreFactor { get; init; }

    public double TemperScoreFactor { get; init; }

    public double ModSlotScoreFactor { get; init; }

    public double GemSocketScoreFactor { get; init; }

    public double WeaponAbilityScoreFactor { get; init; }

    public double RelicAbilityScoreFactor { get; init; }

    public double WeaponAttunementTreeScoreFactor { get; init; }

    public double NecklaceAttunementTreeScoreFactor { get; init; }

    public double SetBonusScoreFactor { get; init; }

    public double LegendaryScoreFactor { get; init; }

    public double AllocatedTalentPointPowerPotential { get; init; }

    public IReadOnlyDictionary<string, int> ScorePerGemType { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public int ScorePerAllocatedWeaponAttunement { get; init; }

    public int ScorePerAllocatedNecklaceAttunement { get; init; }
}

public sealed record ItemSettingsFile
{
    public int MaxItemLevel { get; init; }

    public int ItemLevelsPerUpgrade { get; init; }

    public IReadOnlyDictionary<string, string> SlotNames { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<int, double> GemSocketPowerMultipliers { get; init; } =
        new Dictionary<int, double>();

    public IReadOnlyDictionary<string, double> ItemGemPowers { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public ItemPowerPotential PowerPotential { get; init; } = new();

    public Curve? StatEnchantPower { get; init; }

    public double TemperDefaultStaminaMultiplier { get; init; }

    public IReadOnlyList<TemperStaminaStep> TemperStaminaMultiplierAtItemLevel { get; init; } = [];
}

public sealed record TemperStaminaStep(int ItemLevel, double TemperValue);
