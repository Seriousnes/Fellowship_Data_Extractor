using System.Text;

using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Extraction;

public abstract record ConfigValue
{
    public abstract string Text { get; }
}

public sealed record ConfigScalar(string Written) : ConfigValue
{
    public override string Text => Written;
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
                "UDeveloperSettings class ships no asset, and this file is the only place its values live.",
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

            if (!keys.TryGetValue(name, out var written)) keys[name] = written = [];
            written.Add(line[(assigned + 1)..].Trim());
        }

        if (keys.Count == 0)
        {
            throw new InvalidOperationException(
                $"{path} declares no section [{section}], or the section is empty. Every value the section " +
                "carries would read back as a constructor default that no shipped file states.");
        }

        return new ConfigIni(keys, path, section);
    }

    public string Written(string key) =>
        _bySection.TryGetValue(key, out var written) && written.Count > 0
            ? written[^1]
            : throw new InvalidOperationException(
                $"[{Section}] in {Path} does not set {key}. Its value would come from a C++ constructor that " +
                "ships in no file, so nothing here can state it.");

    public ConfigValue Required(string key) => Parse(Written(key));

    public ConfigList RequiredList(string key) =>
        Required(key) as ConfigList
        ?? throw new InvalidOperationException(
            $"[{Section}] in {Path} sets {key} to a scalar, not the parenthesised literal a struct or a map " +
            "is written as.");

    public int Whole(string key) =>
        int.TryParse(Written(key), System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException(
                $"[{Section}] in {Path} sets {key} to '{Written(key)}', which is not a whole number.");

    public static ConfigValue Parse(string written)
    {
        var at = 0;
        var value = Value(written, ref at);

        Whitespace(written, ref at);

        return at == written.Length
            ? value
            : throw new InvalidOperationException(
                $"'{written}' has {written.Length - at} characters left over after the value it opens with, so " +
                "it is not one config literal.");
    }

    private static ConfigValue Value(string written, ref int at)
    {
        Whitespace(written, ref at);

        if (at >= written.Length) return new ConfigScalar(string.Empty);

        return written[at] == '(' ? List(written, ref at) : Scalar(written, ref at);
    }

    private static ConfigList List(string written, ref int at)
    {
        at++;

        var entries = new List<ConfigEntry>();

        Whitespace(written, ref at);

        if (at < written.Length && written[at] == ')')
        {
            at++;
            return new ConfigList(entries);
        }

        while (true)
        {
            entries.Add(Entry(written, ref at));

            Whitespace(written, ref at);

            if (at >= written.Length)
            {
                throw new InvalidOperationException($"'{written}' opens a literal it never closes.");
            }

            if (written[at] == ',')
            {
                at++;
                continue;
            }

            if (written[at] == ')')
            {
                at++;
                return new ConfigList(entries);
            }

            throw new InvalidOperationException(
                $"'{written}' has '{written[at]}' at {at}, where a ',' or a ')' has to be.");
        }
    }

    private static ConfigEntry Entry(string written, ref int at)
    {
        Whitespace(written, ref at);

        var named = at;

        while (named < written.Length && (char.IsLetterOrDigit(written[named]) || written[named] is '_')) named++;

        var after = named;
        while (after < written.Length && char.IsWhiteSpace(written[after])) after++;

        if (named > at && after < written.Length && written[after] == '=')
        {
            var name = written[at..named];
            at = after + 1;

            return new ConfigEntry(name, Value(written, ref at));
        }

        return new ConfigEntry(null, Value(written, ref at));
    }

    private static ConfigScalar Scalar(string written, ref int at)
    {
        if (written[at] == '"')
        {
            var quoted = new StringBuilder();
            at++;

            while (at < written.Length && written[at] != '"')
            {
                if (written[at] == '\\' && at + 1 < written.Length) at++;
                quoted.Append(written[at++]);
            }

            if (at >= written.Length) throw new InvalidOperationException($"'{written}' opens a quote it never closes.");

            at++;
            return new ConfigScalar(quoted.ToString());
        }

        var from = at;
        var depth = 0;

        while (at < written.Length)
        {
            var glyph = written[at];

            if (glyph == '\'') depth ^= 1;
            else if (depth == 0 && glyph is ',' or ')') break;

            at++;
        }

        return new ConfigScalar(written[from..at].Trim());
    }

    private static void Whitespace(string written, ref int at)
    {
        while (at < written.Length && char.IsWhiteSpace(written[at])) at++;
    }
}
