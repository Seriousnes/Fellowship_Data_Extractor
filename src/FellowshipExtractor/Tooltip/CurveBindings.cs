using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One FScalableFloat or curve-table handle found on an object, resolved to the shipped tuning
/// constant it names. This is the generic link from any asset to its numbers.
/// </summary>
public sealed record CurveBinding(string Property, string RowName)
{
    public string? CurveTable { get; init; }

    /// <summary>Set when the asset looks the row up through a named registry instead of a table.</summary>
    public string? RegistryType { get; init; }

    public double? Value { get; init; }
    public double?[]? Values { get; init; }
    public string[]? Columns { get; init; }
    public string? Comment { get; init; }
    public string? ConstantsTable { get; init; }

    /// <summary>Populated only when the row name is defined in more than one constants table.</summary>
    public string[]? AmbiguousIn { get; init; }
}

public static class CurveBindings
{
    private const string CurveField = ".Curve";

    public static List<CurveBinding> Collect(IPropertyHolder holder, ConstantsTable constants)
    {
        // Struct-valued properties by path, so a curve handle can reach the FScalableFloat that
        // owns it and read the registry name sitting beside it.
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
            var rowName = PropertyWalk.GetString(curve, "RowName");
            if (rowName is null) continue;

            // An FScalableFloat holds the handle in its Curve field; the owning field is the one
            // that carries meaning, so report that path and read its registry name.
            var owner = path.EndsWith(CurveField, StringComparison.Ordinal) ? path[..^CurveField.Length] : null;
            var reported = owner ?? path;
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

    /// <summary>
    /// Unversioned properties omit a field left at its default, so a curve handle whose CurveTable
    /// is unset serializes as a RowName on its own. Requiring both fields silently dropped every
    /// binding that reaches its number through a registry instead of a table - the cooldown on
    /// every ability among them. The field an FScalableFloat holds its handle in is always named
    /// Curve, so that name stands in for the missing CurveTable.
    /// </summary>
    private static bool IsCurveHandle(WalkedProperty property, IPropertyHolder inner) =>
        Names(inner, "RowName") &&
        (Names(inner, "CurveTable") || property.Name.Equals("Curve", StringComparison.Ordinal));

    private static bool Names(IPropertyHolder holder, string name) =>
        holder.Properties.Any(p => p.Name.Text.Equals(name, StringComparison.Ordinal));

    /// <summary>
    /// An FScalableFloat may leave CurveTable unset and name a registry instead. RegistryType is
    /// an FName wrapped in a struct, so it needs one more hop than the other fields.
    /// </summary>
    private static string? RegistryName(IPropertyHolder scalableFloat)
    {
        if (PropertyWalk.GetStruct(scalableFloat, "RegistryType") is { } registry)
        {
            return PropertyWalk.GetString(registry, "Name");
        }
        return PropertyWalk.GetString(scalableFloat, "RegistryType");
    }
}
