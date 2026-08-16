using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;

namespace FellowshipExtractor.Extraction;

public readonly record struct TagRecord(string Table, string Name, int NetId, int Flag);

public readonly record struct ConstantRecord(
    string Table,
    string Key,
    double? Value,
    string[] Raws,
    double?[] Values,
    string[] Columns,
    string? Comment);

public static partial class CsvTables
{
    private const string GuidTableDir = "Content/guid/";

    public static IEnumerable<TagRecord> ReadTags(AbstractFileProvider provider)
    {
        foreach (var file in CsvFiles(provider).Where(f => f.Path.Contains(GuidTableDir, StringComparison.OrdinalIgnoreCase)))
        {
            var table = Path.GetFileNameWithoutExtension(file.Path);
            foreach (var line in Lines(file))
            {
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
