using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine.Curves;

namespace FellowshipExtractor.Extraction;

public sealed class DropItemLevels
{
    public const string Table = "CT_DungeonDifficultyToDropItemLevel";

    public const string DefaultRow = "Default";

    private const string GroupRowStructure = "/Script/fellowship.CRDroptableGroupDataRow";

    private const string OverrideField = "ItemLevelCurveNameOverride";
    private const string NoOverride = "None";

    private const int LinearInterpolation = 0;

    private const string InterpolationField = "InterpMode";
    private const string KeysField = "Keys";
    private const string TimeField = "Time";
    private const string ValueField = "Value";

    public IReadOnlyList<DropItemLevel> Levels { get; }

    public int Count => Levels.Count;

    public int? ItemLevel(string row) =>
        Levels.FirstOrDefault(level => level.Row.Equals(row, StringComparison.OrdinalIgnoreCase))?.ItemLevel;

    public DropItemLevels(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var file = provider.Files.Values.FirstOrDefault(f =>
            f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(f.Path).Equals(Table, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No {Table} package in the build. It is the only thing that states the item level a drop pays at " +
                "a difficulty, so every tooltip would compute its stat magnitudes at a level nothing states.");

        var curves = provider.LoadPackage(file).GetExports().OfType<UCurveTable>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{Table} serializes no curve table, so the drop ladder has nowhere to come from.");

        var wanted = new List<string> { DefaultRow };

        foreach (var overridden in Overrides(provider, assets))
        {
            if (!wanted.Contains(overridden, StringComparer.OrdinalIgnoreCase)) wanted.Add(overridden);
        }

        var levels = new List<DropItemLevel>(wanted.Count);

        foreach (var wantedRow in wanted)
        {
            var row = curves.RowMap.FirstOrDefault(entry =>
                entry.Key.Text.Equals(wantedRow, StringComparison.OrdinalIgnoreCase)).Value
                ?? throw new InvalidOperationException(
                    $"{Table} carries no {wantedRow} row, which a droptable group reads, so a drop reading that " +
                    "group would pay an item level nothing states.");

            levels.Add(Read(wantedRow, row));
        }

        Levels = levels;
    }

    private static IEnumerable<string> Overrides(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, GroupRowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, OverrideField) is not { Length: > 0 } named) continue;
                if (named.Equals(NoOverride, StringComparison.Ordinal)) continue;

                yield return named;
            }
        }
    }

    private static DropItemLevel Read(string rowName, FStructFallback row)
    {
        if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, InterpolationField)?.GenericValue)
            is not LinearInterpolation)
        {
            throw new InvalidOperationException(
                $"{Table}'s {rowName} row no longer interpolates linearly, so the level it pays between two " +
                "difficulties is no longer the lerp the ladder is read as.");
        }

        if (PropertyWalk.Get(row, KeysField)?.GenericValue is not UScriptArray keys)
        {
            throw new InvalidOperationException($"{Table}'s {rowName} row carries no {KeysField} array.");
        }

        var steps = new List<(int Difficulty, int ItemLevel)>(keys.Properties.Count);

        foreach (var key in keys.Properties)
        {
            if (key?.GenericValue is not FScriptStruct { StructType: { } point }) continue;

            var read = point switch
            {
                FSimpleCurveKey simple => ((float Time, float Value)?)(simple.Time, simple.Value),
                FRichCurveKey rich => (rich.Time, rich.Value),
                IPropertyHolder holder
                    when PropertyWalk.AsNumber(PropertyWalk.Get(holder, TimeField)?.GenericValue) is { } time &&
                         PropertyWalk.AsNumber(PropertyWalk.Get(holder, ValueField)?.GenericValue) is { } value
                    => ((float)time, (float)value),
                _ => null,
            };

            if (read is { } curveKey) steps.Add(((int)curveKey.Time, (int)curveKey.Value));
        }

        if (steps.Count == 0)
        {
            throw new InvalidOperationException(
                $"{Table}'s {rowName} row read back with no keys, so the item level it pays is unreadable.");
        }

        steps.Sort((left, right) => left.Difficulty.CompareTo(right.Difficulty));

        var top = steps[^1].ItemLevel;
        var reached = steps.First(step => step.ItemLevel == top);

        return new DropItemLevel(rowName, reached.Difficulty, top);
    }
}
