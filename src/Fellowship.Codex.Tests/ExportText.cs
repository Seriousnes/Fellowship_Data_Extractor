using System.Text;

namespace Fellowship.Codex.Tests;

/// <summary>Export lines as text, for tests that state them as strings.</summary>
public static class ExportText
{
    public static IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> Group(IEnumerable<string> lines) =>
        ExportLines.Group(Encoding.UTF8.GetBytes(string.Join('\n', lines)));

    public static string[] Read(IReadOnlyList<ReadOnlyMemory<byte>> lines) =>
        [.. lines.Select(line => Encoding.UTF8.GetString(line.Span))];
}
