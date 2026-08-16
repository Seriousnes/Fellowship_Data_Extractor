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

    public IReadOnlyList<DropPlateau> Plateaus { get; }

    public int Count => Plateaus.Count;

    public int? Plateau(string row) =>
        Plateaus.FirstOrDefault(plateau => plateau.Row.Equals(row, StringComparison.OrdinalIgnoreCase))?.ItemLevel;

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

        var plateaus = new List<DropPlateau>(wanted.Count);

        foreach (var wantedRow in wanted)
        {
            var row = curves.RowMap.FirstOrDefault(entry =>
                entry.Key.Text.Equals(wantedRow, StringComparison.OrdinalIgnoreCase)).Value
                ?? throw new InvalidOperationException(
                    $"{Table} carries no {wantedRow} row, which a droptable group reads, so a drop reading that " +
                    "group would pay an item level nothing states.");

            plateaus.Add(Plateau(wantedRow, row));
        }

        Plateaus = plateaus;
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

    private static DropPlateau Plateau(string rowName, FStructFallback row)
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
                $"{Table}'s {rowName} row read back with no keys, so the level it plateaus at is unreadable.");
        }

        steps.Sort((left, right) => left.Difficulty.CompareTo(right.Difficulty));

        var plateau = steps[^1].ItemLevel;
        var reached = steps.First(step => step.ItemLevel == plateau);

        return new DropPlateau(rowName, reached.Difficulty, plateau);
    }
}
