using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine.Curves;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class CurveReader
{
    private const string KeysField = "Keys";
    private const string InterpolationField = "InterpMode";
    private const string EnumScope = "::";

    public static Curve Read(AbstractFileProvider provider, string tablePath, string rowName)
    {
        if (!provider.TryLoadPackageObject<UCurveTable>(AbilityPresentationIndex.PackagePath(tablePath), out var table))
        {
            throw new InvalidOperationException($"No curve table loads from {tablePath}.");
        }

        var row = table.RowMap.FirstOrDefault(entry => entry.Key.Text.Equals(rowName, StringComparison.Ordinal)).Value
            ?? throw new InvalidOperationException($"{tablePath} has no row {rowName}.");

        var points = CurveRows.Keys(row);

        if (points.Count == 0) throw new InvalidOperationException($"{tablePath} row {rowName} reads no keys.");

        var modes = Modes(row).ToHashSet(StringComparer.Ordinal);

        if (modes.Count != 1)
        {
            throw new InvalidOperationException(
                $"{tablePath} row {rowName} interpolates {(modes.Count == 0 ? "by no stated mode" : string.Join(" and ", modes))}, " +
                "so no one rule reads a value between its keys.");
        }

        return new Curve(modes.Single(), [.. points.Select(point => new CurvePoint(point.Time, point.Value))]);
    }

    private static IEnumerable<string> Modes(IPropertyHolder row)
    {
        if (Member(PropertyWalk.AsString(PropertyWalk.Get(row, InterpolationField)?.GenericValue)) is { } rowMode)
        {
            yield return rowMode;
        }

        if (PropertyWalk.Get(row, KeysField)?.GenericValue is not UScriptArray keys) yield break;

        foreach (var key in keys.Properties)
        {
            switch (key?.GenericValue)
            {
                case FScriptStruct { StructType: FRichCurveKey rich }:
                    yield return rich.InterpMode.ToString();
                    break;
                case FScriptStruct { StructType: IPropertyHolder holder }
                    when Member(PropertyWalk.AsString(PropertyWalk.Get(holder, InterpolationField)?.GenericValue))
                        is { } keyMode:
                    yield return keyMode;
                    break;
            }
        }
    }

    private static string? Member(string? value) =>
        value is null ? null : value.IndexOf(EnumScope, StringComparison.Ordinal) is var scope and >= 0
            ? value[(scope + EnumScope.Length)..]
            : value;
}
