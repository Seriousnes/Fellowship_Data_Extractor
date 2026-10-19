using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class DungeonScoring
{
    public const string ScoreTable = "CT_MaxDungeonScore";
    public const string ModifierTable = "CT_DungeonModifiers";

    private const string ScoreRow = "MaxDungeonScore";
    private const string AbilityUnlockRow = "AbilityUnlock.Level";

    private const int LinearInterpolation = 0;
    private const string InterpolationField = "InterpMode";

    public IReadOnlyDictionary<int, double> MaxDungeonScore { get; }

    public int ModifierAbilityUnlock { get; }

    public DungeonScoring(AbstractFileProvider provider, IReadOnlyCollection<int> difficulties)
    {
        var scores = CurveRows.Load(provider, ScoreTable, "the most dungeon score a difficulty awards");
        var score = Row(scores, ScoreTable, ScoreRow);

        if (PropertyWalk.AsOrdinal(PropertyWalk.Get(score, InterpolationField)?.GenericValue) is not LinearInterpolation)
        {
            throw new InvalidOperationException(
                $"{ScoreTable}'s {ScoreRow} row no longer interpolates linearly, so the score it states between " +
                "two keys is no longer the lerp the ladder is read as.");
        }

        var keys = CurveRows.Keys(score);
        if (keys.Count == 0)
        {
            throw new InvalidOperationException($"{ScoreTable}'s {ScoreRow} row read back with no keys.");
        }

        MaxDungeonScore = difficulties.Order().ToDictionary(difficulty => difficulty, difficulty => CurveRows.Evaluate(keys, difficulty));

        var modifiers = CurveRows.Load(provider, ModifierTable, "the difficulty dungeon modifiers gain abilities at");

        ModifierAbilityUnlock = CurveRows.First(Row(modifiers, ModifierTable, AbilityUnlockRow)) is { } unlock
            ? (int)Math.Round(unlock)
            : throw new InvalidOperationException($"{ModifierTable}'s {AbilityUnlockRow} row states no value.");
    }

    private static FStructFallback Row(UCurveTable table, string name, string row) =>
        table.RowMap.FirstOrDefault(entry => entry.Key.Text.Equals(row, StringComparison.OrdinalIgnoreCase)).Value
        ?? throw new InvalidOperationException($"{name} has no {row} row.");
}
