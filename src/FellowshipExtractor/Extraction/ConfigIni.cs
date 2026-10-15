using System.Text;

using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Extraction;

public abstract record ConfigValue
{
    public abstract string Text { get; }
}

public sealed record ConfigScalar(string Value) : ConfigValue
{
    public override string Text => Value;
}

public sealed record ConfigEntry(string? Name, ConfigValue Value);

public sealed record ConfigList(IReadOnlyList<ConfigEntry> Entries) : ConfigValue
{
    public override string Text =>
        "(" + string.Join(
            ",",
            Entries.Select(entry => entry.Name is null ? entry.Value.Text : $"{entry.Name}={entry.Value.Text}")) + ")";

    public ConfigValue? Field(string name) => Entries
        .FirstOrDefault(entry => name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
        ?.Value;

    public IEnumerable<(string Key, ConfigValue Value)> Pairs()
    {
        foreach (var entry in Entries)
        {
            if (entry is { Name: null, Value: ConfigList { Entries: [{ Name: null } key, { Name: null } value] } })
            {
                yield return (key.Value.Text, value.Value);
            }
        }
    }
}

public sealed class ConfigIni
{
    private readonly Dictionary<string, List<string>> _bySection;

    private ConfigIni(Dictionary<string, List<string>> bySection, string path, string section)
    {
        _bySection = bySection;
        Path = path;
        Section = section;
    }

    public string Path { get; }

    public string Section { get; }

    public static ConfigIni Read(AbstractFileProvider provider, string path, string section)
    {
        if (!provider.Files.TryGetValue(path, out var file))
        {
            throw new FileNotFoundException(
                $"{path} is not in the containers, so the section [{section}] cannot be read. A " +
                "UDeveloperSettings class has no asset, and this file is the only place its values are stated.",
                path);
        }

        var keys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var inside = false;

        foreach (var raw in Encoding.UTF8.GetString(file.Read()).Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');

            if (line.Length == 0 || line[0] is ';' or '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                inside = line[1..^1].Equals(section, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inside) continue;

            var assigned = line.IndexOf('=');
            if (assigned < 0) continue;

            var name = line[..assigned].TrimStart('+', '-', '.', '!').Trim();
            if (name.Length == 0) continue;

            if (!keys.TryGetValue(name, out var values)) keys[name] = values = [];
            values.Add(line[(assigned + 1)..].Trim());
        }

        if (keys.Count == 0)
        {
            throw new InvalidOperationException(
                $"{path} declares no section [{section}], or the section is empty. Every value in the " +
                "section would read back as a constructor default that no shipped file states.");
        }

        return new ConfigIni(keys, path, section);
    }

    public string Text(string key) =>
        _bySection.TryGetValue(key, out var values) && values.Count > 0
            ? values[^1]
            : throw new InvalidOperationException(
                $"[{Section}] in {Path} does not set {key}. Its value would come from a C++ constructor that " +
                "is in no file, so nothing here can state it.");

    public ConfigValue Required(string key) => Parse(Text(key));

    public ConfigList RequiredList(string key) =>
        Required(key) as ConfigList
        ?? throw new InvalidOperationException(
            $"[{Section}] in {Path} sets {key} to a scalar, not the parenthesised literal a struct or a map " +
            "is written as.");

    public int Integer(string key) =>
        int.TryParse(Text(key), System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException(
                $"[{Section}] in {Path} sets {key} to '{Text(key)}', not a whole number.");

    public static ConfigValue Parse(string text)
    {
        var at = 0;
        var value = Value(text, ref at);

        Whitespace(text, ref at);

        return at == text.Length
            ? value
            : throw new InvalidOperationException(
                $"'{text}' has {text.Length - at} characters left over after the value it opens with, so " +
                "it is not one config literal.");
    }

    private static ConfigValue Value(string text, ref int at)
    {
        Whitespace(text, ref at);

        if (at >= text.Length) return new ConfigScalar(string.Empty);

        return text[at] == '(' ? List(text, ref at) : Scalar(text, ref at);
    }

    private static ConfigList List(string text, ref int at)
    {
        at++;

        var entries = new List<ConfigEntry>();

        Whitespace(text, ref at);

        if (at < text.Length && text[at] == ')')
        {
            at++;
            return new ConfigList(entries);
        }

        while (true)
        {
            entries.Add(Entry(text, ref at));

            Whitespace(text, ref at);

            if (at >= text.Length)
            {
                throw new InvalidOperationException($"'{text}' opens a literal it never closes.");
            }

            if (text[at] == ',')
            {
                at++;
                continue;
            }

            if (text[at] == ')')
            {
                at++;
                return new ConfigList(entries);
            }

            throw new InvalidOperationException(
                $"'{text}' has '{text[at]}' at {at}. A ',' or a ')' has to be there.");
        }
    }

    private static ConfigEntry Entry(string text, ref int at)
    {
        Whitespace(text, ref at);

        var end = at;

        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '_')) end++;

        var after = end;
        while (after < text.Length && char.IsWhiteSpace(text[after])) after++;

        if (end > at && after < text.Length && text[after] == '=')
        {
            var name = text[at..end];
            at = after + 1;

            return new ConfigEntry(name, Value(text, ref at));
        }

        return new ConfigEntry(null, Value(text, ref at));
    }

    private static ConfigScalar Scalar(string text, ref int at)
    {
        if (text[at] == '"')
        {
            var quoted = new StringBuilder();
            at++;

            while (at < text.Length && text[at] != '"')
            {
                if (text[at] == '\\' && at + 1 < text.Length) at++;
                quoted.Append(text[at++]);
            }

            if (at >= text.Length) throw new InvalidOperationException($"'{text}' opens a quote it never closes.");

            at++;
            return new ConfigScalar(quoted.ToString());
        }

        var from = at;
        var depth = 0;

        while (at < text.Length)
        {
            var glyph = text[at];

            if (glyph == '\'') depth ^= 1;
            else if (depth == 0 && glyph is ',' or ')') break;

            at++;
        }

        return new ConfigScalar(text[from..at].Trim());
    }

    private static void Whitespace(string text, ref int at)
    {
        while (at < text.Length && char.IsWhiteSpace(text[at])) at++;
    }
}
