using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>Rows a reader sets an entity's scalar field from, with the <see cref="ResourceGains"/> kind.</summary>
public static class FieldSources
{
    public static string Key(string member) => EntityDocuments.NamingPolicy.ConvertName(member);

    /// <summary>The field has the value of <paramref name="row"/>.</summary>
    public static FieldSource? Row(string? row) =>
        row is null || row.Length == 0 || row.Equals("None", StringComparison.Ordinal)
            ? null
            : new FieldSource(ResourceGains.ConstantsRow, [row]);

    /// <summary>A scalable float: its row times its <c>Value</c>.</summary>
    public static FieldSource? Scalable(string? row, double? multiplier) =>
        Row(row) is { } read
            ? multiplier is null or 1 ? read : read with { Kind = ResourceGains.RowProduct }
            : null;

    public static FieldSource? Of(string kind, IEnumerable<string?> rows) =>
        rows.OfType<string>()
            .Where(row => row.Length > 0 && !row.Equals("None", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() is { Length: > 0 } read
            ? new FieldSource(kind, read)
            : null;

    /// <summary>A value computed from <paramref name="sources"/>.</summary>
    public static FieldSource? Joined(string kind, params FieldSource?[] sources) =>
        Of(kind, sources.SelectMany(source => source?.Rows ?? []));

    public static FieldSource? As(FieldSource? source, string kind) => source is null ? null : source with { Kind = kind };

    /// <summary>The binding's source, or its row where the binding's value is the row's.</summary>
    public static FieldSource? Of(CurveBinding binding) =>
        binding.Source ?? (binding.Computed ? null : Row(binding.RowName));

    public static IReadOnlyDictionary<string, FieldSource>? Set(
        IReadOnlyDictionary<string, FieldSource>? sources, string member, FieldSource? source)
    {
        var key = Key(member);

        if (source is null)
        {
            if (sources is null || !sources.ContainsKey(key)) return sources;

            var without = sources.Where(entry => entry.Key != key).ToDictionary(StringComparer.Ordinal);
            return without.Count == 0 ? null : without;
        }

        var with = new Dictionary<string, FieldSource>(sources ?? new Dictionary<string, FieldSource>(), StringComparer.Ordinal)
        {
            [key] = source,
        };

        return with;
    }

    public static FieldSource? Get(IReadOnlyDictionary<string, FieldSource>? sources, string member) =>
        sources?.GetValueOrDefault(Key(member));

    /// <summary>
    /// Sources with each row under its constants key, keeping only fields with a value. A source of a field with no
    /// value belongs to no field.
    /// </summary>
    public static IReadOnlyDictionary<string, FieldSource>? Finished(Entity entity, ConstantsTable constants)
    {
        if (entity.FieldSources is not { Count: > 0 } sources) return null;

        var finished = new SortedDictionary<string, FieldSource>(StringComparer.Ordinal);

        foreach (var (field, source) in sources)
        {
            if (Members.GetValueOrDefault(field)?.GetValue(entity) is null) continue;

            if (Of(source.Kind, source.Rows.Select(row => constants.Lookup(row).Record?.Key ?? row)) is { } keyed)
            {
                finished[field] = keyed;
            }
        }

        return finished.Count == 0 ? null : finished;
    }

    private static readonly Dictionary<string, System.Reflection.PropertyInfo> Members =
        typeof(Entity).GetProperties().ToDictionary(property => Key(property.Name), StringComparer.Ordinal);
}
