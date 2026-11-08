using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fellowship.SDK.Documents;

public readonly record struct DocumentKey(string Slug, int Id);

public sealed record PatchNoteField(string Label, string? Older, string? Newer);

public sealed record PatchNote(DocumentKey Key, string? Name, string? Icon, bool Added, IReadOnlyList<PatchNoteField> Fields);

/// <summary>
/// Each build's patch notes, one note per line in <see cref="FileName" /> beside its entities.jsonl. The
/// extractor computes them, because a diff parses two whole exports and the codex runs in 512 MiB.
/// </summary>
public static class PatchNotesFile
{
    public const string FileName = "patch-notes.jsonl";

    public static string Write(IEnumerable<PatchNote> notes)
    {
        var text = new StringBuilder();

        foreach (var note in notes) text.Append(JsonSerializer.Serialize(note, PatchNotesJson.Default.PatchNote)).Append('\n');

        return text.ToString();
    }

    public static IReadOnlyList<PatchNote> Read(string text)
    {
        var notes = new List<PatchNote>();
        using var reading = new StringReader(text);

        while (reading.ReadLine() is { Length: > 0 } line)
        {
            notes.Add(JsonSerializer.Deserialize(line, PatchNotesJson.Default.PatchNote)
                ?? throw new InvalidOperationException($"A line of {FileName} deserialised to nothing."));
        }

        return notes;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PatchNote))]
internal sealed partial class PatchNotesJson : JsonSerializerContext;
