using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public sealed record CreatureAttributes
{
    public double? Health { get; init; }

    public double? Armor { get; init; }

    public double? Resist { get; init; }

    public int? KillScore { get; init; }

    public int? SpiritPoints { get; init; }
}

public sealed class CreatureStats
{
    public const string BaseTable = "CT_CharacterBaseAttributes";
    public const string ScalingTable = "CT_CharacterScalingBaseAttributes";

    public const string DefaultNamespace = "Default";

    private const string BaseHealth = "BasicAttributeSet.BaseHealth";
    private const string HealthMultiplier = "Custom.DefaultBaseHealthMultiplier";
    private const string Armor = "BasicAttributeSet.Armor";
    private const string Resist = "BasicAttributeSet.Resist";
    private const string KillScore = "CRNpcAttributeSet.KillScore";
    private const string SpiritPoints = "CRNpcAttributeSet.SpiritPointValue";
    private const string HealthScale = "BasicAttributeSet.DifficultyHealthScale";
    private const string DamageScale = "BasicAttributeSet.DifficultyDamageScale";

    private readonly Dictionary<string, double> _base = new(StringComparer.OrdinalIgnoreCase);

    private readonly double _defaultBaseHealth;

    public DifficultyScaling Scaling { get; }

    public int Namespaces { get; }

    public int BothHealthRows { get; }

    private readonly HashSet<string> _healthless = new(StringComparer.OrdinalIgnoreCase);

    public int Healthless => _healthless.Count;

    public CreatureStats(AbstractFileProvider provider)
    {
        var attributes = CurveRows.Load(
            provider, BaseTable, "how much health, armour, resist, kill score and spirit points a creature has");

        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rowName, row) in attributes.RowMap)
        {
            if (CurveRows.First(row) is not { } value) continue;

            _base[rowName.Text] = value;

            var namespaced = rowName.Text.IndexOf('.');
            if (namespaced > 0) namespaces.Add(rowName.Text[..namespaced]);
        }

        Namespaces = namespaces.Count;

        BothHealthRows = namespaces.Count(scope =>
            _base.ContainsKey($"{scope}.{BaseHealth}") && _base.ContainsKey($"{scope}.{HealthMultiplier}"));

        _defaultBaseHealth = _base.TryGetValue($"{DefaultNamespace}.{BaseHealth}", out var shipped)
            ? shipped
            : throw new InvalidOperationException(
                $"{BaseTable} carries no {DefaultNamespace}.{BaseHealth} row, so every creature that states only a " +
                "health multiplier would multiply a number nothing states.");

        var scaling = CurveRows.Load(
            provider, ScalingTable, "how far a creature's health and damage climb with difficulty");

        Scaling = new DifficultyScaling
        {
            Health = Ladder(scaling, HealthScale),
            Damage = Ladder(scaling, DamageScale),
        };
    }

    private static IReadOnlyDictionary<int, double> Ladder(UCurveTable table, string attribute)
    {
        var wanted = $"{DefaultNamespace}.{attribute}";

        var row = table.RowMap.FirstOrDefault(entry =>
            entry.Key.Text.Equals(wanted, StringComparison.OrdinalIgnoreCase)).Value
            ?? throw new InvalidOperationException(
                $"{ScalingTable} carries no {wanted} row, so a creature's reading at a difficulty other than the " +
                "first has no curve behind it.");

        var steps = CurveRows.Keys(row);

        if (steps.Count == 0)
        {
            throw new InvalidOperationException(
                $"{ScalingTable}'s {wanted} row read back with no keys, so every difficulty would scale the same.");
        }

        var ladder = new Dictionary<int, double>(steps.Count);
        foreach (var step in steps) ladder[(int)step.Time] = step.Value;

        return ladder;
    }

    public CreatureAttributes? For(string characterId)
    {
        var health = Health(characterId);

        var attributes = new CreatureAttributes
        {
            Health = health,
            Armor = Row(characterId, Armor),
            Resist = Row(characterId, Resist),
            KillScore = Ordinal(characterId, KillScore),
            SpiritPoints = Ordinal(characterId, SpiritPoints),
        };

        if (attributes is { Health: null, Armor: null, Resist: null, KillScore: null, SpiritPoints: null })
        {
            return null;
        }

        if (health is null) _healthless.Add(characterId);

        return attributes;
    }

    private double? Health(string characterId) =>
        Row(characterId, BaseHealth)
        ?? (Row(characterId, HealthMultiplier) is { } multiplier ? _defaultBaseHealth * multiplier : null);

    private double? Row(string characterId, string attribute) =>
        _base.TryGetValue($"{characterId}.{attribute}", out var value) ? value : null;

    private int? Ordinal(string characterId, string attribute) =>
        Row(characterId, attribute) is { } value ? (int)value : null;
}
