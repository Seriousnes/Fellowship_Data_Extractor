using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record CurveBinding(string Property, string RowName)
{
    public string? CurveTable { get; init; }

    public string? RegistryType { get; init; }

    public double? Value { get; init; }
    public double?[]? Values { get; init; }
    public string[]? Columns { get; init; }
    public string? Comment { get; init; }
    public string? ConstantsTable { get; init; }

    public bool Multiplies { get; init; }

    public bool Computed { get; init; }

    public string[]? ComputedFrom { get; init; }

    public string[]? AmbiguousIn { get; init; }
}

public static class CurveBindings
{
    private const string CurveField = ".Curve";

    public static List<CurveBinding> Collect(IPropertyHolder holder, ConstantsTable constants)
    {
        var structs = new Dictionary<string, IPropertyHolder>(StringComparer.Ordinal);
        var handles = new List<(string Path, IPropertyHolder Curve)>();

        foreach (var property in PropertyWalk.Walk(holder))
        {
            if (property.Value is not FScriptStruct { StructType: IPropertyHolder inner }) continue;
            structs[property.Path] = inner;

            if (IsCurveHandle(property, inner)) handles.Add((property.Path, inner));
        }

        var found = new List<CurveBinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, curve) in handles)
        {
            var owner = path.EndsWith(CurveField, StringComparison.Ordinal) ? path[..^CurveField.Length] : null;
            var reported = owner ?? path;

            var rowName = PropertyWalk.GetString(curve, "RowName");

            if (rowName is null)
            {
                if (owner is null || Numbers.Field(reported) is not { } field || field == "cost") continue;
                if (!seen.Add($"{reported}|None")) continue;
                if (!structs.TryGetValue(owner, out var bare)) continue;
                if (PropertyWalk.AsNumber(PropertyWalk.Get(bare, "Value")?.GenericValue) is not { } literal) continue;
                if (literal <= 0) continue;

                found.Add(new CurveBinding(reported, "None") { Value = literal });
                continue;
            }

            if (!seen.Add($"{reported}|{rowName}")) continue;

            var (record, tables) = constants.Lookup(rowName);

            found.Add(new CurveBinding(reported, rowName)
            {
                CurveTable = PropertyWalk.GetString(curve, "CurveTable"),
                RegistryType = owner is not null && structs.TryGetValue(owner, out var scalable)
                    ? RegistryName(scalable)
                    : null,
                Value = record?.Value,
                Values = record is { Values.Length: > 1 } r ? r.Values : null,
                Columns = record is { Values.Length: > 1 } c ? c.Columns : null,
                Comment = record?.Comment,
                ConstantsTable = record?.Table,
                AmbiguousIn = tables.Length > 1 ? tables : null,
            });
        }

        return found;
    }

    private static bool IsCurveHandle(WalkedProperty property, IPropertyHolder inner) =>
        Names(inner, "RowName") &&
        (Names(inner, "CurveTable") || property.Name.Equals("Curve", StringComparison.Ordinal));

    private static bool Names(IPropertyHolder holder, string name) =>
        holder.Properties.Any(p => p.Name.Text.Equals(name, StringComparison.Ordinal));

    private static string? RegistryName(IPropertyHolder scalableFloat)
    {
        if (PropertyWalk.GetStruct(scalableFloat, "RegistryType") is { } registry)
        {
            return PropertyWalk.GetString(registry, "Name");
        }
        return PropertyWalk.GetString(scalableFloat, "RegistryType");
    }
}
