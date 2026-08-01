using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;

namespace FellowshipExtractor;

/// <summary>
/// The build ships two families of plain-text CSV that need no property mappings: the
/// Content/guid tag tables (every item, ability, effect and talent name with its network id)
/// and the RawDataTables tuning constants. Both become JSONL here.
/// </summary>
public readonly record struct TagRecord(string Table, string Name, int NetId, int Flag);

/// <summary>
/// One tuning-constant row. Multi-value rows are per-tier, with <see cref="Columns"/> naming the
/// tiers; a value that would not parse as a number is kept verbatim in <see cref="Raws"/>.
/// </summary>
public readonly record struct ConstantRecord(
    string Table,
    string Key,
    double? Value,
    string[] Raws,
    double?[] Values,
    string[] Columns,
    string? Comment);

public static partial class CsvExportCommands
{
    private const string GuidTableDir = "Content/guid/";

    public static async Task ExportTagsAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var target = Path.Combine(options.ResolvedOutputDir, "tags.jsonl");

        var records = 0;
        await using var writer = new StreamWriter(target);
        foreach (var tag in ReadTags(provider))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(tag));
            records++;
        }

        Console.WriteLine($"Wrote {records} tag records to {target}");
    }

    /// <summary>
    /// Every gameplay tag the build registers for replication, across the 18 Content/guid tables.
    /// This is the complete name list for items, abilities, effects, talents and traits.
    /// </summary>
    public static IEnumerable<TagRecord> ReadTags(AbstractFileProvider provider)
    {
        foreach (var file in CsvFiles(provider).Where(f => f.Path.Contains(GuidTableDir, StringComparison.OrdinalIgnoreCase)))
        {
            var table = Path.GetFileNameWithoutExtension(file.Path);
            foreach (var line in Lines(file))
            {
                // The first line is a table version number, and one shipped row is missing its
                // newline, so scan for id triples rather than splitting the line.
                foreach (Match match in TagRow().Matches(line))
                {
                    yield return new TagRecord(
                        table,
                        match.Groups[1].Value.Trim(),
                        int.Parse(match.Groups[2].Value),
                        int.Parse(match.Groups[3].Value));
                }
            }
        }
    }

    public static async Task ExportConstantsAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var target = Path.Combine(options.ResolvedOutputDir, "ability-constants.jsonl");
        var json = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        int records = 0, nonNumeric = 0, tiered = 0;
        await using var writer = new StreamWriter(target);

        foreach (var c in ReadConstants(provider))
        {
            if (c.Values.Any(v => v is null)) nonNumeric++;
            if (c.Values.Length > 1) tiered++;

            var record = new
            {
                table = c.Table,
                key = c.Key,
                value = c.Value,
                raw = c.Value is null ? c.Raws[0] : null,
                values = c.Values.Length > 1 ? c.Values : null,
                raws = c.Values.Length > 1 && c.Values.Any(v => v is null) ? c.Raws : null,
                columns = c.Values.Length > 1 ? c.Columns : null,
                comment = c.Comment,
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(record, json));
            records++;
        }

        Console.WriteLine(
            $"Wrote {records} constants to {target} ({tiered} multi-column, {nonNumeric} with non-numeric value(s))");
    }

    /// <summary>
    /// Every tuning constant the build ships. These are the values ability assets reach through
    /// an FScalableFloat curve-table row name.
    /// </summary>
    public static IEnumerable<ConstantRecord> ReadConstants(AbstractFileProvider provider)
    {
        foreach (var file in CsvFiles(provider).Where(f => !f.Path.Contains(GuidTableDir, StringComparison.OrdinalIgnoreCase)))
        {
            var table = Path.GetFileNameWithoutExtension(file.Path);
            string[]? columns = null;

            foreach (var line in Lines(file))
            {
                var (fields, comment) = SplitRow(line);
                var key = fields[0];

                // The header row names the tier columns: "---,1,2,3,4".
                if (key.Length > 0 && key.All(c => c == '-'))
                {
                    columns = fields[1..];
                    continue;
                }

                if (key.Length == 0 || key.StartsWith(';') || fields.Length < 2) continue;

                var raws = fields[1..];
                var values = raws
                    .Select(r => double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null)
                    .ToArray();

                yield return new ConstantRecord(
                    table, key, values[0], raws, values, Labels(columns, values.Length), comment);
            }
        }
    }

    private static string[] Labels(string[]? columns, int count) =>
        columns is not null && columns.Length == count
            ? columns
            : [.. Enumerable.Range(1, count).Select(i => i.ToString())];

    /// <summary>
    /// Splits a row into comma-separated fields. Designer notes trail a ';' or follow whitespace
    /// after the last value, and belong to the row rather than to any one field.
    /// </summary>
    private static (string[] Fields, string? Comment) SplitRow(string line)
    {
        var semicolon = line.IndexOf(';');
        var comment = semicolon >= 0 ? line[(semicolon + 1)..].Trim() : null;
        var body = semicolon >= 0 ? line[..semicolon] : line;

        var fields = body.Split(',').Select(f => f.Trim()).ToArray();

        var last = fields[^1];
        var split = last.IndexOfAny([' ', '\t']);
        if (split > 0)
        {
            var trailing = last[(split + 1)..].Trim();
            fields[^1] = last[..split];
            comment = string.IsNullOrEmpty(comment) ? trailing : $"{trailing} {comment}";
        }

        return (fields, string.IsNullOrEmpty(comment) ? null : comment);
    }

    private static IEnumerable<GameFile> CsvFiles(AbstractFileProvider provider) =>
        provider.Files.Values
            .Where(f => f.Path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Lines(GameFile file) =>
        Encoding.UTF8.GetString(file.Read())
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Trim().Length > 0);

    [GeneratedRegex(@"([^,]+?),(\d+),(\d+)")]
    private static partial Regex TagRow();
}
