using System.Text.Json.Nodes;

namespace Fellowship.SDK.Documents;

public static class PatchNoteDiff
{
    private const string Element = "[]";

    private const string Step = " › ";

    private static readonly HashSet<string> NoShape = [];

    /// <summary>The document member naming the rows each field is set from. A patch note has the fields alone.</summary>
    private static readonly string Sources = EntityDocuments.NamingPolicy.ConvertName(nameof(EntityDocument.FieldSources));

    public static IReadOnlyList<PatchNote> Between(
        IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> older,
        IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> newer)
    {
        var readings = newer.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<JsonNode?>)[.. entry.Value.Select(line => JsonNode.Parse(line.Span))]);

        var shapes = Shapes(readings);
        var notes = new List<PatchNote>();

        foreach (var (key, now) in readings)
        {
            IReadOnlyList<JsonNode?> was = older.TryGetValue(key, out var lines)
                ? [.. lines.Select(line => JsonNode.Parse(line.Span))]
                : [];

            var shape = shapes.GetValueOrDefault(key.Slug) ?? NoShape;
            var kept = new List<PatchNote>();

            foreach (var reading in now)
            {
                var note = Matching(was, now, reading) is { } before
                    ? new PatchNote(key, Name(reading), Icon(reading), false, Fields(before, reading, shape))
                    : new PatchNote(key, Name(reading), Icon(reading), true, []);

                if (!note.Added && note.Fields.Count == 0) continue;
                if (kept.Any(other => Same(other, note))) continue;

                kept.Add(note);
            }

            notes.AddRange(kept);
        }

        return notes;
    }

    public static IReadOnlyList<PatchNoteField> Fields(JsonNode? older, JsonNode? newer, IReadOnlySet<string> shape)
    {
        var found = new List<PatchNoteField>();

        if (older is JsonObject was && newer is JsonObject now) Members(string.Empty, null, was, now, shape, found);

        return found;
    }

    public static IReadOnlyDictionary<string, HashSet<string>> Shapes(
        IReadOnlyDictionary<DocumentKey, IReadOnlyList<JsonNode?>> readings)
    {
        var shapes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (key, nodes) in readings)
        {
            if (!shapes.TryGetValue(key.Slug, out var shape)) shapes[key.Slug] = shape = new(StringComparer.Ordinal);

            foreach (var node in nodes) Outline(string.Empty, node, shape);
        }

        return shapes;
    }

    private static void Outline(string path, JsonNode? node, HashSet<string> shape)
    {
        switch (node)
        {
            case JsonObject fields:
                foreach (var (name, value) in fields)
                {
                    var below = Below(path, name);

                    shape.Add(below);
                    Outline(below, value, shape);
                }

                break;

            case JsonArray listed:
                var element = path + Element;

                foreach (var value in listed)
                {
                    shape.Add(element);
                    Outline(element, value, shape);
                }

                break;
        }
    }

    private static JsonNode? Matching(IReadOnlyList<JsonNode?> was, IReadOnlyList<JsonNode?> now, JsonNode? reading) =>
        was.Count == 1 && now.Count == 1
            ? was[0]
            : was.FirstOrDefault(before => DocumentVariants.Same(before, reading));

    private static bool Same(PatchNote one, PatchNote other) =>
        one.Added == other.Added && one.Name == other.Name && one.Fields.SequenceEqual(other.Fields);

    private static void Compare(
        string path, string label, JsonNode? before, JsonNode? after, IReadOnlySet<string> shape, List<PatchNoteField> found)
    {
        if (before is null) return;

        if (after is null)
        {
            if (shape.Contains(path)) Spread(label, before, removed: true, found);

            return;
        }

        switch (before, after)
        {
            case (JsonObject was, JsonObject now):
                Members(path, label, was, now, shape, found);
                break;

            case (JsonArray was, JsonArray now):
                Elements(path, label, was, now, shape, found);
                break;

            case (JsonValue, JsonValue) when !JsonNode.DeepEquals(before, after):
                found.Add(new PatchNoteField(label, Text(before), Text(after)));
                break;
        }
    }

    private static void Members(
        string path, string? label, JsonObject was, JsonObject now, IReadOnlySet<string> shape, List<PatchNoteField> found)
    {
        foreach (var (name, value) in was)
        {
            if (path.Length == 0 && name == Sources) continue;

            now.TryGetPropertyValue(name, out var after);

            Compare(Below(path, name), Join(label, Named(name)), value, after, shape, found);
        }
    }

    private static void Elements(
        string path, string label, JsonArray was, JsonArray now, IReadOnlySet<string> shape, List<PatchNoteField> found)
    {
        if (was.All(value => value is JsonValue) && now.All(value => value is JsonValue))
        {
            if (!JsonNode.DeepEquals(was, now))
            {
                found.Add(new PatchNoteField(
                    label, was.Count == 0 ? null : Listed(was), now.Count == 0 ? null : Listed(now)));
            }

            return;
        }

        var element = path + Element;

        if (was.All(Identified) && now.All(Identified))
        {
            var byIdentity = now
                .GroupBy(Identity)
                .ToDictionary(group => group.Key, group => group.First());

            var known = was.Select(Identity).ToHashSet(StringComparer.Ordinal);

            foreach (var before in was)
            {
                if (byIdentity.GetValueOrDefault(Identity(before)) is { } after)
                {
                    if (Name(before) is { } name) Compare(element, Join(label, name), before, after, shape, found);
                }
                else
                {
                    Spread(label, before, removed: true, found);
                }
            }

            foreach (var after in now.Where(after => known.Add(Identity(after))))
            {
                Spread(label, after, removed: false, found);
            }

            return;
        }

        var unpaired = now.ToList();
        var unmatched = new List<(int At, JsonNode? Value)>();

        for (var at = 0; at < was.Count; at++)
        {
            var same = unpaired.FindIndex(after => JsonNode.DeepEquals(after, was[at]));

            if (same >= 0) unpaired.RemoveAt(same);
            else unmatched.Add((at, was[at]));
        }

        for (var at = 0; at < unmatched.Count; at++)
        {
            var (position, before) = unmatched[at];
            var after = at < unpaired.Count ? unpaired[at] : null;

            Compare(element, $"{label} {position + 1}", before, after, shape, found);
        }

        foreach (var after in unpaired.Skip(unmatched.Count))
        {
            Spread(label, after, removed: false, found);
        }
    }

    private static void Spread(string label, JsonNode? node, bool removed, List<PatchNoteField> found)
    {
        switch (node)
        {
            case null:
                return;

            case JsonObject fields when Identified(fields) || Name(fields) is not null:
                if (Name(fields) is { } name) Add(label, name);
                return;

            case JsonObject fields:
                foreach (var (member, value) in fields) Spread(Join(label, Named(member)), value, removed, found);
                return;

            case JsonArray values when values.All(value => value is JsonValue):
                if (values.Count > 0) Add(label, Listed(values));
                return;

            case JsonArray values:
                for (var at = 0; at < values.Count; at++) Spread($"{label} {at + 1}", values[at], removed, found);
                return;

            default:
                Add(label, Text(node));
                return;
        }

        void Add(string at, string text) =>
            found.Add(removed ? new PatchNoteField(at, text, null) : new PatchNoteField(at, null, text));
    }

    private static bool Identified(JsonNode? node) =>
        node is JsonObject fields &&
        fields["type"] is JsonValue type && type.TryGetValue<string>(out _) &&
        fields["id"] is JsonValue id && id.TryGetValue<int>(out _);

    private static string Identity(JsonNode? node) => $"{node!["type"]}:{node["id"]}";

    private static string? Name(JsonNode? node) =>
        node?["name"] is JsonValue name && name.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static string? Icon(JsonNode? node) =>
        node?["icon"] is JsonValue icon && icon.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static string Below(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private static string Join(string? label, string name) => label is null ? name : label + Step + name;

    private static string Named(string name) =>
        name.Contains('.') || name.Length == 0
            ? name
            : Spaced(char.ToUpperInvariant(name[0]) + name[1..]);

    private static string Listed(JsonArray values) => string.Join(", ", values.Select(Text));

    public static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : node?.ToJsonString() ?? "null";

    public static string Spaced(string value)
    {
        var text = string.Join(" / ", value.Split('.'));
        var spaced = new System.Text.StringBuilder(text.Length + 8);

        for (var at = 0; at < text.Length; at++)
        {
            if (at > 0 && char.IsUpper(text[at]) && (char.IsLower(text[at - 1]) || char.IsDigit(text[at - 1])))
            {
                spaced.Append(' ');
            }

            spaced.Append(text[at]);
        }

        return spaced.ToString();
    }
}
