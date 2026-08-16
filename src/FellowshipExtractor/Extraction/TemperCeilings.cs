using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine.Curves;

namespace FellowshipExtractor.Extraction;

public sealed class TemperCeilings
{
    public const string Table = "CT_ItemRarityAndLevelToMaxTempers";

    private const int ConstantInterpolation = 1;

    private const string InterpolationField = "InterpMode";
    private const string KeysField = "Keys";
    private const string TimeField = "Time";
    private const string ValueField = "Value";

    public IReadOnlyDictionary<string, IReadOnlyList<TemperCeiling>> ByRarity { get; }

    public int Count => ByRarity.Count;

    public TemperCeilings(AbstractFileProvider provider, RarityNames rarities)
    {
        var file = provider.Files.Values.FirstOrDefault(f =>
            f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(f.Path).Equals(Table, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No {Table} package in the build. It is the only thing that states how far an item of a given " +
                "rarity and item level can be tempered, so every tooltip would print a tempering line with no " +
                "ceiling behind it.");

        var curves = provider.LoadPackage(file).GetExports().OfType<UCurveTable>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{Table} serializes no curve table, so the tempering ceilings have nowhere to come from.");

        var read = new Dictionary<string, IReadOnlyList<TemperCeiling>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rowName, row) in curves.RowMap)
        {
            read[rowName.Text] = Steps(rowName.Text, row);
        }

        var missing = rarities.Tiers
            .Select(tier => rarities.Label(tier))
            .OfType<string>()
            .Where(rarity => !read.ContainsKey(rarity))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{Table} states no ceiling for {string.Join(" or ", missing)}. Every rung the loot rule sets name " +
                "has a row in the shipped table, so a tooltip read at one of those rungs would print no ceiling.");
        }

        ByRarity = read;
    }

    private static IReadOnlyList<TemperCeiling> Steps(string rarity, FStructFallback row)
    {
        if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, InterpolationField)?.GenericValue)
            is not ConstantInterpolation)
        {
            throw new InvalidOperationException(
                $"{Table}'s {rarity} row no longer interpolates as a constant, so an item level between two keys " +
                "would answer a fraction of a temper instead of holding the lower key's ceiling.");
        }

        if (PropertyWalk.Get(row, KeysField)?.GenericValue is not UScriptArray keys)
        {
            throw new InvalidOperationException($"{Table}'s {rarity} row carries no {KeysField} array.");
        }

        var steps = new List<TemperCeiling>(keys.Properties.Count);

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

            if (read is not { } curveKey) continue;

            steps.Add(new TemperCeiling((int)curveKey.Time, (int)curveKey.Value));
        }

        if (steps.Count == 0)
        {
            throw new InvalidOperationException(
                $"{Table}'s {rarity} row read back with no keys, so every item at that rung would answer the same " +
                "ceiling at every item level.");
        }

        steps.Sort((left, right) => left.ItemLevel.CompareTo(right.ItemLevel));

        return steps;
    }
}
