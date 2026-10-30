using System.Text;

using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;

namespace FellowshipExtractor.Extraction;

public static class StringTableSources
{
    private const string StringTableDir = "/Localization/StringTables/";
    private const string KeyColumn = "Key";
    private const string SourceStringColumn = "SourceString";

    public static IEnumerable<LocalizedString> Read(AbstractFileProvider provider)
    {
        var files = provider.Files.Values
            .Where(file => file.Path.Contains(StringTableDir, StringComparison.OrdinalIgnoreCase) &&
                           file.Path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            foreach (var entry in Read(file)) yield return entry;
        }
    }

    public static IEnumerable<LocalizedString> Read(GameFile file)
    {
        var table = Path.GetFileNameWithoutExtension(file.Path);
        var rows = Rows(Encoding.UTF8.GetString(file.Read()).TrimStart('﻿')).ToArray();

        if (rows.Length == 0) yield break;

        var key = Array.IndexOf(rows[0], KeyColumn);
        var source = Array.IndexOf(rows[0], SourceStringColumn);

        if (key < 0 || source < 0) yield break;

        foreach (var row in rows.Skip(1))
        {
            if (row.Length <= Math.Max(key, source) || row[key].Length == 0) continue;

            yield return new LocalizedString(table, Unescape(row[key]), Unescape(row[source]));
        }
    }

    public static IEnumerable<string[]> Rows(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') field.Append(text[++i]);
                else quoted = false;

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    if (fields.Any(value => value.Length > 0)) yield return [.. fields];
                    fields.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        fields.Add(field.ToString());
        if (fields.Any(value => value.Length > 0)) yield return [.. fields];
    }

    public static string Unescape(string value)
    {
        if (!value.Contains('\\')) return value;

        var unescaped = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 == value.Length)
            {
                unescaped.Append(value[i]);
                continue;
            }

            var next = value[i + 1];
            char? replaced = next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                _ => null,
            };

            if (replaced is { } escaped)
            {
                unescaped.Append(escaped);
                i++;
            }
            else
            {
                unescaped.Append(value[i]);
            }
        }

        return unescaped.ToString();
    }
}
