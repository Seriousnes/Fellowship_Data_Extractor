using System.Text.Json.Nodes;

namespace Fellowship.Codex.Models;

public readonly record struct ChangedLeaf(string Path, string? Older, string? Newer);

public static class Changed
{
    public static IReadOnlyList<(string Path, string Value)> Leaves(JsonNode? node)
    {
        var found = new List<(string, string)>();

        Walk(string.Empty, node, found);

        return found;
    }

    public static IReadOnlyList<ChangedLeaf> Between(JsonNode? older, JsonNode? newer)
    {
        var before = Leaves(older);
        var after = Leaves(newer);

        var was = before.ToDictionary(leaf => leaf.Path, leaf => leaf.Value, StringComparer.Ordinal);
        var now = after.ToDictionary(leaf => leaf.Path, leaf => leaf.Value, StringComparer.Ordinal);

        var paths = new List<string>(after.Count + before.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, _) in after.Concat(before))
        {
            if (seen.Add(path)) paths.Add(path);
        }

        return
        [
            .. paths
                .Select(path => new ChangedLeaf(
                    path, was.GetValueOrDefault(path), now.GetValueOrDefault(path)))
                .Where(leaf => !string.Equals(leaf.Older, leaf.Newer, StringComparison.Ordinal)),
        ];
    }

    public static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var written)
            ? written
            : node?.ToJsonString() ?? "null";

    private static void Walk(string path, JsonNode? node, List<(string, string)> found)
    {
        switch (node)
        {
            case JsonObject carried:
                foreach (var (name, value) in carried)
                {
                    Walk(path.Length == 0 ? name : $"{path}.{name}", value, found);
                }

                break;

            case JsonArray listed:
                for (var at = 0; at < listed.Count; at++) Walk($"{path}[{at}]", listed[at], found);

                break;

            default:
                found.Add((path, Text(node)));

                break;
        }
    }
}
