using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine.Curves;

namespace FellowshipExtractor.Extraction;

public readonly record struct CurveKey(double Time, double Value);

public static class CurveRows
{
    private const string KeysField = "Keys";
    private const string TimeField = "Time";
    private const string ValueField = "Value";

    public static UCurveTable Load(AbstractFileProvider provider, string table, string states)
    {
        var file = provider.Files.Values.FirstOrDefault(f =>
            f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(f.Path).Equals(table, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No {table} package in the build. It is what states {states}.");

        return provider.LoadPackage(file).GetExports().OfType<UCurveTable>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{table} serializes no curve table, so {states} has nowhere to come from.");
    }

    public static IReadOnlyList<CurveKey> Keys(FStructFallback row)
    {
        if (PropertyWalk.Get(row, KeysField)?.GenericValue is not UScriptArray keys) return [];

        var read = new List<CurveKey>(keys.Properties.Count);

        foreach (var key in keys.Properties)
        {
            if (key?.GenericValue is not FScriptStruct { StructType: { } point }) continue;

            var step = point switch
            {
                FSimpleCurveKey simple => new CurveKey(Widened(simple.Time), Widened(simple.Value)),
                FRichCurveKey rich => new CurveKey(Widened(rich.Time), Widened(rich.Value)),
                IPropertyHolder holder
                    when PropertyWalk.AsNumber(PropertyWalk.Get(holder, TimeField)?.GenericValue) is { } time &&
                         PropertyWalk.AsNumber(PropertyWalk.Get(holder, ValueField)?.GenericValue) is { } value
                    => new CurveKey(time, value),
                _ => (CurveKey?)null,
            };

            if (step is { } read1) read.Add(read1);
        }

        read.Sort((left, right) => left.Time.CompareTo(right.Time));

        return read;
    }

    public static double? First(FStructFallback row) => Keys(row) is [var only, ..] ? only.Value : null;

    private static double Widened(float value) => PropertyWalk.AsNumber(value)!.Value;
}
