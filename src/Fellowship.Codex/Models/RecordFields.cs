using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Fellowship.Codex.Models;

public sealed record RecordField(string Key, PropertyInfo Property, bool Numeric, bool Listed)
{
    public bool Boolean => !Listed && (Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType) == typeof(bool);

    public IEnumerable<object> Values(EntityRecord record) => Property.GetValue(record) switch
    {
        null => [],
        string text => [text],
        IEnumerable many when Listed => many.Cast<object>(),
        { } one => [one],
    };

    public IEnumerable<string> Texts(EntityRecord record) => Values(record).Select(RecordFields.Text);

    public IEnumerable<double> Numbers(EntityRecord record) =>
        Numeric ? Values(record).Select(value => Convert.ToDouble(value, CultureInfo.InvariantCulture)) : [];
}

public sealed record RecordFieldFilter(RecordField Field)
{
    public IReadOnlyList<string> Wanted { get; init; } = [];

    public double? Min { get; init; }

    public double? Max { get; init; }

    public bool Matches(EntityRecord record)
    {
        if (Wanted.Count > 0 &&
            !Field.Texts(record).Any(value => Wanted.Contains(value, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (Min is null && Max is null) return true;

        return Field.Numbers(record).Any(value => (Min is not { } min || value >= min) && (Max is not { } max || value <= max));
    }
}

public static class RecordFields
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<RecordField>> Fields = new();

    public static IReadOnlyList<RecordField> Of(Type record) => Fields.GetOrAdd(record, Read);

    public static RecordField? Find(Type record, string key) =>
        Of(record).FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Text(object value) => value switch
    {
        bool flag => flag ? "true" : "false",
        IFormattable formattable when value is not Enum => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static IReadOnlyList<RecordField> Read(Type record) =>
    [
        .. record.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.Name != nameof(EntityRecord.Type))
            .Select(Field)
            .OfType<RecordField>()
            .OrderBy(field => field.Key, StringComparer.Ordinal),
    ];

    private static RecordField? Field(PropertyInfo property)
    {
        var key = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        var type = property.PropertyType;

        if (Scalar(type) is { } numeric) return new RecordField(key, property, numeric, Listed: false);

        var element = type.IsGenericType &&
            type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(IReadOnlyList<>) || definition == typeof(IEnumerable<>) ||
                definition == typeof(IReadOnlyCollection<>))
            ? type.GetGenericArguments()[0]
            : null;

        return element is not null && Scalar(element) is { } listed
            ? new RecordField(key, property, listed, Listed: true)
            : null;
    }

    private static bool? Scalar(Type type)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(string) || bare == typeof(bool) || bare.IsEnum) return false;

        return bare == typeof(int) || bare == typeof(long) || bare == typeof(double) ||
            bare == typeof(float) || bare == typeof(decimal)
            ? true
            : null;
    }
}
