using System.Text;
using System.Text.Json;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

/// <summary>
/// Rows of a list with name, type slug, address, and each facet and measure with a value on at least one row.
/// Each field name is the query key of the filter or sort on that field. "; " separates the values of a facet
/// with several values.
/// </summary>
public sealed class EntityTable
{
    public const string Csv = "csv";

    public const string Json = "json";

    private const string Separator = "; ";

    private const string Url = "url";

    private readonly IReadOnlyList<Field> fields;

    private readonly IReadOnlyList<EntityDocument> rows;

    private sealed record Field(string Label, Func<EntityDocument, object?> Value);

    private EntityTable(IReadOnlyList<Field> fields, IReadOnlyList<EntityDocument> rows)
    {
        this.fields = fields;
        this.rows = rows;
    }

    public static string ContentType(string format) => format switch
    {
        Csv => "text/csv; charset=utf-8",
        Json => "application/json; charset=utf-8",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static EntityTable Of(
        IReadOnlyList<EntityDocument> rows, ICodexService codex, ICodexViewService view, string origin)
    {
        var fields = new List<Field>
        {
            new(QueryKeys.Key(SortingType.Name), PublicTypes.NameOf),
            new(QueryKeys.Key(BrowseParameter.Type), row => EntityTypes.Slug(EntityTypes.TypeOf(row))),
            new(Url, row => origin + view.Href(row)),
        };

        foreach (var facet in codex.Facets.Where(facet => rows.Any(row => facet.Values(row).Any())))
        {
            fields.Add(new Field(
                facet.Key,
                row => facet.Values(row).Select(value => codex.Printed(facet, value)).ToArray() is { Length: > 0 } printed
                    ? string.Join(Separator, printed)
                    : null));
        }

        foreach (var measure in codex.Measures.Where(measure => rows.Any(row => measure.Value(row) is not null)))
        {
            fields.Add(new Field(measure.Key, row => measure.Value(row)));
        }

        return new EntityTable(fields, rows);
    }

    public byte[] Write(string format) => format switch
    {
        Csv => WriteCsv(),
        Json => WriteJson(),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private byte[] WriteCsv()
    {
        var text = new StringBuilder();

        text.AppendJoin(',', fields.Select(field => Quoted(field.Label))).Append("\r\n");

        foreach (var row in rows)
        {
            text.AppendJoin(',', fields.Select(field => Quoted(Printed(field.Value(row))))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private byte[] WriteJson()
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartArray();

            foreach (var row in rows)
            {
                json.WriteStartObject();

                foreach (var field in fields)
                {
                    switch (field.Value(row))
                    {
                        case null:
                            json.WriteNull(field.Label);
                            break;
                        case double number:
                            json.WriteNumber(field.Label, number);
                            break;
                        case var value:
                            json.WriteString(field.Label, value.ToString());
                            break;
                    }
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        return buffer.ToArray();
    }

    private static string Printed(object? value) => value switch
    {
        null => string.Empty,
        double number => FellowshipCodex.Number(number),
        var other => other.ToString() ?? string.Empty,
    };

    private static string Quoted(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0 ? value : '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
